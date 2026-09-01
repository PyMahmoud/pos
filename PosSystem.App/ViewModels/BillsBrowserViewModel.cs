using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for the Bills browser opened from Checkout (item #6,
    /// 2026-08-27/28 batch; reworked 2026-08-28 into receipt revisioning
    /// per Mahmoud's explicit request). Two states, both scoped to one
    /// instance — SelectedBill null means "show the bill list", non-null
    /// means "show that bill's line items" — same null-toggle pattern
    /// CustomersViewModel.SelectedDetail already established, just kept
    /// inside one ViewModel instead of two, since there's no per-bill child
    /// state complex enough to warrant its own class the way
    /// CustomerDetailViewModel's stock-check form did.
    ///
    /// Short-lived by design, same reasoning as CustomerDetailViewModel:
    /// CheckoutViewModel.OpenBillsCommand creates a fresh instance each time
    /// "Bills" is clicked and drops the reference on close, rather than
    /// caching one for the app's lifetime.
    ///
    /// RECEIPT REVISIONING (2026-08-28 rework): returning a product from a
    /// bill no longer rewrites that bill's own row in place the way the
    /// original 2026-08-27 version of this class did. Per Mahmoud's
    /// explicit requirement, a return must:
    ///   - leave the ORIGINAL receipt exactly as it was (full history, for
    ///     audit/lookup — see DatabaseBootstrapper's schema comment), and
    ///   - add a NEW bills row carrying the SAME Billnumber plus a suffix
    ///     ("210" before a return, "210-e1" after the first return against
    ///     it, "210-e2" after a second, etc. — see Core.Models.Bills.
    ///     DisplayNumber and NextRevisionSuffix below).
    /// The Bills list (LoadBills) shows EVERY row — original receipts and
    /// every revision — so "view my bill receipt history" means exactly
    /// that: #210 and #210-e1 both show up as separate, independently
    /// viewable entries, both matched by searching "210". Only the CURRENT
    /// revision of a receipt (bills.IsCurrent) can actually be returned
    /// from — opening a superseded one shows its frozen line items
    /// read-only, with a button to jump straight to the current version
    /// (see IsSelectedBillCurrent / ViewCurrentVersionCommand).
    ///
    /// Both actions are gated behind the shared AdminSession (same
    /// RequireAdminUnlocked() pattern as InventoryViewModel) — a return
    /// affects real money and real inventory, at least as sensitive as
    /// deleting a product, which is already gated.
    ///
    /// Return math (CreateReturnRevision):
    /// - Goods.Quantity is restored for whatever quantity is being
    ///   returned, via Goods.FindGoodByBarcode/FindGoodByName (best-effort
    ///   — see that method's own doc comment on why an exact match isn't
    ///   guaranteed) + Goods.UpdateGoodCountById.
    /// - The bill's Tax was stored as an absolute amount at sale time, not
    ///   a rate (Bills has no TaxRate column, and the rate could have
    ///   changed in Settings since) — so the EFFECTIVE rate is recovered as
    ///   sourceBill.Tax / oldSubtotal, and the new revision's tax is that
    ///   same ratio applied to the new (post-return) subtotal. Earned is
    ///   exact, not a ratio — every remaining line already carries its own
    ///   correct Earned (Price − Cost) × Quantity, so summing what's left
    ///   is exact, not an approximation.
    /// - Paid/Remain: Bills.Paid/Remain are set once at InsertBills and
    ///   never touched again on that SAME row anywhere else in this app —
    ///   so the ratio Paid/Billcost recovered from sourceBill's own stored
    ///   values is exact and stable (a clean 1.0 for Cash/Card, 0.0 for Pay
    ///   Later, in every real case, not a genuinely fuzzy in-between one).
    /// - The linked customer's Paid/Remain are adjusted by the DELTA
    ///   between sourceBill's Paid/Remain and the new revision's, not
    ///   overwritten outright — the customer may carry balances from other
    ///   bills too.
    /// - Time/Datex on the new revision are copied from sourceBill, NOT set
    ///   to "now" — a return is a correction to when the sale actually
    ///   happened, not a new transaction on today's date. Dashboard's
    ///   revenue trend buckets by Datex, so backdating this the normal way
    ///   (today's date) would wrongly show a big drop in revenue on the
    ///   ORIGINAL sale's day (once the superseded original is filtered out
    ///   of Dashboard's IsCurrent-only totals) with no matching change on
    ///   the day the return was actually processed — this keeps the
    ///   corrected total attributed to the same day the real sale happened.
    ///
    /// "Return whole bill" (all lines removed at once) converges with the
    /// same CreateReturnRevision path as a partial return, just with an
    /// empty remaining-lines list — it still creates a real (now zero-item,
    /// zero-cost) revision row rather than hard-deleting the bill the way
    /// the original 2026-08-27 version's DeleteWholeBill did, so the
    /// original sale stays in history like every other return.
    ///
    /// After any successful return, this reloads the bill list from the
    /// database (so newly-superseded/newly-created rows both show up) and
    /// re-selects the fresh copy of whichever bill is now open, by Id —
    /// simpler and safer than hand-patching an in-memory model object (see
    /// Core.Models.Bills — its NotifyPropertyChanged is never actually
    /// wired to any setter, so mutating fields in place wouldn't repaint
    /// bound UI anyway).
    /// </summary>
    public class BillsBrowserViewModel : ViewModelBase
    {
        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();

        private List<Core.Models.Bills> _allBills = new List<Core.Models.Bills>();
        public ObservableCollection<Core.Models.Bills> Bills { get; } = new ObservableCollection<Core.Models.Bills>();
        public ObservableCollection<Core.Models.Sells> BillLines { get; } = new ObservableCollection<Core.Models.Sells>();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        private Core.Models.Bills _selectedBill;
        public Core.Models.Bills SelectedBill
        {
            get => _selectedBill;
            private set
            {
                if (!SetProperty(ref _selectedBill, value)) return;
                OnPropertyChanged(nameof(IsSelectedBillCurrent));
                OnPropertyChanged(nameof(IsSelectedBillSuperseded));
            }
        }

        // True for a receipt's current/only version (return actions shown);
        // false for a superseded one being viewed as read-only history —
        // see BillsView's XAML for how these two drive the return-buttons-
        // vs-history-banner split.
        public bool IsSelectedBillCurrent => SelectedBill?.IsCurrent == true;
        public bool IsSelectedBillSuperseded => SelectedBill != null && !SelectedBill.IsCurrent;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Admin gate -- reworked (per Mahmoud's explicit request) to be
        // fully independent of Dashboard/Inventory/Settings' gates: this
        // ViewModel is already a fresh instance every time "Bills" is
        // opened (see class doc comment), so a private, non-shared
        // _isUnlockedThisVisit flag naturally requires the password again
        // on every reopen -- no separate lock-on-navigate-away hook needed
        // the way Dashboard/Inventory/Settings need one, since there's no
        // cached instance to leave unlocked in the background here.
        private bool _isUnlockedThisVisit;
        // GateBillsEnabled (Settings' new Access Control section, added per
        // Mahmoud's request) -- lets returns/revisions stay open even with
        // a password set elsewhere, if the owner turns Bills' own switch
        // off.
        public bool IsAdminUnlocked => !AppSettings.HasAdminPassword || !AppSettings.GateBillsEnabled || _isUnlockedThisVisit;
        public bool IsAdminLocked => !IsAdminUnlocked;

        private string _adminUnlockPasswordInput = "";
        public string AdminUnlockPasswordInput
        {
            get => _adminUnlockPasswordInput;
            set => SetProperty(ref _adminUnlockPasswordInput, value);
        }

        private string _adminUnlockError = "";
        public string AdminUnlockError
        {
            get => _adminUnlockError;
            set => SetProperty(ref _adminUnlockError, value);
        }

        public ICommand AdminUnlockCommand { get; }

        private void AdminUnlock()
        {
            if (AppSettings.VerifyAdminPassword(AdminUnlockPasswordInput))
            {
                _isUnlockedThisVisit = true;
                OnPropertyChanged(nameof(IsAdminUnlocked));
                OnPropertyChanged(nameof(IsAdminLocked));
                AdminUnlockError = "";
                AdminUnlockPasswordInput = "";
            }
            else
            {
                AdminUnlockError = LocalizationManager.GetString("DashboardUnlockIncorrect");
            }
        }

        private bool RequireAdminUnlocked()
        {
            if (IsAdminUnlocked) return true;
            StatusMessage = LocalizationManager.GetString("BillsAdminRequired");
            return false;
        }

        public ICommand ViewBillCommand { get; }
        public ICommand BackToListCommand { get; }
        public ICommand ReturnLineCommand { get; }
        public ICommand DecrementLineQuantityCommand { get; }
        public ICommand ReturnWholeBillCommand { get; }
        public ICommand ViewCurrentVersionCommand { get; }
        public ICommand CloseCommand { get; }

        // CheckoutViewModel subscribes to this to know when to drop back to
        // the normal Checkout screen — same pattern as
        // CustomerDetailViewModel.CloseRequested.
        public event Action CloseRequested;

        public BillsBrowserViewModel()
        {
            AdminUnlockCommand = new RelayCommand(_ => AdminUnlock());

            ViewBillCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Bills bill) OpenBill(bill);
            });
            BackToListCommand = new RelayCommand(_ => SelectedBill = null);
            ReturnLineCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Sells line) ReturnLine(line);
            });
            DecrementLineQuantityCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Sells line) DecrementLineQuantity(line);
            });
            ReturnWholeBillCommand = new RelayCommand(_ =>
            {
                if (SelectedBill != null) ReturnWholeBill(SelectedBill);
            });
            // A superseded (historical) bill is read-only for returns, but
            // still needs a way OUT to the receipt's actual current version
            // — otherwise someone who opened #210 (now superseded by
            // #210-e1) from a search hit would have no path to the version
            // that's actually still returnable from.
            ViewCurrentVersionCommand = new RelayCommand(_ =>
            {
                if (SelectedBill == null) return;
                var current = _allBills.FirstOrDefault(b => b.Billnumber == SelectedBill.Billnumber && b.IsCurrent);
                if (current != null) OpenBill(current);
            });
            CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());

            LoadBills();
        }

        private void LoadBills()
        {
            // Every bill row, current AND superseded — this list IS the
            // receipt history view (see class doc comment). Grouped by
            // Billnumber (newest receipt first) then by Id descending
            // within a group, so a receipt's revisions cluster together
            // with its newest version on top rather than being scattered
            // across the list by creation order alone.
            _allBills = _billsData.ReadBills("bills")
                .OrderByDescending(b => b.Billnumber)
                .ThenByDescending(b => b.Id)
                .ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<Core.Models.Bills> query = _allBills;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string text = SearchText.Trim();
                query = query.Where(b =>
                    b.DisplayNumber.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (b.Ownername != null && b.Ownername.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            Bills.Clear();
            foreach (var bill in query) Bills.Add(bill);
        }

        private void OpenBill(Core.Models.Bills bill)
        {
            SelectedBill = bill;
            LoadBillLines(bill.Id);
        }

        // BillId-scoped (2026-08-28, receipt revisioning), not
        // Billnumber-scoped — several bills rows can now share one
        // Billnumber (the original plus its revisions), and this must show
        // ONLY the specific revision that was opened, not every revision's
        // lines mixed together.
        private void LoadBillLines(int billId)
        {
            BillLines.Clear();
            foreach (var line in _sellsData.ReadSellsByBillId("sells", billId))
                BillLines.Add(line);
        }

        /// <summary>
        /// Best-effort lookup of the Goods row a sold line came from — see
        /// Goods.FindGoodByBarcode's own doc comment for why this can
        /// legitimately return null (product deleted/renamed since the
        /// sale) rather than always finding an exact match.
        /// </summary>
        private Core.Models.Goods FindSourceGood(Core.Models.Sells line)
        {
            if (!string.IsNullOrEmpty(line.Barcode))
            {
                var byBarcode = _goodsData.FindGoodByBarcode("goods", line.Barcode);
                if (byBarcode != null) return byBarcode;
            }
            return _goodsData.FindGoodByName("goods", line.Name);
        }

        private void RestoreInventoryFor(Core.Models.Sells line, double quantity)
        {
            var good = FindSourceGood(line);
            if (good == null) return; // flagged in FindSourceGood's/Goods' own doc comments — nothing to restore against
            _goodsData.UpdateGoodCountById("goods", good.Id, good.Quantity + quantity);
        }

        /// <summary>
        /// Reduces the linked customer's Paid/Remain by the given deltas
        /// (old bill value minus new bill value, for each field) and
        /// notifies Checkout/Customers to refresh. No-op if the bill isn't
        /// linked to a customer.
        /// </summary>
        private void AdjustLinkedCustomer(int? customerId, double deltaPaid, double deltaRemain)
        {
            if (!customerId.HasValue) return;

            var customer = _customersData.ReadCustomers("customers").FirstOrDefault(c => c.Id == customerId.Value);
            if (customer == null) return; // customer deleted since — nothing left to adjust

            _customersData.UpdateCustomers(
                "customers", customer.Id, customer.Ownername, customer.Ownerid, customer.Ownernumber,
                customer.Paid - deltaPaid, customer.Remain - deltaRemain);

            CustomerDataEvents.RaiseCustomersChanged();
        }

        /// <summary>
        /// Next unused bills.ID — same "read the table, take MAX(ID) + 1"
        /// approach CheckoutViewModel.CompleteSale already uses for a brand
        /// new sale's bill row, reused here for a return-revision's bill
        /// row. No dedicated "next ID" helper exists anywhere in Core, and
        /// duplicating that exact logic (rather than something new and
        /// unverified) keeps this at the same confidence level as the
        /// already-proven Checkout path.
        /// </summary>
        private int NextBillId()
        {
            DataTable billsTable = _billsData.ReadAdapter("bills");
            int nextId = 1;
            foreach (DataRow row in billsTable.Rows)
            {
                int rowId = SafeInt(row["ID"]);
                if (rowId >= nextId) nextId = rowId + 1;
            }
            return nextId;
        }

        private static int SafeInt(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

        /// <summary>
        /// "e1" for a receipt's first return, "e2" for its second, and so
        /// on — counts how many of this Billnumber's existing rows already
        /// carry a non-null RevisionSuffix (i.e. are themselves a prior
        /// return) and returns the next one. Core.Data.Bills.
        /// ReadBillRevisions reads every row sharing this Billnumber
        /// regardless of IsCurrent, so this stays correct even though only
        /// ONE of those rows is ever current at a time.
        /// </summary>
        private string NextRevisionSuffix(int billnumber)
        {
            int existingRevisions = _billsData.ReadBillRevisions("bills", billnumber)
                .Count(b => !string.IsNullOrEmpty(b.RevisionSuffix));
            return "e" + (existingRevisions + 1);
        }

        /// <summary>
        /// The heart of the return flow (2026-08-28 rework — see class doc
        /// comment's "Return math" section for the full reasoning behind
        /// each computed value). Takes the CURRENT bill a return is being
        /// made against and the list of lines that should survive into the
        /// new revision (already reflecting whatever this specific return
        /// removed or reduced — the caller builds this list, this method
        /// only writes it), and:
        ///   1. Computes the new revision's Billcost/Tax/Paid/Remain/Earned
        ///      from remainingLines.
        ///   2. Inserts the new bills row (IsCurrent = true, RevisionSuffix
        ///      = the next "eN").
        ///   3. Re-inserts remainingLines as NEW sells rows under that new
        ///      bill's Id (the OLD sells rows stay untouched, still linked
        ///      to sourceBill's Id, preserving it as a frozen snapshot).
        ///   4. Flips sourceBill to IsCurrent = false.
        ///   5. Adjusts the linked customer's balance by the delta.
        /// Does NOT touch inventory or fire refresh events — callers
        /// (ReturnLine/DecrementLineQuantity/ReturnWholeBill) each handle
        /// inventory restoration themselves first (they know exactly which
        /// units are being returned), then call this, then fire events.
        /// </summary>
        private void CreateReturnRevision(Core.Models.Bills sourceBill, List<Core.Models.Sells> remainingLines, double oldSubtotal)
        {
            double newSubtotal = remainingLines.Sum(l => l.Price * l.Quantity);
            double taxRatio = oldSubtotal > 0 ? sourceBill.Tax / oldSubtotal : 0;
            double newTax = Math.Round(newSubtotal * taxRatio, 2);

            // Discount (2026-09-01, added alongside per-customer/per-bill
            // discounts) -- same ratio-recovery approach as Tax just above:
            // sourceBill.Discount was stored as an absolute currency amount
            // (see DatabaseBootstrapper's DiscountPercent comment), so the
            // EFFECTIVE ratio against the pre-return subtotal is recovered
            // and re-applied to the new, smaller subtotal. DiscountPercent
            // itself (the number shown to a cashier) doesn't need this
            // ratio trick -- it's a rate, not an amount, so it carries
            // forward unchanged onto the new revision.
            double discountRatio = oldSubtotal > 0 ? sourceBill.Discount / oldSubtotal : 0;
            double newDiscount = Math.Round(newSubtotal * discountRatio, 2);

            double newBillcost = newSubtotal - newDiscount + newTax;
            double newEarned = remainingLines.Sum(l => l.Earned);

            double ratioPaid = sourceBill.Billcost > 0 ? sourceBill.Paid / sourceBill.Billcost : 0;
            double newPaid = Math.Round(newBillcost * ratioPaid, 2);
            double newRemain = newBillcost - newPaid;

            int newBillId = NextBillId();
            string suffix = NextRevisionSuffix(sourceBill.Billnumber);

            _billsData.InsertBills(
                "bills", newBillId, sourceBill.Billnumber, newBillcost, sourceBill.Time, sourceBill.Datex,
                sourceBill.Ownername, sourceBill.Ownerid, sourceBill.Ownernumber,
                newPaid, newRemain, newEarned, newTax, newDiscount, sourceBill.Details,
                sourceBill.CustomerId, IsCurrent: true, RevisionSuffix: suffix, DiscountPercent: sourceBill.DiscountPercent);

            foreach (var line in remainingLines)
            {
                _sellsData.InsertSells(
                    "sells", line.Name, line.Category, line.Quantity, line.Cost, line.Price,
                    line.Type, line.Time, line.Datex, line.Barcode, sourceBill.Billnumber,
                    line.Earned, line.Returned, line.Details, newBillId);
            }

            _billsData.SetBillCurrent("bills", sourceBill.Id, false);
            AdjustLinkedCustomer(sourceBill.CustomerId, sourceBill.Paid - newPaid, sourceBill.Remain - newRemain);
        }

        /// <summary>
        /// Removes one whole line from the current bill as a return — the
        /// line's full quantity goes back to inventory, and every OTHER
        /// line on the bill is carried forward unchanged into the new
        /// revision (see CreateReturnRevision).
        /// </summary>
        private void ReturnLine(Core.Models.Sells line)
        {
            if (!RequireAdminUnlocked()) return;
            var bill = SelectedBill;
            if (bill == null || !bill.IsCurrent) return; // history is read-only — see class doc comment

            try
            {
                var currentLines = _sellsData.ReadSellsByBillId("sells", bill.Id);
                double oldSubtotal = currentLines.Sum(l => l.Price * l.Quantity);

                RestoreInventoryFor(line, line.Quantity);
                var remainingLines = currentLines.Where(l => l.Id != line.Id).ToList();

                CreateReturnRevision(bill, remainingLines, oldSubtotal);

                InventoryDataEvents.RaiseGoodsChanged();
                // Reuses the "sales data changed, re-derive KPIs" signal —
                // same event a completed Checkout sale raises. Dashboard
                // only cares that the underlying bills/sells data changed,
                // not specifically that a NEW sale happened.
                OrderEvents.RaiseOrderCompleted();

                StatusMessage = string.Format(LocalizationManager.GetString("BillsDeleteLineSuccess"), line.Name, bill.DisplayNumber);

                ReloadAfterReturn(bill.Billnumber);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("BillsDeleteLineError") + " (" + ex.Message + ")";
            }
        }

        /// <summary>
        /// Returns one unit of this line (2026-08-28, receipt revisioning —
        /// mirrors the original per-line decrement Mahmoud requested
        /// 2026-08-28 in the previous batch, now producing a new revision
        /// instead of mutating the bill in place). If this was the line's
        /// last remaining unit, it converges with ReturnLine's own
        /// full-line path (a zero-quantity line serves no purpose in the
        /// new revision).
        /// </summary>
        private void DecrementLineQuantity(Core.Models.Sells line)
        {
            if (!RequireAdminUnlocked()) return;
            var bill = SelectedBill;
            if (bill == null || !bill.IsCurrent) return;

            try
            {
                var currentLines = _sellsData.ReadSellsByBillId("sells", bill.Id);
                double oldSubtotal = currentLines.Sum(l => l.Price * l.Quantity);

                RestoreInventoryFor(line, 1);

                double newQuantity = line.Quantity - 1;
                var remainingLines = new List<Core.Models.Sells>();
                foreach (var l in currentLines)
                {
                    if (l.Id != line.Id)
                    {
                        remainingLines.Add(l);
                        continue;
                    }
                    if (newQuantity <= 0) continue; // last unit — drop the line entirely, same as ReturnLine

                    l.Quantity = newQuantity;
                    l.Earned = (l.Price - l.Cost) * newQuantity;
                    remainingLines.Add(l);
                }

                CreateReturnRevision(bill, remainingLines, oldSubtotal);

                InventoryDataEvents.RaiseGoodsChanged();
                OrderEvents.RaiseOrderCompleted();

                StatusMessage = string.Format(LocalizationManager.GetString("BillsLineQuantityDecreased"), line.Name, bill.DisplayNumber);

                ReloadAfterReturn(bill.Billnumber);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("BillsAdjustLineError") + " (" + ex.Message + ")";
            }
        }

        /// <summary>
        /// Returns every line on the bill at once — same CreateReturnRevision
        /// path as a partial return, just with an empty remaining-lines
        /// list, so the result is a real (zero-item, zero-cost) revision
        /// row rather than a hard-deleted bill. Replaces the original
        /// 2026-08-27 DeleteWholeBill, which used to remove the bills row
        /// (and every sells row under it) outright — that would have
        /// erased the original sale from history entirely, which no longer
        /// fits "view my bill receipt history" now that returns are
        /// expected to leave a trail.
        /// </summary>
        private void ReturnWholeBill(Core.Models.Bills bill)
        {
            if (!RequireAdminUnlocked()) return;
            if (!bill.IsCurrent) return;

            try
            {
                var currentLines = _sellsData.ReadSellsByBillId("sells", bill.Id);
                double oldSubtotal = currentLines.Sum(l => l.Price * l.Quantity);

                foreach (var line in currentLines) RestoreInventoryFor(line, line.Quantity);

                CreateReturnRevision(bill, new List<Core.Models.Sells>(), oldSubtotal);

                InventoryDataEvents.RaiseGoodsChanged();
                OrderEvents.RaiseOrderCompleted(); // see ReturnLine's matching comment

                StatusMessage = string.Format(LocalizationManager.GetString("BillsDeleteWholeBillSuccess"), bill.DisplayNumber);

                ReloadAfterReturn(bill.Billnumber);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("BillsDeleteWholeBillError") + " (" + ex.Message + ")";
            }
        }

        /// <summary>
        /// Re-reads the bill list (so the browser's list view shows both
        /// the newly-superseded row and its brand-new replacement) and
        /// opens the receipt's fresh current revision — the one a person
        /// who just processed a return actually wants to keep looking at,
        /// not the now-historical row they were viewing a moment ago.
        /// </summary>
        private void ReloadAfterReturn(int billnumber)
        {
            LoadBills();
            var current = _allBills.FirstOrDefault(b => b.Billnumber == billnumber && b.IsCurrent);
            if (current != null) OpenBill(current);
            else SelectedBill = null; // shouldn't happen — CreateReturnRevision always inserts a new current row
        }
    }
}
