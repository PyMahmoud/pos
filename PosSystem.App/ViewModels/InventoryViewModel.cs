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

        // Staged edits / Undo / Redo / Save Changes (2026-09-03, explicit
        // request) -- every mutating action on this screen (Add/Edit/
        // Delete/Adjust-quantity a product, bulk delete/re-category, Add/
        // Delete a category) used to write straight to the database the
        // moment its button was clicked. That's gone now: every action
        // below instead mutates _allRows/AllCategoryNames (the SAME
        // in-memory collections the UI already reads from -- nothing new
        // there) through PushChange, which records a paired Apply/Revert
        // closure on _undoStack so Undo/Redo can walk back and forth
        // through the pending session's history, and nothing touches the
        // database until SaveChanges() actually runs.
        //
        // Why a plain Apply/Revert closure pair per action rather than one
        // command class per action type (AddProductCommand,
        // DeleteProductCommand, ...): every action already computes
        // exactly what changed as local variables right where it validates
        // its input (e.g. SaveEdit already has oldName/newName in scope) --
        // wrapping that in a closure costs nothing extra there, while 8
        // separate command classes would mean 8 separate files' worth of
        // ceremony for what's fundamentally "mutate these fields, know how
        // to put them back."
        //
        // Why SaveChanges() diffs the final in-memory state against a
        // baseline snapshot instead of literally replaying each
        // action's own database write in order: replaying runs into a real
        // ordering problem the moment one staged action targets a row
        // another staged action created (e.g. Add a product, then Edit
        // that same not-yet-saved product's price, all before ever
        // clicking Save) -- the Edit would need to reference a database ID
        // that doesn't exist until the Add's own commit step runs first,
        // and Undo/Redo reordering the timeline makes tracking "what order
        // do these need to commit in" genuinely hard to get right. Diffing
        // sidesteps all of that: _allRows/AllCategoryNames are already the
        // single, correct, fully up-to-date picture of "what should be
        // true after Save" REGARDLESS of how many actions or undo/redo
        // cycles produced them, so Save only ever needs to ask "how does
        // this differ from what's actually in the database right now" once,
        // at the end. This does mean every validation check that used to
        // query the database directly (BarcodeExists, CategoryExists,
        // CountByCategory) had to move to checking _allRows/AllCategoryNames
        // instead -- the database is now stale relative to pending local
        // edits until Save runs, so a database-only check would both miss a
        // same-session duplicate and wrongly block a since-staged-deleted
        // value being reused. See each rewritten method below for its own
        // version of this.
        //
        // Undo/Redo does NOT survive a Save: Revert only ever mutates
        // in-memory state, never the database (that's the whole point of
        // the diff-based commit above) -- if undo history stayed alive
        // across a save boundary, undoing a change from before the save
        // would silently desync the UI from what's actually on disk again,
        // with no re-diff to catch it. SaveChanges() clears both stacks
        // once it's done.
        private sealed class PendingChange
        {
            public readonly Action Apply;
            public readonly Action Revert;
            public PendingChange(Action apply, Action revert) { Apply = apply; Revert = revert; }
        }

        private readonly List<PendingChange> _undoStack = new List<PendingChange>();
        private readonly List<PendingChange> _redoStack = new List<PendingChange>();

        // Baseline: a snapshot of exactly what's in the database as of the
        // last load or last successful Save -- SaveChanges() diffs the
        // CURRENT _allRows/AllCategoryNames against this to know what to
        // actually write. Re-captured at the end of the constructor (after
        // the very first LoadGoods/LoadCategories) and again right after
        // every successful SaveChanges().
        private struct GoodsBaselineSnapshot
        {
            public int Id;
            public string Name;
            public string Category;
            public double Quantity;
            public double Cost;
            public double Price;
            public string Barcode;
            public double DiscountPercent;
        }

        private List<GoodsBaselineSnapshot> _baselineRows = new List<GoodsBaselineSnapshot>();
        private HashSet<string> _baselineCategoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Temporary IDs for staged-but-not-yet-saved new products -- always
        // negative (real database IDs, INTEGER PRIMARY KEY AUTOINCREMENT,
        // are always positive), so "row.Id <= 0" reliably means "this
        // doesn't exist in the database yet" everywhere below and in
        // SaveChanges(). Monotonically decreasing per InventoryViewModel
        // instance -- this screen's ViewModel lives for the app's whole
        // lifetime once MainViewModel caches its View (same lifetime rule
        // as every event subscription elsewhere in this class), so a
        // simple never-reset counter can't collide with itself.
        private int _nextTempId;
        private int NextTempId() => --_nextTempId;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        // Drives both the Save/Discard buttons' visibility and
        // MainViewModel's "leaving Inventory with unsaved changes" guard
        // (SelectedNavItem's setter) -- reaching zero (via Undo, Discard,
        // or Save) means _allRows/AllCategoryNames are back to exactly
        // matching the database, so there is nothing left to warn about or
        // commit.
        public bool HasUnsavedChanges => _undoStack.Count > 0;

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand DiscardChangesCommand { get; }

        // Every staged action funnels through here. Applies immediately
        // (so the UI reflects it right away, same as the pre-staging
        // immediate-write behavior looked to the user), records it for
        // Undo, and -- same as every other user-initiated change on this
        // screen -- clears the redo stack: once a NEW action happens, the
        // timeline has branched, and whatever was available to redo no
        // longer has a consistent "future" to redo into.
        private void PushChange(Action apply, Action revert)
        {
            apply();
            _undoStack.Add(new PendingChange(apply, revert));
            _redoStack.Clear();
            RefreshDerivedCollections();
            RaiseUndoRedoState();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            var change = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            change.Revert();
            _redoStack.Add(change);
            RefreshDerivedCollections();
            RaiseUndoRedoState();
            StatusMessage = LocalizationManager.GetString("InventoryUndoStatus");
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            var change = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            change.Apply();
            _undoStack.Add(change);
            RefreshDerivedCollections();
            RaiseUndoRedoState();
            StatusMessage = LocalizationManager.GetString("InventoryRedoStatus");
        }

        // Undoes every pending action, oldest-effect-last (same order Undo()
        // itself already walks), then throws away the redo history too --
        // unlike a plain Undo-to-empty, Discard is a deliberate "throw all
        // of this away" action, so whatever was just discarded should not
        // be redo-able back in afterward.
        private void DiscardChanges()
        {
            if (_undoStack.Count == 0) return;
            while (_undoStack.Count > 0) Undo();
            _redoStack.Clear();
            RaiseUndoRedoState();
            StatusMessage = LocalizationManager.GetString("InventoryDiscardSuccess");
        }

        private void RaiseUndoRedoState()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // Every collection/derived-property that could be affected by ANY
        // staged action -- called uniformly after PushChange/Undo/Redo
        // rather than each call site picking and choosing which of these
        // its specific change actually touched. Slightly more work than
        // strictly necessary on, say, a quantity-only Adjust, but this
        // app's own "281 rows is nothing" precedent (LoadGoods' doc
        // comment) applies just as well here, and uniform beats
        // per-call-site bookkeeping bugs.
        private void RefreshDerivedCollections()
        {
            RebuildCategoryChips();
            ApplyFilter();
            RebuildFilteredNewProductCategories();
            RebuildFilteredCategoryToDeleteOptions();
            RebuildDiscountedRows();
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsAllVisibleSelected));
        }

        private void CaptureBaseline()
        {
            _baselineRows = _allRows.Select(r => new GoodsBaselineSnapshot
            {
                Id = r.Id,
                Name = r.Name,
                Category = r.Category,
                Quantity = r.Quantity,
                Cost = r.Cost,
                Price = r.Price,
                Barcode = r.Barcode,
                DiscountPercent = r.DiscountPercent
            }).ToList();
            _baselineCategoryNames = new HashSet<string>(AllCategoryNames, StringComparer.OrdinalIgnoreCase);
        }

        // Commits every pending change to the database in one pass, via a
        // diff against _baselineRows/_baselineCategoryNames -- see this
        // class's staging-model doc comment above for why a diff instead
        // of replaying each staged action's own write. Order: new rows
        // first (so they have a real ID before anything downstream could
        // need it), then deletes, then field/quantity edits on surviving
        // rows, then categories. Wrapped as one method-level try/catch
        // rather than per-row: a partial failure here would leave the
        // database and the in-memory baseline out of sync with no clean
        // way to know which rows actually made it, so this reports the
        // failure and leaves everything pending (nothing's cleared from
        // the undo stack) rather than guessing.
        private void SaveChanges()
        {
            if (!HasUnsavedChanges) return;

            try
            {
                foreach (var row in _allRows.Where(r => r.Id <= 0).ToList())
                {
                    string today = DateTime.Now.ToString("dd/MM/yyyy");
                    row.Id = _goodsData.InsertGoodsReturningId(
                        "goods", row.Name, row.Category, row.Quantity, row.Cost, row.Price,
                        "", row.Barcode, 0, today, today, row.DiscountPercent);
                }

                var currentIds = new HashSet<int>(_allRows.Where(r => r.Id > 0).Select(r => r.Id));
                foreach (var baseline in _baselineRows.Where(b => !currentIds.Contains(b.Id)))
                {
                    _goodsData.RemoveGoodsById("goods", baseline.Id);
                }

                var baselineById = _baselineRows.ToDictionary(b => b.Id);
                foreach (var row in _allRows.Where(r => r.Id > 0))
                {
                    if (!baselineById.TryGetValue(row.Id, out var baseline)) continue;

                    bool fieldsChanged = baseline.Name != row.Name || baseline.Category != row.Category ||
                                          baseline.Cost != row.Cost || baseline.Price != row.Price ||
                                          baseline.Barcode != row.Barcode || baseline.DiscountPercent != row.DiscountPercent;
                    if (fieldsChanged)
                        _goodsData.UpdateGoodsById("goods", row.Id, row.Name, row.Category, row.Cost, row.Price, row.Barcode, row.DiscountPercent);

                    if (baseline.Quantity != row.Quantity)
                        _goodsData.UpdateGoodCountById("goods", row.Id, row.Quantity);
                }

                var currentCategories = new HashSet<string>(AllCategoryNames, StringComparer.OrdinalIgnoreCase);
                foreach (var added in currentCategories.Except(_baselineCategoryNames, StringComparer.OrdinalIgnoreCase))
                    _categoriesData.InsertCategoryName(added);
                foreach (var removed in _baselineCategoryNames.Except(currentCategories, StringComparer.OrdinalIgnoreCase))
                    _categoriesData.DeleteCategoryByName(removed);

                _undoStack.Clear();
                _redoStack.Clear();

                // Reloads _allRows/AllCategoryNames fresh from the database
                // (this VM's own LoadGoods subscription runs synchronously
                // as part of this call) -- self-corrects against anything
                // subtle the diff above might have missed, and is also how
                // Checkout's cached goods list picks up these changes, same
                // as every other write on this screen already did before
                // staging existed. CaptureBaseline() below has to run AFTER
                // this, not before, so it snapshots the just-reloaded
                // canonical state rather than the pre-reload one.
                InventoryDataEvents.RaiseGoodsChanged();
                LoadCategories();

                CaptureBaseline();
                RaiseUndoRedoState();

                StatusMessage = LocalizationManager.GetString("InventorySaveChangesSuccess");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("InventorySaveChangesError") + " (" + ex.Message + ")";
            }
        }

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

        // Bulk discount (added 2026-09-04, alongside the Discounts page --
        // see class doc comment). Same selection (_allRows.Where(r =>
        // r.IsSelected)) and admin gate as BulkChangeCategory/BulkDelete
        // below; the only new piece is parsing/validating a percentage
        // instead of picking a category.
        private string _bulkDiscountPercentInput = "";
        public string BulkDiscountPercentInput
        {
            get => _bulkDiscountPercentInput;
            set => SetProperty(ref _bulkDiscountPercentInput, value);
        }

        public ICommand BulkAddDiscountsCommand { get; }

        // Discounts management page (added 2026-09-04) -- a full-screen
        // overlay, same Visible/Collapsed toggle idea as Checkout's Bills
        // browser (CheckoutView.xaml's SelectedBillsBrowser==null trick),
        // but simpler here: no separate ViewModel class, no CloseRequested
        // event wiring -- this page only ever shows/edits/removes a
        // discount on a row that's already part of _allRows, so it stays
        // entirely inside InventoryViewModel and reuses the exact same
        // PushChange/Undo/Redo/Save Changes pipeline every other action on
        // this screen already goes through (see the class doc comment on
        // staging). A separate ViewModel with its own immediate-write path
        // (the way BillsBrowserViewModel works) would mean TWO different
        // ways of writing to the same `goods` table that could race or
        // partially commit against each other on a row with other pending
        // edits -- not worth it for what's fundamentally a filtered view
        // of the same rows plus two small actions.
        private bool _isDiscountsBrowserOpen;
        public bool IsDiscountsBrowserOpen
        {
            get => _isDiscountsBrowserOpen;
            set => SetProperty(ref _isDiscountsBrowserOpen, value);
        }

        public ICommand OpenDiscountsCommand { get; }
        public ICommand CloseDiscountsCommand { get; }

        // Every currently-discounted product, name-sorted -- rebuilt by
        // RebuildDiscountedRows() alongside every other derived collection
        // (see RefreshDerivedCollections/LoadGoods) so it's always current
        // whether a discount was just added, edited, removed, or the whole
        // screen reloaded. Filters _allRows (not the post-search/category-
        // filter Rows) -- the Discounts page is its own independent view,
        // not affected by whatever search/category filter happens to be
        // active on the main grid.
        public ObservableCollection<InventoryRow> DiscountedRows { get; } = new ObservableCollection<InventoryRow>();

        public ICommand SaveDiscountEditCommand { get; }
        public ICommand RemoveDiscountCommand { get; }

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
            BulkAddDiscountsCommand = new RelayCommand(_ => BulkAddDiscounts());
            OpenDiscountsCommand = new RelayCommand(_ => IsDiscountsBrowserOpen = true);
            CloseDiscountsCommand = new RelayCommand(_ => IsDiscountsBrowserOpen = false);
            SaveDiscountEditCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) SaveDiscountEdit(row);
            });
            RemoveDiscountCommand = new RelayCommand(p =>
            {
                if (p is InventoryRow row) RemoveDiscount(row);
            });
            UndoCommand = new RelayCommand(_ => Undo());
            RedoCommand = new RelayCommand(_ => Redo());
            SaveChangesCommand = new RelayCommand(_ => SaveChanges());
            DiscardChangesCommand = new RelayCommand(_ => DiscardChanges());
            AdminUnlockCommand = new RelayCommand(_ => AdminUnlock());
            AppSettings.Changed += () =>
            {
                OnPropertyChanged(nameof(IsAdminUnlocked));
                OnPropertyChanged(nameof(IsAdminLocked));
            };

            InventoryDataEvents.GoodsChanged += ReloadIfNoUnsavedChanges;
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
            //
            // Routed through ReloadIfNoUnsavedChanges rather than LoadGoods
            // directly as of 2026-09-03 (staged edits) -- see that method's
            // comment for why a direct LoadGoods() here would risk silently
            // discarding a pending, not-yet-saved edit.
            AppSettings.Changed += ReloadIfNoUnsavedChanges;

            LoadCategories();
            LoadGoods();
            CaptureBaseline();
        }

        // Guards the two CROSS-SCREEN triggers of a reload (a Checkout sale
        // via InventoryDataEvents.GoodsChanged, a Settings threshold change
        // via AppSettings.Changed) against destroying pending, not-yet-saved
        // edits -- added 2026-09-03 alongside staged edits/Undo/Redo/Save
        // Changes. LoadGoods() rebuilds _allRows entirely from the database,
        // which is exactly right for THIS screen's own writes (SaveChanges
        // clears the undo stack before triggering its own reload, so
        // HasUnsavedChanges is already false by the time this check runs
        // there) but would silently discard anything still staged if some
        // OTHER screen's change reached here first. Accepted trade-off:
        // this screen's grid can go stale relative to a sale that just
        // happened elsewhere until the pending edits are Saved or
        // Discarded, which is a far smaller cost than losing unsaved work
        // out from under someone without warning.
        private void ReloadIfNoUnsavedChanges()
        {
            if (HasUnsavedChanges) return;
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
            _allRows = models.Select(m =>
            {
                // DiscountPercent set explicitly after construction, not
                // via GoodsR's constructor (added 2026-09-04 for
                // Inventory's Discounts feature) — see GoodsR.DiscountPercent's
                // own doc comment for why that property was deliberately
                // kept out of the constructor's parameter list.
                var goodsR = new Core.Models.GoodsR(
                    m.Id, m.Name, m.Category, m.Quantity, m.Cost, m.Price, m.Type, m.Barcode, m.Earned, m.Datex, m.Datee);
                goodsR.DiscountPercent = m.DiscountPercent;
                return new InventoryRow(goodsR);
            }).ToList();

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
            RebuildDiscountedRows();
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

            double oldQuantity = row.Quantity;
            string name = row.Name;

            PushChange(
                apply: () => row.Quantity = newQuantity,
                revert: () => row.Quantity = oldQuantity);

            row.AdjustInput = "";

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryAdjustSuccess"), name, newQuantity)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
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

            // Barcode optional; when present, must be unique. Checked
            // against the LOCAL, in-memory state (_allRows) as of
            // 2026-09-03, not the database -- see this class's staging-
            // model doc comment for why a database-only check would miss a
            // duplicate barcode between two products both staged in the
            // same not-yet-saved session. "" (no barcode) is deliberately
            // never checked here -- the partial unique index (and this
            // check) only constrains non-empty values, so any number of
            // barcode-less products is fine.
            if (!string.IsNullOrEmpty(barcode) && _allRows.Any(r => string.Equals(r.Barcode, barcode, StringComparison.Ordinal)))
            {
                StatusMessage = LocalizationManager.GetString("InventoryAddDuplicateBarcode");
                return;
            }

            string today = DateTime.Now.ToString("dd/MM/yyyy");
            var newRow = new InventoryRow(new Core.Models.GoodsR(
                NextTempId(), name, category, quantity, cost, price, "", barcode, 0, today, today));
            newRow.PropertyChanged += Row_PropertyChanged;

            PushChange(
                apply: () => _allRows.Insert(0, newRow),
                revert: () => _allRows.Remove(newRow));

            NewProductName = "";
            NewProductBarcode = "";
            NewProductCategoryInput = "";
            RebuildFilteredNewProductCategories(); // reset the narrowed list back to full after the field clears
            NewProductQuantity = "";
            NewProductCost = "";
            NewProductPrice = "";

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryAddSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
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

            // Checked against the LOCAL AllCategoryNames list, not the
            // database, as of 2026-09-03 -- see this class's staging-model
            // doc comment. AllCategoryNames is already kept live-correct by
            // every staged action (PushChange/Undo/Redo all funnel through
            // RefreshDerivedCollections), so it's the right thing to check
            // regardless of what's actually committed to the database yet.
            if (AllCategoryNames.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = LocalizationManager.GetString("InventoryCategoryAddDuplicate");
                return;
            }

            PushChange(
                apply: () => InsertCategorySorted(name),
                revert: () => AllCategoryNames.Remove(name));

            NewCategoryName = "";
            StatusMessage = string.Format(LocalizationManager.GetString("InventoryCategoryAddSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
        }

        // Keeps AllCategoryNames alphabetically ordered after a staged
        // add, matching what LoadCategories()' own "ORDER BY Name ASC"
        // query would produce -- a plain .Add() would tack the new name
        // onto the end instead, out of order until the next full reload.
        private void InsertCategorySorted(string name)
        {
            int index = 0;
            while (index < AllCategoryNames.Count &&
                   string.Compare(AllCategoryNames[index], name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                index++;
            }
            AllCategoryNames.Insert(index, name);
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
            // this category. Checked against the LOCAL _allRows as of
            // 2026-09-03, not the database -- see this class's staging-
            // model doc comment: a product bulk-moved out of this category
            // earlier in the same pending session must actually unblock
            // this delete, and one moved INTO it must still block it,
            // neither of which the database yet knows about. The only way
            // to clear this block right now is editing each of those
            // products to a different category first (Edit Product, or
            // Bulk Change Category).
            int inUseCount = _allRows.Count(r => string.Equals(r.Category, name, StringComparison.OrdinalIgnoreCase));
            if (inUseCount > 0)
            {
                StatusMessage = string.Format(LocalizationManager.GetString("InventoryCategoryDeleteInUse"), name, inUseCount);
                return;
            }

            string exact = AllCategoryNames.First(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
            int index = AllCategoryNames.IndexOf(exact);

            PushChange(
                apply: () => AllCategoryNames.Remove(exact),
                revert: () =>
                {
                    if (!AllCategoryNames.Contains(exact))
                        AllCategoryNames.Insert(Math.Min(index, AllCategoryNames.Count), exact);
                });

            CategoryToDelete = null;
            StatusMessage = string.Format(LocalizationManager.GetString("InventoryCategoryDeleteSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
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

            // Checked against the LOCAL _allRows, not the database, as of
            // 2026-09-03 -- see this class's staging-model doc comment.
            if (!string.IsNullOrEmpty(barcode) && _allRows.Any(r => r != row && string.Equals(r.Barcode, barcode, StringComparison.Ordinal)))
            {
                StatusMessage = LocalizationManager.GetString("InventoryEditDuplicateBarcode");
                return;
            }

            string oldName = row.Name, oldCategory = row.Category, oldBarcode = row.Barcode;
            double oldCost = row.Cost, oldPrice = row.Price;

            PushChange(
                apply: () =>
                {
                    row.Name = name;
                    row.Category = category;
                    row.Cost = cost;
                    row.Price = price;
                    row.Barcode = barcode;
                    row.IsEditing = false;
                },
                revert: () =>
                {
                    row.Name = oldName;
                    row.Category = oldCategory;
                    row.Cost = oldCost;
                    row.Price = oldPrice;
                    row.Barcode = oldBarcode;
                });

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryEditSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
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

            string name = row.Name;
            int index = _allRows.IndexOf(row);

            PushChange(
                apply: () => _allRows.Remove(row),
                revert: () =>
                {
                    if (!_allRows.Contains(row))
                        _allRows.Insert(Math.Min(index, _allRows.Count), row);
                });

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryDeleteSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
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

            var snapshot = selected.Select(r => (Row: r, Index: _allRows.IndexOf(r))).ToList();

            PushChange(
                apply: () =>
                {
                    foreach (var entry in snapshot) _allRows.Remove(entry.Row);
                },
                revert: () =>
                {
                    foreach (var entry in snapshot.OrderBy(e => e.Index))
                    {
                        if (!_allRows.Contains(entry.Row))
                            _allRows.Insert(Math.Min(entry.Index, _allRows.Count), entry.Row);
                    }
                });

            foreach (var row in selected) row.IsSelected = false;

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryBulkDeleteSuccess"), selected.Count)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
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

            var snapshot = selected.Select(r => (Row: r, OldCategory: r.Category)).ToList();

            PushChange(
                apply: () =>
                {
                    foreach (var entry in snapshot) entry.Row.Category = category;
                },
                revert: () =>
                {
                    foreach (var entry in snapshot) entry.Row.Category = entry.OldCategory;
                });

            foreach (var row in selected) row.IsSelected = false;
            BulkCategoryTarget = null;

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryBulkCategoryChangeSuccess"), selected.Count, category)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
        }

        // Discounts feature (added 2026-09-04) -- see the property block
        // above (BulkDiscountPercentInput/IsDiscountsBrowserOpen/
        // DiscountedRows) for the overall design reasoning.

        // Rebuilds DiscountedRows from _allRows -- called from LoadGoods()
        // and RefreshDerivedCollections() (i.e. after every staged action,
        // same uniform-refresh reasoning that method's own comment already
        // gives), so this is always an accurate, current filter regardless
        // of what changed.
        private void RebuildDiscountedRows()
        {
            DiscountedRows.Clear();
            foreach (var row in _allRows.Where(r => r.HasDiscount).OrderBy(r => r.Name))
                DiscountedRows.Add(row);
        }

        // Bulk "Add Discounts" (added 2026-09-04) -- applies ONE typed-in
        // percentage to every currently-selected product at once, same
        // selection source and no-confirmation-modal directness as
        // BulkDelete/BulkChangeCategory above. Overwrites any discount a
        // selected product already had rather than refusing or stacking --
        // "a product cannot have more than one discount" (explicit
        // requirement) is what makes overwrite the only sensible behavior
        // here: there is no second slot to stack into, by construction
        // (see Core.Models.Goods.DiscountPercent's doc comment). Anyone who
        // wants to see or fine-tune what a specific product's discount
        // already is before changing it can do that from the Discounts
        // page instead, which shows the current value per row.
        private void BulkAddDiscounts()
        {
            if (!RequireAdminUnlocked()) return;

            var selected = _allRows.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = LocalizationManager.GetString("InventoryBulkNoSelection");
                return;
            }

            if (!double.TryParse(BulkDiscountPercentInput, out double discountPercent) || discountPercent < 0 || discountPercent > 100)
            {
                StatusMessage = LocalizationManager.GetString("InventoryBulkDiscountInvalid");
                return;
            }

            var snapshot = selected.Select(r => (Row: r, OldDiscount: r.DiscountPercent)).ToList();

            PushChange(
                apply: () =>
                {
                    foreach (var entry in snapshot) entry.Row.DiscountPercent = discountPercent;
                },
                revert: () =>
                {
                    foreach (var entry in snapshot) entry.Row.DiscountPercent = entry.OldDiscount;
                });

            foreach (var row in selected) row.IsSelected = false;
            BulkDiscountPercentInput = "";

            StatusMessage = string.Format(LocalizationManager.GetString("InventoryBulkDiscountSuccess"), selected.Count, discountPercent)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
        }

        // Discounts page: per-row Save (added 2026-09-04) -- reads that
        // row's own DiscountEditInput buffer (see InventoryRow's doc
        // comment on that property for why it's a separate typed-in buffer
        // rather than a live binding straight to DiscountPercent), same
        // validation range as the bulk version above. Admin-gated, same as
        // every other discount-changing action on this screen.
        private void SaveDiscountEdit(InventoryRow row)
        {
            if (!RequireAdminUnlocked()) return;

            if (!double.TryParse(row.DiscountEditInput, out double discountPercent) || discountPercent < 0 || discountPercent > 100)
            {
                StatusMessage = LocalizationManager.GetString("InventoryBulkDiscountInvalid");
                return;
            }

            double oldDiscount = row.DiscountPercent;
            string name = row.Name;

            PushChange(
                apply: () => row.DiscountPercent = discountPercent,
                revert: () => row.DiscountPercent = oldDiscount);

            StatusMessage = string.Format(LocalizationManager.GetString("DiscountsBrowserSaveSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
        }

        // Discounts page: per-row Remove (added 2026-09-04) -- sets
        // DiscountPercent back to 0 ("no discount"), same PushChange
        // staging as everything else here rather than a separate delete-
        // style path; the row simply drops out of DiscountedRows on the
        // next RebuildDiscountedRows (fired by PushChange's own
        // RefreshDerivedCollections call), same as any other filtered-out
        // row elsewhere in this class.
        private void RemoveDiscount(InventoryRow row)
        {
            if (!RequireAdminUnlocked()) return;

            double oldDiscount = row.DiscountPercent;
            string name = row.Name;

            PushChange(
                apply: () => row.DiscountPercent = 0,
                revert: () => row.DiscountPercent = oldDiscount);

            StatusMessage = string.Format(LocalizationManager.GetString("DiscountsBrowserRemoveSuccess"), name)
                + " " + LocalizationManager.GetString("InventoryPendingSaveNote");
        }
    }
}
