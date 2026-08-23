using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;
using PosSystem.Core.Models;

namespace PosSystem.App.ViewModels
{
    public enum PaymentMethod
    {
        Cash,
        Card,
        // Phase 5 addition. Only reachable when a real customer (not
        // Walk-in) is selected — see CanPayLater / the guard at the top of
        // CompleteSale(). Leaves the bill partially/fully unpaid and adds
        // the difference to the linked customer's Remain.
        PayLater
    }

    /// <summary>
    /// DataContext for CheckoutView. Loads all goods once on construction
    /// and filters client-side by category + search text — 281 rows is
    /// nothing to filter in memory, and it means typing in the search box
    /// doesn't round-trip SQLite on every keystroke.
    ///
    /// On Complete Sale: one Bills row for the whole order, one Sells row
    /// per line, and each sold good's Quantity decremented — all via the
    /// existing Core.Data layer, no new SQL added here.
    ///
    /// Every completed sale settles in full UNLESS Pay Later is selected
    /// (Phase 5): Cash and Card are still both immediately-settled tender
    /// types (no payment gateway — staff key in whatever the card reader
    /// showed as a logged payment), but a sale can now optionally be linked
    /// to a customer via the picker either way. Linking a customer under
    /// Cash/Card just records who the sale was for (Paid grows on their
    /// running total, Remain doesn't); linking under Pay Later is the
    /// actual "buy now, pay later" flow — Remain grows by the full total
    /// instead, and gets paid down later from the Customers screen.
    /// </summary>
    public class CheckoutViewModel : ViewModelBase
    {
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();
        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();

        private List<GoodsR> _allGoods = new List<GoodsR>();

        public ObservableCollection<GoodsR> FilteredGoods { get; } = new ObservableCollection<GoodsR>();
        public ObservableCollection<CategoryChip> Categories { get; } = new ObservableCollection<CategoryChip>();
        public ObservableCollection<CartLine> CartLines { get; } = new ObservableCollection<CartLine>();
        public ObservableCollection<CustomerOption> Customers { get; } = new ObservableCollection<CustomerOption>();

        private CategoryChip _selectedCategory;
        public CategoryChip SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value)) ApplyFilter();
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value)) ApplyFilter();
            }
        }

        private CustomerOption _selectedCustomer;
        public CustomerOption SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (!SetProperty(ref _selectedCustomer, value)) return;
                OnPropertyChanged(nameof(CanPayLater));

                // Walk-in (Model == null) can't carry a tab — fall back to
                // Cash automatically rather than leaving Pay Later selected
                // with nothing to attach it to.
                if (!CanPayLater && SelectedPaymentMethod == PaymentMethod.PayLater)
                    SelectedPaymentMethod = PaymentMethod.Cash;
            }
        }

        public bool CanPayLater => SelectedCustomer?.Model != null;

        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
        public PaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (SetProperty(ref _selectedPaymentMethod, value))
                {
                    OnPropertyChanged(nameof(IsCashSelected));
                    OnPropertyChanged(nameof(IsCardSelected));
                    OnPropertyChanged(nameof(IsPayLaterSelected));
                }
            }
        }

        public bool IsCashSelected => SelectedPaymentMethod == PaymentMethod.Cash;
        public bool IsCardSelected => SelectedPaymentMethod == PaymentMethod.Card;
        public bool IsPayLaterSelected => SelectedPaymentMethod == PaymentMethod.PayLater;

        public double Subtotal => CartLines.Sum(l => l.LineTotal);

        // Tax/discount are Settings-driven, per the README's note that tax
        // rate gets added there when the client needs it — not invented here.
        public double Total => Subtotal;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SetPaymentMethodCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand IncrementLineCommand { get; }
        public ICommand DecrementLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand CompleteSaleCommand { get; }

        public CheckoutViewModel()
        {
            SetPaymentMethodCommand = new RelayCommand(p =>
            {
                if (p is PaymentMethod method && (method != PaymentMethod.PayLater || CanPayLater))
                    SelectedPaymentMethod = method;
            });
            AddToCartCommand = new RelayCommand(p => AddToCart(p as GoodsR));
            IncrementLineCommand = new RelayCommand(p =>
            {
                if (p is CartLine line) line.Quantity += 1;
            });
            DecrementLineCommand = new RelayCommand(p =>
            {
                if (!(p is CartLine line)) return;
                if (line.Quantity <= 1) CartLines.Remove(line);
                else line.Quantity -= 1;
                RaiseTotals();
            });
            RemoveLineCommand = new RelayCommand(p =>
            {
                if (p is CartLine line) CartLines.Remove(line);
                RaiseTotals();
            });
            CompleteSaleCommand = new RelayCommand(_ => CompleteSale());

            CartLines.CollectionChanged += (s, e) => RaiseTotals();
            LocalizationManager.LanguageChanged += _ =>
            {
                RebuildCategoryChips();
                RebuildCustomerOptions();
            };
            CustomerDataEvents.CustomersChanged += RebuildCustomerOptions;

            LoadGoods();
            RebuildCustomerOptions();
        }

        private void RaiseTotals()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Total));
        }

        private void LoadGoods()
        {
            _allGoods = _goodsData.ReadAllGoodsRPic("goods");
            RebuildCategoryChips();
            ApplyFilter();
        }

        private void RebuildCategoryChips()
        {
            string previousValue = SelectedCategory?.Value;

            Categories.Clear();
            Categories.Add(new CategoryChip
            {
                DisplayName = LocalizationManager.GetString("CheckoutAllCategory"),
                Value = null
            });
            foreach (var category in _allGoods
                         .Select(g => g.Category)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct()
                         .OrderBy(c => c))
            {
                Categories.Add(new CategoryChip { DisplayName = category, Value = category });
            }

            _selectedCategory = Categories.FirstOrDefault(c => c.Value == previousValue) ?? Categories[0];
            OnPropertyChanged(nameof(SelectedCategory));
        }

        private void RebuildCustomerOptions()
        {
            int? previousId = SelectedCustomer?.Model?.Id;

            Customers.Clear();
            Customers.Add(new CustomerOption
            {
                DisplayName = LocalizationManager.GetString("CheckoutWalkIn"),
                Model = null
            });
            foreach (var customer in _customersData.ReadCustomers("customers").OrderBy(c => c.Ownername))
            {
                Customers.Add(new CustomerOption { DisplayName = customer.Ownername, Model = customer });
            }

            _selectedCustomer = previousId.HasValue
                ? Customers.FirstOrDefault(c => c.Model?.Id == previousId.Value) ?? Customers[0]
                : Customers[0];
            OnPropertyChanged(nameof(SelectedCustomer));
            OnPropertyChanged(nameof(CanPayLater));
        }

        private void ApplyFilter()
        {
            IEnumerable<GoodsR> query = _allGoods;

            if (SelectedCategory?.Value != null)
                query = query.Where(g => g.Category == SelectedCategory.Value);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(g =>
                    g.Name != null && g.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            FilteredGoods.Clear();
            foreach (var good in query) FilteredGoods.Add(good);
        }

        private void AddToCart(GoodsR good)
        {
            if (good == null || good.Quantity <= 0) return;

            var existing = CartLines.FirstOrDefault(l => l.GoodId == good.Id);
            if (existing != null)
            {
                if (existing.Quantity < existing.MaxAvailable) existing.Quantity += 1;
            }
            else
            {
                CartLines.Add(new CartLine(good, 1));
            }
            RaiseTotals();
        }

        private void CompleteSale()
        {
            if (CartLines.Count == 0) return;

            if (SelectedPaymentMethod == PaymentMethod.PayLater && !CanPayLater)
            {
                // Guard only — the Pay Later button is disabled in this
                // state (see CheckoutView.xaml IsEnabled binding), this just
                // prevents a bad write if it's ever reached another way.
                StatusMessage = LocalizationManager.GetString("CheckoutPayLaterRequiresCustomer");
                return;
            }

            try
            {
                DateTime now = DateTime.Now;
                string time = now.ToString("HH:mm");
                string date = now.ToString("dd/MM/yyyy");

                // Bills.InsertBills requires an explicit ID (not just
                // Billnumber) — there's no dedicated "next ID" helper in
                // Core, so compute both from the existing table rather than
                // adding new SQL to a file Mahmoud might also be touching.
                // DBNull-guarded: Convert.ToInt32 throws on a NULL cell
                // ("Object cannot be cast from DBNull to other types"), and
                // a bill row missing ID/Billnumber shouldn't block every
                // future sale from saving — treat it as 0 and move on.
                DataTable billsTable = _billsData.ReadAdapter("bills");
                int nextId = 1;
                int nextBillNumber = 1000;
                foreach (DataRow row in billsTable.Rows)
                {
                    int rowId = SafeInt(row["ID"]);
                    int rowBillNumber = SafeInt(row["Billnumber"]);
                    if (rowId >= nextId) nextId = rowId + 1;
                    if (rowBillNumber >= nextBillNumber) nextBillNumber = rowBillNumber + 1;
                }

                double totalCost = Total;
                double totalEarned = CartLines.Sum(l => (l.Price - l.Cost) * l.Quantity);

                var linkedCustomer = SelectedCustomer?.Model;
                bool isPayLater = SelectedPaymentMethod == PaymentMethod.PayLater;

                string ownername = linkedCustomer?.Ownername ?? "";
                string ownerid = linkedCustomer?.Ownerid ?? "";
                string ownernumber = linkedCustomer?.Ownernumber ?? "";

                // Cash/Card settle the bill in full immediately (Paid =
                // total, Remain = 0) — exactly what this screen always did,
                // whether or not a customer happens to be linked. Pay Later
                // is the one case that leaves a balance: nothing collected
                // now, the whole total goes on the linked customer's tab.
                double billPaid = isPayLater ? 0 : totalCost;
                double billRemain = isPayLater ? totalCost : 0;
                string paymentTag = SelectedPaymentMethod == PaymentMethod.Cash ? "Cash"
                                   : SelectedPaymentMethod == PaymentMethod.Card ? "Card"
                                   : "Credit";

                _billsData.InsertBills(
                    "bills", nextId, nextBillNumber, totalCost, time, date,
                    ownername, ownerid, ownernumber,
                    billPaid, billRemain, totalEarned, 0, 0, paymentTag);

                foreach (var line in CartLines)
                {
                    _sellsData.InsertSells(
                        "sells", line.Name, line.Category, line.Quantity, line.Cost, line.Price,
                        line.Type, time, date, line.Barcode, nextBillNumber,
                        (line.Price - line.Cost) * line.Quantity, "No", "");

                    double newQuantity = line.MaxAvailable - line.Quantity;
                    _goodsData.UpdateGoodCount("goods", line.Barcode, newQuantity);
                }

                // Linked customer's running totals: Paid grows by whatever
                // was actually collected now, Remain grows by whatever
                // wasn't (0 for Cash/Card, the full total for Pay Later).
                // See CustomersViewModel.RecordPayment for how a later
                // payment brings Remain back down.
                if (linkedCustomer != null)
                {
                    double newCustomerPaid = linkedCustomer.Paid + billPaid;
                    double newCustomerRemain = linkedCustomer.Remain + billRemain;
                    _customersData.UpdateCustomers(
                        "customers", linkedCustomer.Id, linkedCustomer.Ownername,
                        linkedCustomer.Ownerid, linkedCustomer.Ownernumber,
                        newCustomerPaid, newCustomerRemain);
                }

                StatusMessage = isPayLater
                    ? string.Format(LocalizationManager.GetString("CheckoutSaleSuccessCredit"), nextBillNumber, ownername)
                    : string.Format(LocalizationManager.GetString("CheckoutSaleSuccess"), nextBillNumber);

                CartLines.Clear();
                LoadGoods();

                // Phase 6: every completed sale (not just customer-linked
                // ones) needs to refresh Dashboard's KPIs/charts.
                OrderEvents.RaiseOrderCompleted();

                if (linkedCustomer != null)
                {
                    CustomerDataEvents.RaiseCustomersChanged();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CheckoutSaleError") + " (" + ex.Message + ")";
            }
        }

        // DataRow returns DBNull.Value (not C# null) for a null cell —
        // Convert.ToInt32(DBNull.Value) throws "Object cannot be cast from
        // DBNull to other types", which is exactly what surfaced here.
        private static int SafeInt(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }
}
