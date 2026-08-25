using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    /// adjustment per row, plus an Add Product form.
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
    ///
    /// Categories (rewritten 2026-08-25 — see below for the prior
    /// implicit-category design this replaced): categories are now a real,
    /// independent list (Core.Data.Categories against the `categories`
    /// table DatabaseBootstrapper creates/migrates), not just whatever
    /// distinct strings happen to be on goods.Category. AllCategoryNames is
    /// that list, refreshed by LoadCategories(). Both the Add Product form
    /// and the per-row Edit form bind their category field's ItemsSource to
    /// it as a plain, non-editable, selection-only ComboBox (WPF's built-in
    /// IsTextSearchEnabled gives type-ahead-jump "search" for free on a
    /// closed ComboBox — no custom filtering code needed) — a product can
    /// only ever be saved with a category actually selected from that list,
    /// which is what makes "the category written must be found" true by
    /// construction rather than by a validation check that could drift out
    /// of sync with it. AddCategoryCommand/DeleteCategoryCommand are the
    /// only way a category is created or removed now; adding a product no
    /// longer implicitly creates one.
    ///
    /// Superseded by the above (kept only as history, since a later reader
    /// hitting old comments here or in Dashboard-Parity-Plan.md/git history
    /// referencing "typing a new category on Add Product creates it"
    /// should know that's no longer true): this screen originally had no
    /// separate categories table at all — every category was implicit,
    /// derived from distinct goods.Category values, and typing an unknown
    /// name into Add Product's category field silently created it. Mahmoud
    /// asked for that removed in favor of an explicit, findable-only list
    /// with its own Add/Delete actions — this rewrite is that.
    ///
    /// Barcode is optional on a product but must be unique when present —
    /// enforced both at the DB level (DatabaseBootstrapper's partial unique
    /// index on goods.Barcode) and here (Goods.BarcodeExists checked before
    /// insert, Goods.BarcodeExistsExcludingId before an edit-save, for a
    /// clean error message instead of a raw SQLite exception).
    /// Cost and starting Quantity are required at creation, not optional —
    /// deliberate: defaulting Cost to 0 would silently corrupt every future
    /// profit number for that product (Dashboard's Profit = Price − Cost
    /// per sale) with no way to notice or fix it later. Confirmed with the
    /// client rather than guessed.
    ///
    /// Edit Product (added 2026-08-25): per-card inline edit, toggled by
    /// InventoryRow.IsEditing — Name/Category/Cost/Price/Barcode are
    /// editable; Quantity deliberately is not (the existing Adjust-quantity
    /// mini-form already owns that field end-to-end; see InventoryRow's
    /// class comment for why conflating the two is a real risk, not just
    /// redundancy). Saves through Goods.UpdateGoodsById (ID-keyed, unlike
    /// the legacy Barcode-keyed UpdateGoods — see that method's comment).
    /// </summary>
    public class InventoryViewModel : ViewModelBase
    {
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();
        private readonly Core.Data.Categories _categoriesData = new Core.Data.Categories();

        private List<InventoryRow> _allRows = new List<InventoryRow>();
        public ObservableCollection<InventoryRow> Rows { get; } = new ObservableCollection<InventoryRow>();
        public ObservableCollection<CategoryChip> Categories { get; } = new ObservableCollection<CategoryChip>();
        public ObservableCollection<CategoryChip> StockFilters { get; } = new ObservableCollection<CategoryChip>();

        // The one real list of known category names — see class doc
        // comment. Backs three different pickers: Add Product's category
        // ComboBox, each row's Edit-mode category ComboBox, and the
        // Delete-category ComboBox below.
        public ObservableCollection<string> AllCategoryNames { get; } = new ObservableCollection<string>();

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

        private string _newProductName = "";
        public string NewProductName
        {
            get => _newProductName;
            set => SetProperty(ref _newProductName, value);
        }

        private string _newProductBarcode = "";
        public string NewProductBarcode
        {
            get => _newProductBarcode;
            set => SetProperty(ref _newProductBarcode, value);
        }

        // Selection-only now (SelectedItem of a non-editable ComboBox,
        // ItemsSource=AllCategoryNames) — see class doc comment. No longer
        // drives a live-filtered suggestion list on every keystroke the way
        // it did when the field was free-typed, so there's nothing extra to
        // do in this setter beyond the plain SetProperty every other field
        // here already uses.
        private string _newProductCategoryInput = "";
        public string NewProductCategoryInput
        {
            get => _newProductCategoryInput;
            set => SetProperty(ref _newProductCategoryInput, value);
        }

        private string _newProductQuantity = "";
        public string NewProductQuantity
        {
            get => _newProductQuantity;
            set => SetProperty(ref _newProductQuantity, value);
        }

        private string _newProductCost = "";
        public string NewProductCost
        {
            get => _newProductCost;
            set => SetProperty(ref _newProductCost, value);
        }

        private string _newProductPrice = "";
        public string NewProductPrice
        {
            get => _newProductPrice;
            set => SetProperty(ref _newProductPrice, value);
        }

        // Category management (added 2026-08-25) — see class doc comment.
        private string _newCategoryName = "";
        public string NewCategoryName
        {
            get => _newCategoryName;
            set => SetProperty(ref _newCategoryName, value);
        }

        private string _categoryToDelete;
        public string CategoryToDelete
        {
            get => _categoryToDelete;
            set => SetProperty(ref _categoryToDelete, value);
        }

        public ICommand AdjustQuantityCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand AddCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand StartEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SaveEditCommand { get; }

        public InventoryViewModel()
        {
            AdjustQuantityCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) AdjustQuantity(row);
            });
            AddProductCommand = new RelayCommand(_ => AddProduct());
            AddCategoryCommand = new RelayCommand(_ => AddCategory());
            DeleteCategoryCommand = new RelayCommand(_ => DeleteCategory());
            StartEditCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) StartEdit(row);
            });
            CancelEditCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) row.IsEditing = false;
            });
            SaveEditCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) SaveEdit(row);
            });

            InventoryDataEvents.GoodsChanged += LoadGoods;
            LocalizationManager.LanguageChanged += _ => RebuildStockFilterChips();

            LoadCategories();
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

        // Reads the real category list from the `categories` table — see
        // class doc comment for why this replaced deriving suggestions
        // from goods.Category. Called on construction and after every
        // Add/Delete-category action; NOT called from LoadGoods, since
        // editing or adding a product can no longer introduce a category
        // that isn't already in this list (both forms are selection-only).
        private void LoadCategories()
        {
            string previousSelection = CategoryToDelete;

            AllCategoryNames.Clear();
            foreach (var name in _categoriesData.ReadAllCategoryNames())
                AllCategoryNames.Add(name);

            CategoryToDelete = AllCategoryNames.Contains(previousSelection) ? previousSelection : null;
        }

        // Filter chips (the horizontal row above the product grid) still
        // derive from what's actually ON a product, deliberately distinct
        // from AllCategoryNames above — a category with zero products right
        // now is a real, selectable category for Add Product, but isn't
        // worth its own "browse by this" chip until something's actually in
        // it.
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
                // ID-based, not Barcode-based — see the class-level doc
                // comment. row.Barcode may be "" (no barcode), and Barcode
                // is no longer guaranteed unique in that case.
                _goodsData.UpdateGoodCountById("goods", row.Id, newQuantity);
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

        private void AddProduct()
        {
            string name = NewProductName?.Trim() ?? "";
            string category = NewProductCategoryInput?.Trim() ?? "";
            string barcode = NewProductBarcode?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddMissingName");
                return;
            }

            // Selection-only field now (see class doc comment) — this can
            // really only fail when nothing's been picked at all, but kept
            // as an explicit check rather than assumed, since NewProductCategoryInput
            // is a plain string property a future change could still set
            // some other way.
            if (string.IsNullOrWhiteSpace(category) || !AllCategoryNames.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddMissingCategory");
                return;
            }

            if (!double.TryParse(NewProductQuantity, out double quantity) || quantity < 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddInvalidQuantity");
                return;
            }

            if (!double.TryParse(NewProductCost, out double cost) || cost < 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddInvalidCost");
                return;
            }

            if (!double.TryParse(NewProductPrice, out double price) || price <= 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddInvalidPrice");
                return;
            }

            // Barcode optional; when present, must be unique. "" (no
            // barcode) is deliberately never checked here — the partial
            // unique index only constrains non-empty values, so any number
            // of barcode-less products is fine.
            if (!string.IsNullOrEmpty(barcode) && _goodsData.BarcodeExists("goods", barcode))
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddDuplicateBarcode");
                return;
            }

            try
            {
                string today = DateTime.Now.ToString("dd/MM/yyyy");

                _goodsData.InsertGoods(
                    "goods",
                    name,
                    category,
                    quantity,
                    cost,
                    price,
                    "",       // Type — legacy field, unused by any current screen; no established meaning to fill in
                    barcode,  // "" when omitted
                    0,        // Earned — no sales yet
                    today,
                    today);

                NewProductName = "";
                NewProductBarcode = "";
                NewProductCategoryInput = "";
                NewProductQuantity = "";
                NewProductCost = "";
                NewProductPrice = "";

                StatusMessage = string.Format(LocalizationManager.GetString("InventoryAddSuccess"), name);

                LoadGoods();
                InventoryDataEvents.RaiseGoodsChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddError") + " (" + ex.Message + ")";
            }
        }

        // Category management (added 2026-08-25) — see class doc comment.

        private void AddCategory()
        {
            string name = NewCategoryName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = LocalizationManager.GetString("InventoryCategoryAddMissingName");
                return;
            }

            if (_categoriesData.CategoryExists(name))
            {
                StatusMessage = LocalizationManager.GetString("InventoryCategoryAddDuplicate");
                return;
            }

            try
            {
                _categoriesData.InsertCategoryName(name);
                NewCategoryName = "";
                StatusMessage = string.Format(LocalizationManager.GetString("InventoryCategoryAddSuccess"), name);
                LoadCategories();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddError") + " (" + ex.Message + ")";
            }
        }

        private void DeleteCategory()
        {
            string name = CategoryToDelete;

            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = LocalizationManager.GetString("InventoryCategoryDeleteMissingSelection");
                return;
            }

            // Refuse rather than silently orphan every product still in
            // this category — see Core.Data.Categories.DeleteCategoryByName's
            // comment. The only way to clear this block right now is
            // editing each of those products to a different category first
            // (Edit Product, this same session's other addition).
            int inUseCount = _goodsData.CountByCategory("goods", name);
            if (inUseCount > 0)
            {
                StatusMessage = string.Format(LocalizationManager.GetString("InventoryCategoryDeleteInUse"), name, inUseCount);
                return;
            }

            try
            {
                _categoriesData.DeleteCategoryByName(name);
                StatusMessage = string.Format(LocalizationManager.GetString("InventoryCategoryDeleteSuccess"), name);
                CategoryToDelete = null;
                LoadCategories();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddError") + " (" + ex.Message + ")";
            }
        }

        // Edit Product (added 2026-08-25) — see class doc comment.

        private void StartEdit(InventoryRow row)
        {
            row.EditName = row.Name;
            row.EditBarcode = row.Barcode;
            row.EditCategoryInput = row.Category;
            row.EditCost = row.Cost.ToString(CultureInfo.InvariantCulture);
            row.EditPrice = row.Price.ToString(CultureInfo.InvariantCulture);
            row.IsEditing = true;
        }

        private void SaveEdit(InventoryRow row)
        {
            string name = row.EditName?.Trim() ?? "";
            string category = row.EditCategoryInput?.Trim() ?? "";
            string barcode = row.EditBarcode?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditMissingName");
                return;
            }

            // Selection-only field (see InventoryRow.EditCategoryInput's
            // comment) — still checked, same reasoning as AddProduct above.
            if (string.IsNullOrWhiteSpace(category) || !AllCategoryNames.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditMissingCategory");
                return;
            }

            if (!double.TryParse(row.EditCost, out double cost) || cost < 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditInvalidCost");
                return;
            }

            if (!double.TryParse(row.EditPrice, out double price) || price <= 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditInvalidPrice");
                return;
            }

            if (!string.IsNullOrEmpty(barcode) && _goodsData.BarcodeExistsExcludingId("goods", barcode, row.Id))
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditDuplicateBarcode");
                return;
            }

            try
            {
                _goodsData.UpdateGoodsById("goods", row.Id, name, category, cost, price, barcode);

                row.Name = name;
                row.Category = category;
                row.Cost = cost;
                row.Price = price;
                row.Barcode = barcode;
                row.IsEditing = false;

                StatusMessage = string.Format(LocalizationManager.GetString("InventoryEditSuccess"), name);

                // Category and/or Name may have changed, and the filter
                // chips / search results are built from _allRows'
                // snapshot of those same fields — a full reload keeps them
                // correct rather than trying to patch RebuildCategoryChips
                // and ApplyFilter's in-memory query by hand.
                RebuildCategoryChips();
                ApplyFilter();

                InventoryDataEvents.RaiseGoodsChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditError") + " (" + ex.Message + ")";
            }
        }
    }
}
