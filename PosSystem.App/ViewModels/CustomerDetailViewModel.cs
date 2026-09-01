using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;
using PosSystem.Core.Models;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for the customer detail drill-down, opened from a "View
    /// Details" button on a CustomersView card. Two things live here, both
    /// scoped to one customer:
    ///
    /// - Sales history: every Bills row linked to this customer via
    ///   bills.CustomerId, joined to its Sells line items — "what
    ///   medications did I sell this pharmacy, and how much".
    /// - Stock checks: the append-only on-hand log a rep fills in on every
    ///   visit (Quantity + BatchNumber + ExpiryDate + Notes), newest first.
    ///   This is genuinely new data with no prior analog in the schema —
    ///   see DatabaseBootstrapper for the `stockchecks` table it lives in.
    ///   Deliberately records what's left AT THE PHARMACY of what was
    ///   already sold to them (restock-decision signal), not the rep's own
    ///   stock — that's what the existing Inventory screen covers.
    ///
    /// Short-lived by design (unlike every other ViewModel in this app):
    /// CustomersViewModel creates a fresh instance each time "View Details"
    /// is clicked and drops the reference when closed, rather than caching
    /// one per customer forever the way MainViewModel caches screens — a
    /// pharma rep could have hundreds of pharmacy customers, and keeping
    /// every detail ViewModel (plus its loaded sales history) alive for the
    /// app's lifetime would leak memory for no benefit.
    /// </summary>
    public class CustomerDetailViewModel : ViewModelBase
    {
        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();
        private readonly Core.Data.StockChecks _stockChecksData = new Core.Data.StockChecks();
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();
        private readonly Core.Data.Payments _paymentsData = new Core.Data.Payments();

        public Customers Customer { get; }

        public ObservableCollection<SoldMedicationSummary> SoldMedications { get; } = new ObservableCollection<SoldMedicationSummary>();
        public ObservableCollection<StockCheck> StockCheckHistory { get; } = new ObservableCollection<StockCheck>();
        public ObservableCollection<GoodsR> AvailableMedications { get; } = new ObservableCollection<GoodsR>();

        // Balance + payment history (added 2026-08-31). Kept as this
        // ViewModel's OWN observable properties, not read straight off
        // Customer above -- Customers (Core.Models) has plain get/set
        // properties with no INotifyPropertyChanged, so a revert or a new
        // manual credit wouldn't repaint the balance summary on screen
        // unless something here actually raises PropertyChanged. Customer
        // itself is also kept in sync (see RefreshBalance below) so
        // anything that reads Customer.Paid/Remain/CreditOwed directly
        // (there is none today, but a future addition might) doesn't see a
        // stale snapshot from construction time.
        private double _paid;
        public double Paid { get => _paid; private set => SetProperty(ref _paid, value); }

        private double _remain;
        public double Remain { get => _remain; private set { if (SetProperty(ref _remain, value)) OnPropertyChanged(nameof(HasDebt)); } }

        private double _creditOwed;
        public double CreditOwed { get => _creditOwed; private set { if (SetProperty(ref _creditOwed, value)) OnPropertyChanged(nameof(HasCredit)); } }

        public bool HasDebt => Remain > 0;
        public bool HasCredit => CreditOwed > 0;

        public ObservableCollection<Payment> PaymentHistory { get; } = new ObservableCollection<Payment>();

        private string _creditAmountInput = "";
        public string CreditAmountInput
        {
            get => _creditAmountInput;
            set => SetProperty(ref _creditAmountInput, value);
        }

        private string _creditNoteInput = "";
        public string CreditNoteInput
        {
            get => _creditNoteInput;
            set => SetProperty(ref _creditNoteInput, value);
        }

        public ICommand AddManualCreditCommand { get; }
        public ICommand RevertPaymentCommand { get; }

        // Default discount % (added 2026-09-01) -- this customer's standing
        // discount, auto-applied to a new Checkout bill the moment they're
        // selected there (see CheckoutViewModel.SelectedCustomer's setter),
        // editable per-bill from there without changing what's saved here.
        // Gated the same way as every other admin-gated section in this
        // app -- a private, non-shared "unlocked this visit" flag, no
        // Unloaded-on-navigate-away hook needed since this whole ViewModel
        // is already fresh every time "View Details" is opened (see class
        // doc comment).
        private double _discountPercent;
        public double DiscountPercent { get => _discountPercent; private set => SetProperty(ref _discountPercent, value); }

        private string _discountPercentInput = "";
        public string DiscountPercentInput
        {
            get => _discountPercentInput;
            set => SetProperty(ref _discountPercentInput, value);
        }

        private bool _isDiscountUnlockedThisVisit;
        public bool IsDiscountUnlocked => !AppSettings.HasAdminPassword || !AppSettings.GateDiscountEnabled || _isDiscountUnlockedThisVisit;
        public bool IsDiscountLocked => !IsDiscountUnlocked;

        private string _discountUnlockPasswordInput = "";
        public string DiscountUnlockPasswordInput
        {
            get => _discountUnlockPasswordInput;
            set => SetProperty(ref _discountUnlockPasswordInput, value);
        }

        private string _discountUnlockError = "";
        public string DiscountUnlockError
        {
            get => _discountUnlockError;
            set => SetProperty(ref _discountUnlockError, value);
        }

        public ICommand DiscountUnlockCommand { get; }
        public ICommand SaveDiscountCommand { get; }

        private void DiscountUnlock()
        {
            if (AppSettings.VerifyAdminPassword(DiscountUnlockPasswordInput))
            {
                _isDiscountUnlockedThisVisit = true;
                OnPropertyChanged(nameof(IsDiscountUnlocked));
                OnPropertyChanged(nameof(IsDiscountLocked));
                DiscountUnlockError = "";
                DiscountUnlockPasswordInput = "";
            }
            else
            {
                DiscountUnlockError = LocalizationManager.GetString("DashboardUnlockIncorrect");
            }
        }

        private void SaveDiscount()
        {
            if (!IsDiscountUnlocked)
            {
                StatusMessage = LocalizationManager.GetString("CustomerDetailDiscountAdminRequired");
                return;
            }

            if (!double.TryParse(DiscountPercentInput, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double value) || value < 0 || value > 100)
            {
                StatusMessage = LocalizationManager.GetString("CustomerDetailDiscountInvalid");
                return;
            }

            try
            {
                _customersData.UpdateCustomerDiscount("customers", Customer.Id, value);
                Customer.DiscountPercent = value;
                DiscountPercent = value;
                DiscountPercentInput = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

                StatusMessage = LocalizationManager.GetString("CustomerDetailDiscountSuccess");

                // Checkout's customer picker doesn't re-read this customer's
                // model until it rebuilds its own list -- same reasoning as
                // every other write here raising CustomerDataEvents.
                CustomerDataEvents.RaiseCustomersChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CustomerDetailDiscountError") + " (" + ex.Message + ")";
            }
        }

        // CustomersViewModel subscribes to this (alongside CloseRequested)
        // to know a payment/credit/revert here changed this customer's
        // balance, so the list card behind this detail page shows the
        // current numbers the moment the person goes back to it instead of
        // whatever was cached when "View Details" was first clicked.
        public event Action BalanceChanged;

        private GoodsR _selectedMedication;
        public GoodsR SelectedMedication
        {
            get => _selectedMedication;
            set => SetProperty(ref _selectedMedication, value);
        }

        private string _quantityInput = "";
        public string QuantityInput
        {
            get => _quantityInput;
            set => SetProperty(ref _quantityInput, value);
        }

        private string _batchInput = "";
        public string BatchInput
        {
            get => _batchInput;
            set => SetProperty(ref _batchInput, value);
        }

        private string _expiryInput = "";
        public string ExpiryInput
        {
            get => _expiryInput;
            set => SetProperty(ref _expiryInput, value);
        }

        private string _notesInput = "";
        public string NotesInput
        {
            get => _notesInput;
            set => SetProperty(ref _notesInput, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SaveStockCheckCommand { get; }
        public ICommand CloseCommand { get; }

        // CustomersViewModel subscribes to this to know when to drop back
        // to the list view — simpler than this ViewModel reaching back into
        // its owner directly.
        public event Action CloseRequested;

        public CustomerDetailViewModel(Customers customer)
        {
            Customer = customer;
            _paid = customer.Paid;
            _remain = customer.Remain;
            _creditOwed = customer.CreditOwed;
            _discountPercent = customer.DiscountPercent;
            _discountPercentInput = customer.DiscountPercent.ToString(System.Globalization.CultureInfo.InvariantCulture);

            SaveStockCheckCommand = new RelayCommand(_ => SaveStockCheck());
            CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
            AddManualCreditCommand = new RelayCommand(_ => AddManualCredit());
            RevertPaymentCommand = new RelayCommand(p =>
            {
                if (p is Payment payment) RevertPayment(payment);
            });
            DiscountUnlockCommand = new RelayCommand(_ => DiscountUnlock());
            SaveDiscountCommand = new RelayCommand(_ => SaveDiscount());

            LoadSalesHistory();
            LoadStockCheckHistory();
            LoadAvailableMedications();
            LoadPaymentHistory();
        }

        private void LoadPaymentHistory()
        {
            PaymentHistory.Clear();
            foreach (var payment in _paymentsData.ReadByCustomer(Customer.Id))
                PaymentHistory.Add(payment);
        }

        // Writes a new Paid/Remain/CreditOwed triple to both the database
        // and this ViewModel's own observable properties (see those
        // properties' doc comment for why a plain assignment to Customer
        // alone wouldn't repaint anything), then tells CustomersViewModel
        // the list needs refreshing too.
        private void PersistBalance(double newPaid, double newRemain, double newCredit)
        {
            _customersData.UpdateCustomerBalance(
                "customers", Customer.Id, Customer.Ownername, Customer.Ownerid, Customer.Ownernumber,
                newPaid, newRemain, newCredit);

            Customer.Paid = newPaid;
            Customer.Remain = newRemain;
            Customer.CreditOwed = newCredit;

            Paid = newPaid;
            Remain = newRemain;
            CreditOwed = newCredit;

            CustomerDataEvents.RaiseCustomersChanged();
            BalanceChanged?.Invoke();
        }

        // Manual credit entry (added 2026-08-31) -- e.g. a refund or
        // goodwill credit that isn't tied to any payment the customer
        // actually made. Never touches Paid/Remain, only CreditOwed --
        // see Models.Payment's doc comment on the "Credit" Type.
        private void AddManualCredit()
        {
            if (!double.TryParse(CreditAmountInput, out double amount) || amount <= 0)
            {
                StatusMessage = LocalizationManager.GetString("CustomerDetailCreditInvalid");
                return;
            }

            try
            {
                double previousPaid = Paid;
                double previousRemain = Remain;
                double previousCredit = CreditOwed;
                double newCredit = previousCredit + amount;

                PersistBalance(previousPaid, previousRemain, newCredit);

                DateTime now = DateTime.Now;
                _paymentsData.InsertPayment(
                    Customer.Id, "Credit", amount, 0, amount,
                    previousPaid, previousRemain, previousCredit,
                    (CreditNoteInput ?? "").Trim(), now.ToString("dd/MM/yyyy"), now.ToString("HH:mm"));

                CreditAmountInput = "";
                CreditNoteInput = "";
                StatusMessage = LocalizationManager.GetString("CustomerDetailCreditSuccess");
                LoadPaymentHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CustomerDetailCreditError") + " (" + ex.Message + ")";
            }
        }

        // Revert (added 2026-08-31) -- reverses this SPECIFIC payment's
        // effect by subtracting its Amount/AppliedToRemain/AppliedToCredit
        // back out of the customer's CURRENT balances, not by restoring
        // that row's Previous* snapshot -- see Models.Payment's doc comment
        // for why subtracting is the one that stays correct regardless of
        // which order payments get reverted in. Clamped at 0: a payment
        // reverted after some OTHER change already moved the balance below
        // what a raw subtraction would produce (e.g. more debt added since,
        // or credit already spent down some other way this app doesn't
        // model yet) should floor at 0 rather than go negative, which has
        // no real-world meaning for either Remain or CreditOwed.
        private void RevertPayment(Payment payment)
        {
            if (payment.IsReverted) return;

            try
            {
                double newPaid = Paid;
                double newRemain = Remain;
                double newCredit = CreditOwed;

                if (string.Equals(payment.Type, "Credit", StringComparison.OrdinalIgnoreCase))
                {
                    newCredit = Math.Max(0, CreditOwed - payment.Amount);
                }
                else
                {
                    newPaid = Math.Max(0, Paid - payment.Amount);
                    newRemain = Math.Max(0, Remain + payment.AppliedToRemain);
                    newCredit = Math.Max(0, CreditOwed - payment.AppliedToCredit);
                }

                PersistBalance(newPaid, newRemain, newCredit);

                _paymentsData.MarkReverted(payment.Id);
                payment.IsReverted = true;

                StatusMessage = LocalizationManager.GetString("CustomerDetailRevertSuccess");
                LoadPaymentHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CustomerDetailRevertError") + " (" + ex.Message + ")";
            }
        }

        private void LoadSalesHistory()
        {
            // Filtered to IsCurrent = 1 (2026-08-28, receipt revisioning --
            // see DatabaseBootstrapper's matching comment), same reasoning
            // as DashboardViewModel.RefreshDashboard's matching filter: a
            // returned bill's original row stays in `bills` as history, so
            // reading every bill for this customer unfiltered would count a
            // returned medication's revenue/quantity twice -- once from the
            // now-superseded original, once from its replacement. Matched
            // by BillId, not Billnumber, for the same reason Dashboard's
            // filter is -- a superseded bill's lines share their
            // replacement's Billnumber.
            var bills = _billsData.ReadBillsByCustomer("bills", Customer.Id).Where(b => b.IsCurrent).ToList();
            var billIds = new HashSet<int>(bills.Select(b => b.Id));

            var lines = _sellsData.ReadPendingSell("sells")
                .Where(s => billIds.Contains(s.BillId));

            var grouped = lines
                .GroupBy(s => s.Name)
                .Select(g => new SoldMedicationSummary
                {
                    Name = g.Key,
                    TotalQuantity = g.Sum(s => s.Quantity),
                    TotalRevenue = g.Sum(s => s.Quantity * s.Price)
                })
                .OrderByDescending(x => x.TotalRevenue);

            SoldMedications.Clear();
            foreach (var item in grouped) SoldMedications.Add(item);
        }

        private void LoadStockCheckHistory()
        {
            StockCheckHistory.Clear();
            foreach (var check in _stockChecksData.ReadByCustomer(Customer.Id))
                StockCheckHistory.Add(check);
        }

        private void LoadAvailableMedications()
        {
            AvailableMedications.Clear();
            foreach (var good in _goodsData.ReadAllGoodsRPic("goods").OrderBy(g => g.Name))
                AvailableMedications.Add(good);
            SelectedMedication = AvailableMedications.FirstOrDefault();
        }

        private void SaveStockCheck()
        {
            if (SelectedMedication == null)
            {
                StatusMessage = LocalizationManager.GetString("StockCheckMissingMedication");
                return;
            }
            if (!double.TryParse(QuantityInput, out double quantity) || quantity < 0)
            {
                StatusMessage = LocalizationManager.GetString("StockCheckInvalidQuantity");
                return;
            }

            try
            {
                DateTime now = DateTime.Now;
                _stockChecksData.InsertStockCheck(
                    Customer.Id, SelectedMedication.Barcode, SelectedMedication.Name,
                    quantity, (BatchInput ?? "").Trim(), (ExpiryInput ?? "").Trim(),
                    now.ToString("dd/MM/yyyy"), now.ToString("HH:mm"), (NotesInput ?? "").Trim());

                QuantityInput = "";
                BatchInput = "";
                ExpiryInput = "";
                NotesInput = "";

                StatusMessage = LocalizationManager.GetString("StockCheckSaveSuccess");
                LoadStockCheckHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("StockCheckSaveError") + " (" + ex.Message + ")";
            }
        }
    }
}
