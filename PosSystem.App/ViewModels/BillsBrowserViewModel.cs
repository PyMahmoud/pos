using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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
    /// STAGED RETURNS (2026-09-04, per Baraa's explicit request) — every
    /// return action below (−, Return, Return Whole Bill) used to call
    /// CreateReturnRevision immediately, so returning 3 products off one
    /// bill created 3 separate new revisions ("210-e1", "210-e2", "210-e3")
    /// instead of one. These actions now only STAGE a pending return
    /// quantity on each Core.Models.Sells line (see that class's
    /// PendingReturnQuantity doc comment) — no database write, no
    /// inventory change, no new revision — and SaveReturnsCommand is the
    /// only thing that actually calls CreateReturnRevision, once, covering
    /// every staged line at once. Staged state lives only on the in-memory
    /// BillLines objects, so navigating away from a bill without saving
    /// (BackToListCommand, opening a different bill, or closing the
    /// browser) silently discards it — LoadBillLines always re-reads fresh
    /// Sells objects from the database with PendingReturnQuantity at its
    /// default of 0, nothing was ever written, so there's nothing to lose.
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
        public ICommand UndoLineReturnCommand { get; }
        public ICommand ReturnWholeBillCommand { get; }
        public ICommand SaveReturnsCommand { get; }
        public ICommand DiscardReturnsCommand { get; }
        public ICommand ViewCurrentVersionCommand { get; }
        public ICommand CloseCommand { get; }

        // Staged-returns (2026-09-04) — true while at least one BillLines
        // entry has a nonzero PendingReturnQuantity. Every staging method
        // below (StageLineForReturn/StageLineUnitReturn/UndoLineReturn/
        // StageWholeBillReturn/DiscardPendingReturns) raises this manually
        // after mutating a line rather than this ViewModel subscribing to
        // each Sells object's PropertyChanged -- simpler, since every
        // mutation path already funnels through one of these methods (all
        // UI-command-driven), so there's no other place PendingReturnQuantity
        // could change out from under it.
        public bool HasPendingReturns => BillLines.Any(l => l.PendingReturnQuantity > 0);

        // CheckoutViewModel subscribes to this to know when to drop back to
        // the normal Checkout screen — same pattern as
        // CustomerDetailViewModel.CloseRequested.
        public event Action CloseRequested;

        // Busy guard (2026-09-09, performance work) -- SaveReturnsAsync now
        // runs its database work in the background instead of freezing the
        // screen while it happens (see that method's own doc comment for
        // the full reasoning). Before this change, the UI being completely
        // frozen during a save had one accidental side benefit: it was
        // physically impossible to click anything else (navigate to a
        // different bill, stage another return, click Save twice) while a
        // save was in flight. Now that the screen stays responsive, that
        // same guarantee has to be provided on purpose instead of for
        // free -- every command below that could conflict with an
        // in-flight save is gated on this flag, so the app still behaves,
        // from the user's perspective, as if nothing else can be touched
        // until the save finishes -- it just no longer LOOKS frozen while
        // that's true.
        private bool _isSavingReturns;

        public BillsBrowserViewModel()
        {
            AdminUnlockCommand = new RelayCommand(_ => AdminUnlock());

            ViewBillCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Bills bill) OpenBill(bill);
            }, _ => !_isSavingReturns);
            BackToListCommand = new RelayCommand(_ => SelectedBill = null, _ => !_isSavingReturns);
            ReturnLineCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Sells line) StageLineForReturn(line);
            }, _ => !_isSavingReturns);
            DecrementLineQuantityCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Sells line) StageLineUnitReturn(line);
            }, _ => !_isSavingReturns);
            UndoLineReturnCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Sells line) UndoLineReturn(line);
            }, _ => !_isSavingReturns);
            ReturnWholeBillCommand = new RelayCommand(_ =>
            {
                if (SelectedBill != null) StageWholeBillReturn();
            }, _ => !_isSavingReturns);
            // async-void execute delegate (2026-09-09) -- RelayCommand's
            // Action<object> parameter happily accepts an async lambda
            // (this is standard, well-supported C#; ICommand.Execute has
            // always returned void, so there is no Task for the command
            // infrastructure to await either way) -- SaveReturnsAsync
            // itself has its own try/catch around all the real work (same
            // as the old synchronous SaveReturns did), so an async void
            // here doesn't lose error handling the way async void
            // sometimes can.
            SaveReturnsCommand = new RelayCommand(async _ => await SaveReturnsAsync(), _ => HasPendingReturns && !_isSavingReturns);
            DiscardReturnsCommand = new RelayCommand(_ => DiscardPendingReturns(), _ => HasPendingReturns && !_isSavingReturns);
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
            }, _ => !_isSavingReturns);
            CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(), _ => !_isSavingReturns);

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
        /// (old bill value minus new bill value, for each field). No-op if
        /// the bill isn't linked to a customer, or that customer no longer
        /// exists. Returns whether an adjustment was actually made, so the
        /// caller knows whether to raise CustomerDataEvents.
        /// RaiseCustomersChanged() -- moved out of this method (2026-09-09,
        /// performance work) since this method's own database calls now
        /// run on a background thread as part of SaveReturnsAsync (see that
        /// method's doc comment), and that notification event has other
        /// screens' ObservableCollections listening on it with no thread-
        /// safety of its own -- it must only ever be raised from the UI
        /// thread, which is the caller's job now, after the background work
        /// this method is part of has finished.
        /// </summary>
        private bool AdjustLinkedCustomer(int? customerId, double deltaPaid, double deltaRemain)
        {
            if (!customerId.HasValue) return false;

            var customer = _customersData.ReadCustomers("customers").FirstOrDefault(c => c.Id == customerId.Value);
            if (customer == null) return false; // customer deleted since — nothing left to adjust

            _customersData.UpdateCustomers(
                "customers", customer.Id, customer.Ownername, customer.Ownerid, customer.Ownernumber,
                customer.Paid - deltaPaid, customer.Remain - deltaRemain);

            return true;
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
        private bool CreateReturnRevision(Core.Models.Bills sourceBill, List<Core.Models.Sells> remainingLines, double oldSubtotal)
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
                sourceBill.CustomerId, IsCurrent: true, RevisionSuffix: suffix, DiscountPercent: sourceBill.DiscountPercent,
                PaymentReference: sourceBill.PaymentReference);

            foreach (var line in remainingLines)
            {
                // OriginalPrice/DiscountPercent (2026-09-09) -- carried
                // forward unchanged from the line being re-inserted (it was
                // already read from the database with these populated, see
                // Core.Data.Sells' read methods), same as every other
                // per-unit fact about this line (Cost, Type, Barcode) that a
                // return doesn't change.
                _sellsData.InsertSells(
                    "sells", line.Name, line.Category, line.Quantity, line.Cost, line.Price,
                    line.Type, line.Time, line.Datex, line.Barcode, sourceBill.Billnumber,
                    line.Earned, line.Returned, line.Details, newBillId,
                    line.OriginalPrice, line.DiscountPercent);
            }

            _billsData.SetBillCurrent("bills", sourceBill.Id, false);
            return AdjustLinkedCustomer(sourceBill.CustomerId, sourceBill.Paid - newPaid, sourceBill.Remain - newRemain);
        }

        /// <summary>
        /// STAGES the line's full remaining quantity for return (2026-09-04
        /// rework — see class doc comment's "STAGED RETURNS" section). No
        /// database write, no inventory change yet — just sets this line's
        /// PendingReturnQuantity, which SaveReturnsCommand later commits
        /// alongside any other staged lines in one revision. Acts as a
        /// toggle: clicking again on an already-fully-staged line clears
        /// the staging back to 0, since that's the obvious "undo" for a
        /// single-click action.
        /// </summary>
        private void StageLineForReturn(Core.Models.Sells line)
        {
            var bill = SelectedBill;
            if (bill == null || !bill.IsCurrent) return; // history is read-only — see class doc comment

            line.PendingReturnQuantity = line.PendingReturnQuantity >= line.Quantity ? 0 : line.Quantity;
            OnPropertyChanged(nameof(HasPendingReturns));
        }

        /// <summary>
        /// STAGES one additional unit of this line for return (2026-09-04
        /// rework of the original per-line decrement) — increments
        /// PendingReturnQuantity by 1, capped at the line's full Quantity.
        /// Nothing is written to the database or restored to inventory
        /// until SaveReturnsCommand runs.
        /// </summary>
        private void StageLineUnitReturn(Core.Models.Sells line)
        {
            var bill = SelectedBill;
            if (bill == null || !bill.IsCurrent) return;

            if (line.PendingReturnQuantity < line.Quantity)
                line.PendingReturnQuantity += 1;
            OnPropertyChanged(nameof(HasPendingReturns));
        }

        /// <summary>
        /// Clears a single line's staged return back to 0 — the per-line
        /// "undo" shown next to a line once it has anything staged, without
        /// discarding any OTHER line's staged returns the way
        /// DiscardPendingReturns does for the whole bill.
        /// </summary>
        private void UndoLineReturn(Core.Models.Sells line)
        {
            line.PendingReturnQuantity = 0;
            OnPropertyChanged(nameof(HasPendingReturns));
        }

        /// <summary>
        /// STAGES every line on the bill for full return at once (2026-09-04
        /// rework of the original immediate ReturnWholeBill) — sets every
        /// BillLines entry's PendingReturnQuantity to its full Quantity.
        /// Still just staging: SaveReturnsCommand is what actually creates
        /// the (now zero-item, zero-cost) revision.
        /// </summary>
        private void StageWholeBillReturn()
        {
            var bill = SelectedBill;
            if (bill == null || !bill.IsCurrent) return;

            foreach (var line in BillLines) line.PendingReturnQuantity = line.Quantity;
            OnPropertyChanged(nameof(HasPendingReturns));
        }

        /// <summary>
        /// Clears every line's staged return back to 0 — the "Discard"
        /// button next to Save Returns, for backing out of a whole staged
        /// batch without saving any of it.
        /// </summary>
        private void DiscardPendingReturns()
        {
            foreach (var line in BillLines) line.PendingReturnQuantity = 0;
            OnPropertyChanged(nameof(HasPendingReturns));
            StatusMessage = LocalizationManager.GetString("BillsDiscardReturnsSuccess");
        }

        /// <summary>
        /// Commits every currently-staged line at once (2026-09-04, the
        /// actual point of the staged-returns rework — see class doc
        /// comment) as a SINGLE CreateReturnRevision call, so returning
        /// several products off one bill produces exactly one new revision
        /// instead of one per product. For each staged line: restores
        /// PendingReturnQuantity units to inventory, then either drops the
        /// line entirely (fully returned) or carries it forward at its
        /// reduced quantity — same per-line logic the old immediate
        /// ReturnLine/DecrementLineQuantity used, just applied to every
        /// staged line in one pass instead of one line per database write.
        /// Re-reads currentLines fresh from the database rather than
        /// trusting BillLines' bound objects directly, same defensive
        /// reasoning the original per-action methods already followed.
        ///
        /// ASYNC REWRITE (2026-09-09, performance work): this used to run
        /// entirely on the UI thread, which is exactly why saving a return
        /// with several lines noticeably froze the screen — a chain of
        /// 15-20 back-to-back database calls (read the bill's lines, look
        /// up and update each returned product's stock, insert the new
        /// revision's bill row, insert each of its line rows, mark the old
        /// bill superseded, adjust the customer's balance) all ran one
        /// after another with nothing else able to run until the last one
        /// finished. Every one of those calls is now done inside
        /// Task.Run(...) on a background thread instead, and everything
        /// that touches an on-screen element (StatusMessage, the Bills and
        /// BillLines lists, the data-changed events other screens listen
        /// for) happens only AFTER that background work has fully finished
        /// and control has returned to the UI thread — the same order of
        /// operations and the same final result as before, just without
        /// blocking the screen while it's in progress.
        ///
        /// This split matters: currentLines/remainingLines here are fresh
        /// objects read directly from the database inside the background
        /// block, never the same objects as BillLines (the list actually
        /// bound to the screen) — so mutating them off the UI thread is
        /// safe, nothing visible is being touched from the wrong thread.
        /// CustomerDataEvents.RaiseCustomersChanged() in particular used to
        /// fire from inside this same chain (via AdjustLinkedCustomer) —
        /// that event has no thread-safety of its own and other screens'
        /// lists listen for it, so AdjustLinkedCustomer/CreateReturnRevision
        /// were changed (see their own doc comments) to report back
        /// whether a customer needs updating instead of raising it
        /// themselves, so it can be raised here, safely, after the
        /// background work is done.
        /// </summary>
        private async Task SaveReturnsAsync()
        {
            if (!RequireAdminUnlocked()) return;
            var bill = SelectedBill;
            if (bill == null || !bill.IsCurrent) return;

            var stagedIds = BillLines.Where(l => l.PendingReturnQuantity > 0)
                .ToDictionary(l => l.Id, l => l.PendingReturnQuantity);
            if (stagedIds.Count == 0) return;

            _isSavingReturns = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                int returnedLineCount = 0;
                bool customerAdjusted = false;

                // Everything inside this block is the exact same logic the
                // old synchronous SaveReturns ran, in the exact same order
                // — just executed on a background thread instead of the UI
                // thread, and touching only local variables / the database,
                // never anything bound to the screen.
                await Task.Run(() =>
                {
                    var currentLines = _sellsData.ReadSellsByBillId("sells", bill.Id);
                    double oldSubtotal = currentLines.Sum(l => l.Price * l.Quantity);

                    var remainingLines = new List<Core.Models.Sells>();
                    foreach (var line in currentLines)
                    {
                        if (!stagedIds.TryGetValue(line.Id, out double pendingQuantity) || pendingQuantity <= 0)
                        {
                            remainingLines.Add(line);
                            continue;
                        }

                        RestoreInventoryFor(line, pendingQuantity);
                        returnedLineCount++;

                        double newQuantity = line.Quantity - pendingQuantity;
                        if (newQuantity <= 0) continue; // fully returned - drop the line entirely

                        line.Quantity = newQuantity;
                        line.Earned = (line.Price - line.Cost) * newQuantity;
                        line.Returned = "Yes";
                        remainingLines.Add(line);
                    }

                    customerAdjusted = CreateReturnRevision(bill, remainingLines, oldSubtotal);
                });

                // Back on the UI thread from here on (an await in a WPF app
                // resumes on the original UI thread by default) — safe to
                // touch StatusMessage, raise the shared events, and update
                // the on-screen lists.
                InventoryDataEvents.RaiseGoodsChanged();
                OrderEvents.RaiseOrderCompleted();
                if (customerAdjusted) CustomerDataEvents.RaiseCustomersChanged();

                StatusMessage = string.Format(LocalizationManager.GetString("BillsSaveReturnsSuccess"), returnedLineCount, bill.DisplayNumber);

                await ReloadAfterReturnAsync(bill.Billnumber);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("BillsSaveReturnsError") + " (" + ex.Message + ")";
            }
            finally
            {
                _isSavingReturns = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Re-reads the bill list (so the browser's list view shows both
        /// the newly-superseded row and its brand-new replacement) and
        /// opens the receipt's fresh current revision — the one a person
        /// who just processed a return actually wants to keep looking at,
        /// not the now-historical row they were viewing a moment ago.
        ///
        /// Made async (2026-09-09, performance work) for the same reason
        /// SaveReturnsAsync was — re-reading the whole bills table plus the
        /// new revision's line items is two more database round-trips
        /// right after the several SaveReturnsAsync already did; running
        /// them on a background thread too keeps the screen responsive the
        /// whole way through, not just for the first part of the save.
        /// Only the final list/selection updates (which must happen on the
        /// UI thread, since Bills/BillLines are bound directly to the
        /// screen) run after the background read completes.
        /// </summary>
        private async Task ReloadAfterReturnAsync(int billnumber)
        {
            List<Core.Models.Bills> freshBills = null;
            await Task.Run(() =>
            {
                freshBills = _billsData.ReadBills("bills")
                    .OrderByDescending(b => b.Billnumber)
                    .ThenByDescending(b => b.Id)
                    .ToList();
            });

            _allBills = freshBills;
            ApplyFilter();

            var current = _allBills.FirstOrDefault(b => b.Billnumber == billnumber && b.IsCurrent);
            if (current == null)
            {
                SelectedBill = null; // shouldn't happen — CreateReturnRevision always inserts a new current row
                return;
            }

            SelectedBill = current;

            List<Core.Models.Sells> freshLines = null;
            await Task.Run(() =>
            {
                freshLines = _sellsData.ReadSellsByBillId("sells", current.Id).ToList();
            });

            BillLines.Clear();
            foreach (var line in freshLines) BillLines.Add(line);
        }
    }
}
