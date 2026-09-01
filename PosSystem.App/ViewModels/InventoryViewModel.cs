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
        // Admin gate (#7, 2026-08-27 batch, item #7's extension beyond just
        // Dashboard) -- Mahmoud confirmed the admin password should also
        // cover add/edit/delete product and delete category, not just
        // viewing Dashboard. Shares AdminSession with Dashboard (and the
        // upcoming Excel export) -- see that class's doc comment for why
        // unlocking once now covers every gated screen for the rest of the
        // session, not a separate password prompt per screen. Browsing,
        // searching, filtering, and the existing inline quantity-adjust (a
        // routine restock-count task, not a structural change) stay open to
        // any staff member -- only the actions Mahmoud actually named are
        // gated: AddProduct, StartEdit (which blocks reaching SaveEdit),
        // DeleteProduct, AddCategory, DeleteCategory.
        // Admin gate (#7, 2026-08-27 batch, item #7's extension beyond just
        // Dashboard) -- reworked (per Mahmoud's explicit request) so this
        // screen's unlock is independent and temporary, same reasoning as
        // DashboardViewModel.IsUnlocked's doc comment: unlocking Inventory
        // does NOT unlock Dashboard, Bills, or Settings' gated sections,
        // and leaving this screen re-locks it -- LockAdmin() below is
        // called from InventoryView's Unloaded event. Browsing, searching,
        // filtering, and the existing inline quantity-adjust (a routine
        // restock-count task, not a structural change) stay open to any
        // staff member -- only the actions Mahmoud actually named are
        // gated: AddProduct, StartEdit (which blocks reaching SaveEdit),
        // DeleteProduct, AddCategory, DeleteCategory.
        private bool _isUnlockedThisVisit;
        // GateInventoryEnabled (Settings' new Access Control section, added
        // per Mahmoud's request) -- lets this screen's admin actions stay
        // open even with a password set elsewhere, if the owner turns
        // Inventory's own switch off.
        public bool IsAdminUnlocked => !AppSettings.HasAdminPassword || !AppSettings.GateInventoryEnabled || _isUnlockedThisVisit;
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

        /// <summary>
        /// Re-locks this screen -- called from InventoryView's Unloaded
        /// event when the sidebar selection moves away from Inventory, so
        /// coming back later requires the password again. Same pattern as
        /// DashboardViewModel.LockAdmin.
        /// </summary>
        public void LockAdmin()
        {
            if (!_isUnlockedThisVisit) return;
            _isUnlockedThisVisit = false;
            AdminUnlockPasswordInput = "";
            AdminUnlockError = "";
            OnPropertyChanged(nameof(IsAdminUnlocked));
            OnPropertyChanged(nameof(IsAdminLocked));
        }

        /// <summary>
        /// Shared guard for every gated action below -- sets the usual
        /// StatusMessage (same field every other validation failure in this
        /// class already uses) and returns false so the caller can bail out
        /// exactly like a validation failure, rather than a separate
        /// error-reporting path just for this one check.
        /// </summary>
        private bool RequireAdminUnlocked()
        {
            if (IsAdminUnlocked) return true;
            StatusMessage = LocalizationManager.GetString("InventoryAdminRequired");
            return false;
        }

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

        // Type-to-search variants of the Add Product category picker and
        // the Delete-category picker (2026-08-26) — same live-narrowing
        // pattern as CategorySearchText/FilteredCategoryOptions above,
        // ported to these two plain-string pickers instead of CategoryChip.
        // AllCategoryNames stays the untouched master list; these two
        // Filtered* collections are what each ComboBox actually shows,
        // narrowed as the user types into that picker's own search text.
        // Kept as two independent sets of state (not shared with the
        // filter picker's, and not with each other) since all three
        // ComboBoxes can be mid-edit independently.
        public ObservableCollection<string> FilteredNewProductCategoryOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> FilteredCategoryToDeleteOptions { get; } = new ObservableCollection<string>();

        private bool _suppressNewProductCategoryFilter;
        private bool _suppressCategoryToDeleteFilter;

        private string _newProductCategorySearchText = "";
        public string NewProductCategorySearchText
        {
            get => _newProductCategorySearchText;
            set
            {
                if (!SetProperty(ref _newProductCategorySearchText, value)) return;
                if (_suppressNewProductCategoryFilter) return;
                RebuildFilteredNewProductCategories();
                IsNewProductCategoryDropDownOpen = true;
            }
        }

        private bool _isNewProductCategoryDropDownOpen;
        public bool IsNewProductCategoryDropDownOpen
        {
            get => _isNewProductCategoryDropDownOpen;
            set => SetProperty(ref _isNewProductCategoryDropDownOpen, value);
        }

        private string _categoryToDeleteSearchText = "";
        public string CategoryToDeleteSearchText
        {
            get => _categoryToDeleteSearchText;
            set
            {
                if (!SetProperty(ref _categoryToDeleteSearchText, value)) return;
                if (_suppressCategoryToDeleteFilter) return;
                RebuildFilteredCategoryToDeleteOptions();
                IsCategoryToDeleteDropDownOpen = true;
            }
        }

        private bool _isCategoryToDeleteDropDownOpen;
        public bool IsCategoryToDeleteDropDownOpen
        {
            get => _isCategoryToDeleteDropDownOpen;
            set => SetProperty(ref _isCategoryToDeleteDropDownOpen, value);
        }

        // Type-to-search category dropdown (2026-08-26) — same design as
        // CheckoutViewModel's own FilteredCategoryOptions/CategorySearchText;
        // see that class's doc comment on FilteredCategoryOptions for the
        // full reasoning. Categories above stays the full master list
        // (unchanged, still built by RebuildCategoryChips from _allRows'
        // distinct categories); FilteredCategoryOptions is what the ComboBox
        // actually shows, narrowed by CategorySearchText as the user types.
        public ObservableCollection<CategoryChip> FilteredCategoryOptions { get; } = new ObservableCollection<CategoryChip>();

        private bool _suppressCategoryFilter;

        private string _categorySearchText = "";
        public string CategorySearchText
        {
            get => _categorySearchText;
            set
            {
                if (!SetProperty(ref _categorySearchText, value)) return;
                if (_suppressCategoryFilter) return;
                RebuildFilteredCategories();
                IsCategoryDropDownOpen = true;
            }
        }

        private bool _isCategoryDropDownOpen;
        public bool IsCategoryDropDownOpen
        {
            get => _isCategoryDropDownOpen;
            set => SetProperty(ref _isCategoryDropDownOpen, value);
        }

        private CategoryChip _selectedCategory;
        public CategoryChip SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetProperty(ref _selectedCategory, value)) return;
                ApplyFilter();

                _suppressCategoryFilter = true;
                CategorySearchText = value?.DisplayName ?? "";
                _suppressCategoryFilter = false;
            }
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

        // Editable again as of 2026-08-26 (SelectedItem of an IsEditable
        // ComboBox whose ItemsSource is FilteredNewProductCategoryOptions,
        // narrowed live by NewProductCategorySearchText) — see class doc
        // comment on AllCategoryNames. Still selection-only where it
        // matters: AddProduct() below still rejects anything not actually
        // present in AllCategoryNames, so typing narrows the list but can't
        // silently create a new category the way the pre-2026-08-25 design
        // did. Setting this (i.e. picking an item) syncs the search text to
        // match, same two-way sync SelectedCategory/CategorySearchText use
        // above.
        private string _newProductCategoryInput = "";
        public string NewProductCategoryInput
        {
            get => _newProductCategoryInput;
            set
            {
                if (!SetProperty(ref _newProductCategoryInput, value)) return;
                _suppressNewProductCategoryFilter = true;
                NewProductCategorySearchText = value ?? "";
                _suppressNewProductCategoryFilter = false;
            }
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

        // Category management (added 2026-08-25) -- see class doc comment.
        private string _newCategoryName = "";
        public string NewCategoryName
        {
            get => _newCategoryName;
            set => SetProperty(ref _newCategoryName, value);
        }

        // Editable as of 2026-08-26, same live-narrowing treatment as
        // NewProductCategoryInput above (FilteredCategoryToDeleteOptions /
        // CategoryToDeleteSearchText). DeleteCategory() below already
        // treats a value not found in AllCategoryNames as "nothing
        // selected" via CategoryExists-style lookups, so an unmatched
        // typed string just can't be deleted rather than needing a new
        // guard here.
        private string _categoryToDelete;
        public string CategoryToDelete
        {
            get => _categoryToDelete;
            set
            {
                if (!SetProperty(ref _categoryToDelete, value)) return;
                _suppressCategoryToDeleteFilter = true;
                CategoryToDeleteSearchText = value ?? "";
                _suppressCategoryToDeleteFilter = false;
            }
        }

        public ICommand AdjustQuantityCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand AddCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand StartEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand DeleteProductCommand { get; }

        // Batch selection (added 2026-08-31) -- lets someone select several
        // products at once (checkbox per card, see InventoryRow.IsSelected)
        // and either delete all of them or move them all to a different
        // category in one action. Both bulk actions are admin-gated, same
        // reasoning as the existing single-product Edit/Delete.
        public int SelectedCount => _allRows.Count(r => r.IsSelected);
        public bool HasSelection => SelectedCount > 0;

        private string _bulkCategoryTarget;
        public string BulkCategoryTarget
        {
            get => _bulkCategoryTarget;
            set => SetProperty(ref _bulkCategoryTarget, value);
        }

        // Bound to a select-all checkbox above the grid. Reads/writes
        // against whatever's currently VISIBLE (Rows, post-filter) rather
        // than every product in _allRows.
        public bool IsAllVisibleSelected
        {
            get => Rows.Count > 0 && Rows.All(r => r.IsSelected);
            set { foreach (var row in Rows) row.IsSelected = value; }
        }

        public ICommand BulkDeleteCommand { get; }
        public ICommand BulkChangeCategoryCommand { get; }

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
            DeleteProductCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) DeleteProduct(row);
            });
            BulkDeleteCommand = new RelayCommand(_ => BulkDelete());
            BulkChangeCategoryCommand = new RelayCommand(_ => BulkChangeCategory());
            AdminUnlockCommand = new RelayCommand(_ => AdminUnlock());
            AppSettings.Changed += () =>
            {
                OnPropertyChanged(nameof(IsAdminUnlocked));
                OnPropertyChanged(nameof(IsAdminLocked));
            };

            InventoryDataEvents.GoodsChanged += LoadGoods;
            LocalizationManager.LanguageChanged += _ => RebuildStockFilterChips();

            // Settings screen (2026-08-26): a saved Low Stock Threshold
            // change needs every already-rendered card's badge to
            // re-evaluate IsLowStock/IsInStock against the new value, not
            // just the next product added. InventoryRow reads the live
            // AppSettings value already (see that class), but WPF only
            // repaints a binding when PropertyChanged actually fires for
            // that property -- LoadGoods() rebuilds every InventoryRow from
            // scratch, which does. No unsubscribe: same lifetime rule every
            // other cross-screen event subscription in this app already
            // follows (see CheckoutViewModel's InventoryDataEvents.GoodsChanged
            // comment) -- each screen's ViewModel lives for the app's
            // lifetime once MainViewModel caches its View.
            AppSettings.Changed += LoadGoods;

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

            // Batch selection -- every fresh InventoryRow starts unselected
            // by construction, but this ViewModel still needs to hear about
            // a checkbox toggling on any of them to keep SelectedCount/
            // HasSelection/IsAllVisibleSelected current. No unsubscribe: a
            // reload replaces _allRows wholesale, and the old rows (with
            // this subscription pointing outward at this still-alive VM)
            // simply fall out of scope and become collectible.
            foreach (var row in _allRows) row.PropertyChanged += Row_PropertyChanged;

            RebuildCategoryChips();
            RebuildStockFilterChips();
            ApplyFilter();
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsAllVisibleSelected));
        }

        private void Row_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(InventoryRow.IsSelected)) return;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsAllVisibleSelected));
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

            // Both live-narrowed lists are keyed off AllCategoryNames, so a
            // category being added/removed here has to re-run both filters
            // (against whatever's currently typed in each box) or they'd
            // keep showing a stale snapshot until the next keystroke.
            RebuildFilteredNewProductCategories();
            RebuildFilteredCategoryToDeleteOptions();
        }

        // See CategorySearchText/RebuildFilteredCategories' comments above
        // for the shared reasoning; these two are the same idea against a
        // plain List<string> instead of a List<CategoryChip>, and neither
        // needs an always-reachable "All" entry the way that one does.
        private void RebuildFilteredNewProductCategories()
        {
            FilteredNewProductCategoryOptions.Clear();
            string text = NewProductCategorySearchText ?? "";
            IEnumerable<string> source = string.IsNullOrWhiteSpace(text)
                ? AllCategoryNames
                : AllCategoryNames.Where(c => c.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (var name in source) FilteredNewProductCategoryOptions.Add(name);
        }

        private void RebuildFilteredCategoryToDeleteOptions()
        {
            FilteredCategoryToDeleteOptions.Clear();
            string text = CategoryToDeleteSearchText ?? "";
            IEnumerable<string> source = string.IsNullOrWhiteSpace(text)
                ? AllCategoryNames
                : AllCategoryNames.Where(c => c.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (var name in source) FilteredCategoryToDeleteOptions.Add(name);
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

            // See CheckoutViewModel.RebuildCategoryChips' matching comment —
            // this bypasses the SelectedCategory setter (direct field
            // assignment above), so the search-box sync has to happen here
            // too.
            _suppressCategoryFilter = true;
            CategorySearchText = _selectedCategory?.DisplayName ?? "";
            _suppressCategoryFilter = false;
            RebuildFilteredCategories();
        }

        // See CheckoutViewModel.RebuildFilteredCategories for the full
        // reasoning (same design, ported here for Inventory's own category
        // filter). "All" (Categories[0], Value == null) is always kept
        // reachable even when the typed text doesn't match its own label.
        private void RebuildFilteredCategories()
        {
            FilteredCategoryOptions.Clear();
            string text = CategorySearchText ?? "";
            CategoryChip allChip = Categories.Count > 0 ? Categories[0] : null;

            if (string.IsNullOrWhiteSpace(text))
            {
                foreach (var chip in Categories) FilteredCategoryOptions.Add(chip);
                return;
            }

            bool allIncluded = false;
            foreach (var chip in Categories)
            {
                if (chip.DisplayName != null &&
                    chip.DisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FilteredCategoryOptions.Add(chip);
                    if (chip == allChip) allIncluded = true;
                }
            }

            if (!allIncluded && allChip != null)
                FilteredCategoryOptions.Insert(0, allChip);
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
            if (!RequireAdminUnlocked()) return;

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
                RebuildFilteredNewProductCategories(); // reset the narrowed list back to full after the field clears
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
            if (!RequireAdminUnlocked()) return;

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
            if (!RequireAdminUnlocked()) return;

            string name = CategoryToDelete;

            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = LocalizationManager.GetString("InventoryCategoryDeleteMissingSelection");
                return;
            }

            // The picker became free-typed on 2026-08-26 (live-narrowed
            // ComboBox, see AllCategoryNames' doc comment) instead of
            // selection-only, so "looks like a category" no longer implies
            // "is one" the way a plain SelectedItem guaranteed. Same check
            // AddProduct/SaveEdit already run against AllCategoryNames for
            // their own now-editable category fields.
            if (!AllCategoryNames.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
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
                LoadCategories(); // also re-runs both Rebuild*Options against the now-updated AllCategoryNames
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddError") + " (" + ex.Message + ")";
            }
        }

        // Edit Product (added 2026-08-25) — see class doc comment.

        private void StartEdit(InventoryRow row)
        {
            // Gated here, not in SaveEdit -- blocking entry into edit mode
            // is a clearer signal than letting someone fill out the whole
            // form and only finding out it's locked on Save.
            if (!RequireAdminUnlocked()) return;

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

        // Delete Product (added 2026-08-25) -- same directness as
        // DeleteCategory above: no confirmation modal, since this app has
        // no modal-dialog system built anywhere yet (every destructive
        // action so far -- DeleteCategory, and before that RemoveGoods's
        // original callers -- has relied on the action being an explicit,
        // deliberate button click plus a status message afterward, not a
        // confirm-are-you-sure step). Unlike deleting a category, this has
        // no in-use guard to check first: see Goods.RemoveGoodsById's
        // comment for why deleting a product is safe with respect to sales
        // history (Sells.cs's `sells` table is a full snapshot per line
        // item, not a foreign key to goods.ID) -- there is no equivalent
        // still-referenced-elsewhere state to block on, the way
        // CountByCategory blocks an in-use category delete.
        private void DeleteProduct(InventoryRow row)
        {
            if (!RequireAdminUnlocked()) return;

            try
            {
                string name = row.Name;
                _goodsData.RemoveGoodsById("goods", row.Id);

                _allRows.Remove(row);
                Rows.Remove(row);

                StatusMessage = string.Format(LocalizationManager.GetString("InventoryDeleteSuccess"), name);

                // Category chips are built from _allRows' distinct
                // categories (RebuildCategoryChips' own comment) -- a
                // deleted product may have been the last one in its
                // category, so that chip needs to disappear too.
                RebuildCategoryChips();

                InventoryDataEvents.RaiseGoodsChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryDeleteError") + " (" + ex.Message + ")";
            }
        }

        // Batch selection: bulk delete (added 2026-08-31) -- same no-
        // confirmation-modal directness as the single-product Delete above.
        // Selection is read from _allRows (not just the filtered Rows) so a
        // selection made before the filter changed is still honored.
        private void BulkDelete()
        {
            if (!RequireAdminUnlocked()) return;

            var selected = _allRows.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryBulkNoSelection");
                return;
            }

            try
            {
                foreach (var row in selected)
                {
                    _goodsData.RemoveGoodsById("goods", row.Id);
                    _allRows.Remove(row);
                    Rows.Remove(row);
                }

                StatusMessage = string.Format(LocalizationManager.GetString("InventoryBulkDeleteSuccess"), selected.Count);

                RebuildCategoryChips();
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsAllVisibleSelected));

                InventoryDataEvents.RaiseGoodsChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryDeleteError") + " (" + ex.Message + ")";
            }
        }

        // Batch selection: bulk category change (added 2026-08-31) -- moves
        // every currently-selected row to BulkCategoryTarget. Goes through
        // Goods.UpdateGoodsById one row at a time -- there's no bulk-update
        // method on that class, and this app's product count is small
        // enough (per LoadGoods' own "281 rows is nothing" precedent) that
        // a loop of single-row updates costs nothing noticeable. Each call
        // carries the row's own existing Name/Cost/Price/Barcode forward
        // unchanged -- only Category actually changes.
        private void BulkChangeCategory()
        {
            if (!RequireAdminUnlocked()) return;

            var selected = _allRows.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryBulkNoSelection");
                return;
            }

            string category = BulkCategoryTarget?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(category) || !AllCategoryNames.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = LocalizationManager.GetString("InventoryBulkMissingCategory");
                return;
            }

            try
            {
                foreach (var row in selected)
                {
                    _goodsData.UpdateGoodsById("goods", row.Id, row.Name, category, row.Cost, row.Price, row.Barcode);
                    row.Category = category;
                    row.IsSelected = false;
                }

                StatusMessage = string.Format(LocalizationManager.GetString("InventoryBulkCategoryChangeSuccess"), selected.Count, category);

                BulkCategoryTarget = null;
                RebuildCategoryChips();
                ApplyFilter();
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsAllVisibleSelected));

                InventoryDataEvents.RaiseGoodsChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditError") + " (" + ex.Message + ")";
            }
        }
    }
}
