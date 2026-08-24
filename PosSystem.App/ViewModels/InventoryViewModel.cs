using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for InventoryView — Phase 7 (POS-development-plan.md).
    /// Loads the goods table once (same "load it all, filter in memory"
    /// approach Checkout/Customers already use — 281 rows is nothing) and
    /// exposes: search by name/barcode, filter by category (same chip
    /// pattern as Checkout) and by stock status, plus an inline quantity
    /// adjustment per row.
    ///
    /// Low-stock threshold: a plain constant (InventoryRow.LowStockThreshold
    /// = 10), not a stored setting. Deliberately not added as a schema
    /// column on Goods or a new Settings field — the plan doesn't specify a
    /// client-confirmed number, and Phase 4 already established the
    /// precedent of flagging an unconfirmed schema/config decision rather
    /// than adding one unasked (see Goods.IsAvailable in that phase). If the
    /// client wants this configurable per-product or shop-wide, that's a
    /// real follow-up, not a guess made now.
    ///
    /// Reloads on InventoryDataEvents.GoodsChanged — that's how a Checkout
    /// sale (which decrements Quantity) shows up here even when Inventory
    /// wasn't the active tab, same cross-screen-freshness reasoning as
    /// Customers/Checkout's CustomerDataEvents link. This ViewModel also
    /// raises that same event after its own writes, so Checkout's cached
    /// goods list (and cart MaxAvailable checks) stay current too.
    /// </summary>
    public class InventoryViewModel : ViewModelBase
    {
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();

        private List<InventoryRow> _allRows = new List<InventoryRow>();
        public ObservableCollection<InventoryRow> Rows { get; } = new ObservableCollection<InventoryRow>();
        public ObservableCollection<CategoryChip> Categories { get; } = new ObservableCollection<CategoryChip>();
        public ObservableCollection<CategoryChip> StockFilters { get; } = new ObservableCollection<CategoryChip>();

        private CategoryChip _selectedCategory;
        public CategoryChip SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) ApplyFilter(); }
        }

        private CategoryChip _selectedStockFilter;
        public CategoryChip SelectedStockFilter
        {
            get => _selectedStockFilter;
            set { if (SetProperty(ref _selectedStockFilter, value)) ApplyFilter(); }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand AdjustQuantityCommand { get; }

        public InventoryViewModel()
        {
            AdjustQuantityCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) AdjustQuantity(row);
            });

            InventoryDataEvents.GoodsChanged += LoadGoods;
            LocalizationManager.LanguageChanged += _ => RebuildStockFilterChips();

            LoadGoods();
        }

        private void LoadGoods()
        {
            // Sorted by quantity ascending at the source (ReadAllGoodsQuantity,
            // same as ReadAllGoodsPic just ordered differently) — lowest
            // stock first is the more useful default for an inventory
            // screen than alphabetical, and matches what a "what needs
            // restocking" glance actually wants.
            var models = _goodsData.ReadAllGoodsQuantity("goods");
            _allRows = models.Select(m => new InventoryRow(new Core.Models.GoodsR(
                m.Id, m.Name, m.Category, m.Quantity, m.Cost, m.Price, m.Type, m.Barcode, m.Earned, m.Datex, m.Datee))).ToList();

            RebuildCategoryChips();
            RebuildStockFilterChips();
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
            foreach (var category in _allRows
                         .Select(r => r.Category)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct()
                         .OrderBy(c => c))
            {
                Categories.Add(new CategoryChip { DisplayName = category, Value = category });
            }

            _selectedCategory = Categories.FirstOrDefault(c => c.Value == previousValue) ?? Categories[0];
            OnPropertyChanged(nameof(SelectedCategory));
        }

        private void RebuildStockFilterChips()
        {
            string previousValue = SelectedStockFilter?.Value;

            StockFilters.Clear();
            StockFilters.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("InventoryStockAll"), Value = null });
            StockFilters.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("InventoryStockLow"), Value = "low" });
            StockFilters.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("InventoryStockOut"), Value = "out" });

            _selectedStockFilter = StockFilters.FirstOrDefault(c => c.Value == previousValue) ?? StockFilters[0];
            OnPropertyChanged(nameof(SelectedStockFilter));
        }

        private void ApplyFilter()
        {
            IEnumerable<InventoryRow> query = _allRows;

            if (SelectedCategory?.Value != null)
                query = query.Where(r => r.Category == SelectedCategory.Value);

            if (SelectedStockFilter?.Value == "low")
                query = query.Where(r => r.IsLowStock);
            else if (SelectedStockFilter?.Value == "out")
                query = query.Where(r => r.IsOutOfStock);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(r =>
                    (r.Name != null && r.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.Barcode != null && r.Barcode.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0));

            Rows.Clear();
            foreach (var row in query) Rows.Add(row);
        }

        private void AdjustQuantity(InventoryRow row)
        {
            if (!double.TryParse(row.AdjustInput, out double newQuantity) || newQuantity < 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAdjustInvalid");
                return;
            }

            try
            {
                _goodsData.UpdateGoodCount("goods", row.Barcode, newQuantity);
                row.Quantity = newQuantity;
                row.AdjustInput = "";

                StatusMessage = string.Format(LocalizationManager.GetString("InventoryAdjustSuccess"), row.Name, newQuantity);

                InventoryDataEvents.RaiseGoodsChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAdjustError") + " (" + ex.Message + ")";
            }
        }
    }
}
