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

        public Customers Customer { get; }

        public ObservableCollection<SoldMedicationSummary> SoldMedications { get; } = new ObservableCollection<SoldMedicationSummary>();
        public ObservableCollection<StockCheck> StockCheckHistory { get; } = new ObservableCollection<StockCheck>();
        public ObservableCollection<GoodsR> AvailableMedications { get; } = new ObservableCollection<GoodsR>();

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

            SaveStockCheckCommand = new RelayCommand(_ => SaveStockCheck());
            CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());

            LoadSalesHistory();
            LoadStockCheckHistory();
            LoadAvailableMedications();
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
