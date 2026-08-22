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
        Card
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
    /// Every completed sale is treated as paid in full. Cash and Card are
    /// both immediately-settled tender types per the business plan (no
    /// payment gateway — staff key in whatever the card reader showed as a
    /// logged payment). "Buy now, pay later" belongs to the Customers/Debt
    /// screen (Phase 5), not here — Ownername/Ownerid/Ownernumber are left
    /// blank (walk-in sale) until Checkout gets a "link to customer" step.
    /// </summary>
    public class CheckoutViewModel : ViewModelBase
    {
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();
        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();

        private List<GoodsR> _allGoods = new List<GoodsR>();

        public ObservableCollection<GoodsR> FilteredGoods { get; } = new ObservableCollection<GoodsR>();
        public ObservableCollection<CategoryChip> Categories { get; } = new ObservableCollection<CategoryChip>();
        public ObservableCollection<CartLine> CartLines { get; } = new ObservableCollection<CartLine>();

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
                }
            }
        }

        public bool IsCashSelected => SelectedPaymentMethod == PaymentMethod.Cash;
        public bool IsCardSelected => SelectedPaymentMethod == PaymentMethod.Card;

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
                if (p is PaymentMethod method) SelectedPaymentMethod = method;
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
            LocalizationManager.LanguageChanged += _ => RebuildCategoryChips();

            LoadGoods();
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

            try
            {
                DateTime now = DateTime.Now;
                string time = now.ToString("HH:mm");
                string date = now.ToString("dd/MM/yyyy");

                // Bills.InsertBills requires an explicit ID (not just
                // Billnumber) — there's no dedicated "next ID" helper in
                // Core, so compute both from the existing table rather than
                // adding new SQL to a file Mahmoud might also be touching.
                DataTable billsTable = _billsData.ReadAdapter("bills");
                int nextId = 1;
                int nextBillNumber = 1000;
                foreach (DataRow row in billsTable.Rows)
                {
                    int rowId = Convert.ToInt32(row["ID"]);
                    int rowBillNumber = Convert.ToInt32(row["Billnumber"]);
                    if (rowId >= nextId) nextId = rowId + 1;
                    if (rowBillNumber >= nextBillNumber) nextBillNumber = rowBillNumber + 1;
                }

                double totalCost = Total;
                double totalEarned = CartLines.Sum(l => (l.Price - l.Cost) * l.Quantity);
                string paymentTag = SelectedPaymentMethod == PaymentMethod.Cash ? "Cash" : "Card";

                _billsData.InsertBills(
                    "bills", nextId, nextBillNumber, totalCost, time, date,
                    "", "", "",
                    totalCost, 0, totalEarned, 0, 0, paymentTag);

                foreach (var line in CartLines)
                {
                    _sellsData.InsertSells(
                        "sells", line.Name, line.Category, line.Quantity, line.Cost, line.Price,
                        line.Type, time, date, line.Barcode, nextBillNumber,
                        (line.Price - line.Cost) * line.Quantity, "No", "");

                    double newQuantity = line.MaxAvailable - line.Quantity;
                    _goodsData.UpdateGoodCount("goods", line.Barcode, newQuantity);
                }

                StatusMessage = string.Format(LocalizationManager.GetString("CheckoutSaleSuccess"), nextBillNumber);

                CartLines.Clear();
                LoadGoods();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CheckoutSaleError") + " (" + ex.Message + ")";
            }
        }
    }
}
