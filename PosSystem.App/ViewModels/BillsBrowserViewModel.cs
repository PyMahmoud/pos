using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for the Bills browser opened from Checkout (item #6,
    /// 2026-08-27/28 batch). Two states, both scoped to one instance —
    /// SelectedBill null means "show the bill list", non-null means "show
    /// that bill's line items" — same null-toggle pattern
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
    /// Deleting a line or a whole bill is real, financial reversal — not
    /// just a row delete — per Mahmoud's explicit requirement
    /// (2026-08-27): restore the product's Goods.Quantity, and if the bill
    /// is linked to a customer (Bills.CustomerId), reduce their Paid/Remain
    /// by whatever that line (or the whole bill) contributed. Both actions
    /// are gated behind the shared AdminSession (same
    /// RequireAdminUnlocked() pattern as InventoryViewModel) — deleting a
    /// bill or a paid line is at least as financially sensitive as deleting
    /// a product, which is already gated.
    ///
    /// Reversal math, deleting ONE line from a bill that still has other
    /// lines left (DeleteLine):
    /// - Goods.Quantity restored via Goods.FindGoodByBarcode/FindGoodByName
    ///   (best-effort — see that method's own doc comment on why an exact
    ///   match isn't guaranteed) + Goods.UpdateGoodCountById.
    /// - The bill's Tax was stored as an absolute amount at sale time, not
    ///   a rate (Bills has no TaxRate column, and the rate could have
    ///   changed in Settings since) — so the EFFECTIVE rate is recovered as
    ///   bill.Tax / oldSubtotal, and the new tax is that same ratio applied
    ///   to the new (post-deletion) subtotal. Earned is exact, not a ratio —
    ///   every remaining Sells row already carries its own correct Earned
    ///   (Price − Cost) × Quantity, so summing what's left is exact, not an
    ///   approximation.
    /// - Paid/Remain: Bills.Paid/Remain are set once at InsertBills and
    ///   never touched again anywhere else in this app (CustomersViewModel.
    ///   RecordPayment only ever updates the CUSTOMER's running Paid/Remain,
    ///   never a specific bill row) — so the ratio Paid/Billcost recovered
    ///   from the bill's own stored values is exact and stable, not
    ///   estimated: it comes out to a clean 1.0 (Cash/Card, fully paid) or
    ///   0.0 (Pay Later, fully owed) in every real case, never a genuinely
    ///   fuzzy in-between value.
    /// - The linked customer's Paid/Remain are adjusted by the DELTA
    ///   between the bill's old and new Paid/Remain, not overwritten to the
    ///   new bill values outright — the customer may carry balances from
    ///   other bills too.
    ///
    /// Deleting the LAST remaining line converges with DeleteWholeBill
    /// below (an empty bill shell serves no purpose) — same customer-balance
    /// and row-removal path, just reached via the "one line at a time" UI
    /// instead of the explicit "delete whole bill" button.
    ///
    /// DeleteWholeBill: restores inventory for every line, deletes every
    /// Sells row for the bill, reduces the linked customer's Paid/Remain by
    /// the bill's full (unmodified-since-creation, see above) Paid/Remain,
    /// then deletes the Bills row itself.
    ///
    /// After any successful mutation, this reloads the bill list from the
    /// database and re-selects the (now-updated) bill from that fresh read
    /// rather than hand-patching the in-memory Core.Models.Bills object —
    /// simpler and safer than relying on that model's property setters to
    /// raise PropertyChanged (they don't; see Core.Models.Bills — its
    /// NotifyPropertyChanged is never actually called from any setter, so
    /// mutating fields in place wouldn't repaint the bound UI anyway).
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
            private set => SetProperty(ref _selectedBill, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Admin gate — same shared AdminSession + RequireAdminUnlocked
        // pattern as InventoryViewModel; see that class's own comment for
        // why a shared session (not a per-screen password) is the design.
        public bool IsAdminUnlocked => AdminSession.IsUnlocked;
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
            if (AdminSession.TryUnlock(AdminUnlockPasswordInput))
            {
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
        public ICommand DeleteLineCommand { get; }
        public ICommand DeleteWholeBillCommand { get; }
        public ICommand CloseCommand { get; }

        // CheckoutViewModel subscribes to this to know when to drop back to
        // the normal Checkout screen — same pattern as
        // CustomerDetailViewModel.CloseRequested.
        public event Action CloseRequested;

        public BillsBrowserViewModel()
        {
            AdminUnlockCommand = new RelayCommand(_ => AdminUnlock());
            AdminSession.Changed += () =>
            {
                OnPropertyChanged(nameof(IsAdminUnlocked));
                OnPropertyChanged(nameof(IsAdminLocked));
            };

            ViewBillCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Bills bill) OpenBill(bill);
            });
            BackToListCommand = new RelayCommand(_ => SelectedBill = null);
            DeleteLineCommand = new RelayCommand(p =>
            {
                if (p is Core.Models.Sells line) DeleteLine(line);
            });
            DeleteWholeBillCommand = new RelayCommand(_ =>
            {
                if (SelectedBill != null) DeleteWholeBill(SelectedBill);
            });
            CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());

            LoadBills();
        }

        private void LoadBills()
        {
            // Newest first — Billnumber increments monotonically (see
            // CheckoutViewModel.CompleteSale's nextBillNumber computation),
            // so this is a reliable "most recent sale first" ordering
            // without needing to parse Datex/Time strings.
            _allBills = _billsData.ReadBills("bills")
                .OrderByDescending(b => b.Billnumber)
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
                    b.Billnumber.ToString().IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (b.Ownername != null && b.Ownername.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            Bills.Clear();
            foreach (var bill in query) Bills.Add(bill);
        }

        private void OpenBill(Core.Models.Bills bill)
        {
            SelectedBill = bill;
            LoadBillLines(bill.Billnumber);
        }

        private void LoadBillLines(int billnumber)
        {
            BillLines.Clear();
            foreach (var line in _sellsData.ReadSellsByBillnumber("sells", billnumber))
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

        private void RestoreInventoryFor(Core.Models.Sells line)
        {
            var good = FindSourceGood(line);
            if (good == null) return; // flagged in FindSourceGood's/Goods' own doc comments — nothing to restore against
            _goodsData.UpdateGoodCountById("goods", good.Id, good.Quantity + line.Quantity);
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
        /// Deletes the Bills row and reverses its full (unmodified-since-
        /// creation, see class doc comment) Paid/Remain against the linked
        /// customer, if any. Shared by DeleteWholeBill and by DeleteLine
        /// when the line being removed was the bill's last one.
        /// </summary>
        private void RemoveBillRow(Core.Models.Bills bill)
        {
            AdjustLinkedCustomer(bill.CustomerId, bill.Paid, bill.Remain);
            _billsData.DeleteBillById("bills", bill.Id);
        }

        private void DeleteLine(Core.Models.Sells line)
        {
            if (!RequireAdminUnlocked()) return;
            var bill = SelectedBill;
            if (bill == null) return;

            try
            {
                var originalLines = _sellsData.ReadSellsByBillnumber("sells", bill.Billnumber);
                double oldSubtotal = originalLines.Sum(l => l.Price * l.Quantity);

                RestoreInventoryFor(line);
                _sellsData.DeleteSellById("sells", line.Id);

                var remainingLines = _sellsData.ReadSellsByBillnumber("sells", bill.Billnumber);

                if (remainingLines.Count == 0)
                {
                    // Last line gone — an empty bill shell serves no
                    // purpose, so this converges with DeleteWholeBill's own
                    // row-removal step (using the bill's own still-current
                    // Paid/Remain, exactly as that method does).
                    RemoveBillRow(bill);
                    SelectedBill = null;
                }
                else
                {
                    double newSubtotal = remainingLines.Sum(l => l.Price * l.Quantity);
                    double taxRatio = oldSubtotal > 0 ? bill.Tax / oldSubtotal : 0;
                    double newTax = Math.Round(newSubtotal * taxRatio, 2);
                    double newBillcost = newSubtotal + newTax;
                    double newEarned = remainingLines.Sum(l => l.Earned);

                    double ratioPaid = bill.Billcost > 0 ? bill.Paid / bill.Billcost : 0;
                    double newPaid = Math.Round(newBillcost * ratioPaid, 2);
                    double newRemain = newBillcost - newPaid;

                    _billsData.UpdateBillAmounts("bills", bill.Id, newBillcost, newPaid, newRemain, newEarned);
                    AdjustLinkedCustomer(bill.CustomerId, bill.Paid - newPaid, bill.Remain - newRemain);
                }

                InventoryDataEvents.RaiseGoodsChanged();
                // Reuses the "sales data changed, re-derive KPIs" signal —
                // same event a completed Checkout sale raises. Dashboard
                // only cares that the underlying bills/sells data changed,
                // not specifically that a NEW sale happened.
                OrderEvents.RaiseOrderCompleted();

                StatusMessage = string.Format(LocalizationManager.GetString("BillsDeleteLineSuccess"), line.Name, bill.Billnumber);

                LoadBills();
                if (SelectedBill != null)
                {
                    // Re-select the freshly-reloaded copy of the same bill
                    // (by Id) rather than hand-patching the old in-memory
                    // instance — see class doc comment on why.
                    var refreshed = _allBills.FirstOrDefault(b => b.Id == bill.Id);
                    OpenBill(refreshed ?? bill);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("BillsDeleteLineError") + " (" + ex.Message + ")";
            }
        }

        private void DeleteWholeBill(Core.Models.Bills bill)
        {
            if (!RequireAdminUnlocked()) return;

            try
            {
                int billnumber = bill.Billnumber;
                var lines = _sellsData.ReadSellsByBillnumber("sells", billnumber);
                foreach (var line in lines) RestoreInventoryFor(line);

                _sellsData.DeleteSellsByBillnumber("sells", billnumber);
                RemoveBillRow(bill);

                InventoryDataEvents.RaiseGoodsChanged();
                OrderEvents.RaiseOrderCompleted(); // see DeleteLine's matching comment

                StatusMessage = string.Format(LocalizationManager.GetString("BillsDeleteWholeBillSuccess"), billnumber);

                SelectedBill = null;
                LoadBills();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("BillsDeleteWholeBillError") + " (" + ex.Message + ")";
            }
        }
    }
}
