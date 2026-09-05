using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One row on the Inventory screen. Wraps a Core.Models.GoodsR with
    /// INotifyPropertyChanged so Quantity/stock-status badges repaint
    /// immediately after an adjustment, without needing a full list reload.
    /// AdjustInput is this row's own quantity-entry TextBox binding — kept
    /// per-row (not one shared field on the ViewModel), same reasoning as
    /// CustomerRow.PaymentInput: typing into one product's box can never
    /// bleed into another's.
    /// </summary>
    public class InventoryRow : INotifyPropertyChanged
    {
        // Was a plain, unconfirmed-with-the-client placeholder constant
        // (10) until the Settings screen got real content (2026-08-26) --
        // see InventoryViewModel's class-level comment (historical, kept
        // for context) and AppSettings.LowStockThreshold's own doc comment.
        // Reading the live app-wide setting here means every InventoryRow
        // (existing or freshly constructed) reflects whatever was last
        // saved on Settings without needing its own subscription --
        // InventoryViewModel.LoadGoods() rebuilds every row from scratch on
        // AppSettings.Changed (see that class), so this always gets
        // re-evaluated against the current value rather than a stale one
        // captured at construction time.
        public static double LowStockThreshold => PosSystem.App.AppSettings.LowStockThreshold;

        // Settable as of 2026-09-03 (Inventory's staged-edits feature) --
        // a newly-Added-but-not-yet-Saved row is given a temporary,
        // negative placeholder ID (see InventoryViewModel.NextTempId) since
        // it doesn't exist in the database yet; SaveChanges() sets this to
        // the real, positive, database-assigned ID once the INSERT actually
        // runs. Every other row (loaded from the database, or a temp row
        // before Save) never has this touched after construction -- this
        // is not a general-purpose settable property for casual use
        // elsewhere, just what the staging/commit flow specifically needs.
        public int Id { get; internal set; }

        // Name/Category/Cost/Price/Barcode were plain get-only properties
        // until Inventory's Edit Product feature (2026-08-25) — settable +
        // notifying now, same reasoning as Quantity below, so a saved edit
        // repaints this card immediately without a full LoadGoods() reload.
        private string _name;
        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        private string _category;
        public string Category
        {
            get => _category;
            set { if (_category == value) return; _category = value; OnPropertyChanged(); }
        }

        private double _cost;
        public double Cost
        {
            get => _cost;
            set { if (_cost == value) return; _cost = value; OnPropertyChanged(); }
        }

        private double _price;
        public double Price
        {
            get => _price;
            set
            {
                if (_price == value) return;
                _price = value;
                OnPropertyChanged();
                // Discount (added 2026-09-04) derives from Price, so a
                // Price edit has to re-notify these too or a stale
                // DiscountedPrice/DiscountAmount would linger on-screen
                // after Save Edit changes a discounted product's price.
                OnPropertyChanged(nameof(DiscountedPrice));
                OnPropertyChanged(nameof(DiscountAmount));
            }
        }

        public string Type { get; }

        // Discount (added 2026-09-04, Inventory's Discounts feature -- bulk
        // "Add Discounts" button + a dedicated Discounts management page,
        // requested off a screenshot of Checkout's own discount line: "a
        // minus price like one in checkout"). A single percentage, same
        // convention as Core.Models.Goods.DiscountPercent (0 = no
        // discount) -- see that class's doc comment for why a product can
        // never have more than one discount by construction. Setting this
        // also re-syncs DiscountEditInput to match (see that property's
        // own comment) so the Discounts page's edit box always reflects
        // whatever the real, current value actually is, not a stale one
        // left over from before the last change.
        private double _discountPercent;
        public double DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent == value) return;
                _discountPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(DiscountedPrice));
                OnPropertyChanged(nameof(DiscountAmount));
                _discountEditInput = value.ToString(CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(DiscountEditInput));
            }
        }

        public bool HasDiscount => DiscountPercent > 0;

        // Rounded to 2 decimal places, same convention as every other
        // currency figure this app displays (StringFormat={}{0:0.00}
        // throughout the XAML) -- computed here rather than in the View so
        // Checkout's future integration (flagged, not yet built -- see
        // InventoryViewModel's class doc comment) has one already-correct
        // place to read a discounted price from, instead of re-deriving
        // the same formula a second time.
        public double DiscountedPrice => System.Math.Round(Price * (1 - DiscountPercent / 100.0), 2);

        // The currency amount taken off, shown with a leading "-" in the
        // View exactly the way CheckoutView.xaml's own DiscountAmount line
        // already does (StringFormat={}-{0:0.00}) -- the visual reference
        // point from the screenshot this feature was requested from.
        public double DiscountAmount => System.Math.Round(Price * DiscountPercent / 100.0, 2);

        // Edit buffer for the Discounts page's per-row "new %" TextBox --
        // same buffer-not-live-binding reasoning as AdjustInput/EditCost/
        // EditPrice elsewhere in this class (a partial/invalid typed value
        // never touches the real DiscountPercent until explicitly saved).
        // Initialized from the real value at construction and re-synced by
        // DiscountPercent's own setter above on every subsequent change
        // (bulk-apply, Discounts-page Save, or Remove), so this only ever
        // shows something stale while the user is actively mid-edit in the
        // box, never after any actual change lands.
        private string _discountEditInput = "0";
        public string DiscountEditInput
        {
            get => _discountEditInput;
            set { if (_discountEditInput == value) return; _discountEditInput = value; OnPropertyChanged(); }
        }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set { if (_barcode == value) return; _barcode = value; OnPropertyChanged(); }
        }

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOutOfStock));
                OnPropertyChanged(nameof(IsLowStock));
                OnPropertyChanged(nameof(IsInStock));
            }
        }

        public bool IsOutOfStock => Quantity <= 0;
        public bool IsLowStock => Quantity > 0 && Quantity <= LowStockThreshold;
        public bool IsInStock => Quantity > LowStockThreshold;

        private string _adjustInput = "";
        public string AdjustInput
        {
            get => _adjustInput;
            set { if (_adjustInput == value) return; _adjustInput = value; OnPropertyChanged(); }
        }

        // Batch selection (added 2026-08-31) -- backs a checkbox on each
        // card. InventoryViewModel listens for this specific property to
        // change (see its Row_PropertyChanged) to keep SelectedCount and
        // the bulk-action bar's visibility current without a full reload.
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        // Edit-mode state, added 2026-08-25 for the per-card Edit button.
        // IsEditing toggles which half of the card's DataTemplate is
        // visible (InventoryView.xaml, via BoolToVisibilityConverter — the
        // read-only half binds IsNotEditing). The Edit* fields are a
        // separate typed-in buffer, not a live binding straight to
        // Name/Category/Cost/Price/Barcode above, for the same reason
        // AdjustInput is separate from Quantity: so Cancel can throw away
        // an in-progress edit without having half-typed a stray character
        // into the real value first, and so a parse failure (e.g. Price
        // buffer briefly not a valid number while typing) never touches
        // the real Price the rest of the app reads. InventoryViewModel.
        // StartEditCommand populates these from the real values;
        // SaveEditCommand validates them and, only on success, writes them
        // back to the real properties above.
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (_isEditing == value) return;
                _isEditing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotEditing));
            }
        }
        public bool IsNotEditing => !IsEditing;

        private string _editName = "";
        public string EditName
        {
            get => _editName;
            set { if (_editName == value) return; _editName = value; OnPropertyChanged(); }
        }

        private string _editBarcode = "";
        public string EditBarcode
        {
            get => _editBarcode;
            set { if (_editBarcode == value) return; _editBarcode = value; OnPropertyChanged(); }
        }

        // Selection-only (bound to a non-editable ComboBox's SelectedItem,
        // ItemsSource=InventoryViewModel.AllCategoryNames) — not free-typed
        // text, so unlike EditName/EditBarcode/EditCost/EditPrice there is
        // no "is this a real category" validation needed on save: a
        // selection can only ever be one of the real, known category names
        // (or null, if nothing's been picked yet, which SaveEditCommand
        // does still check for).
        private string _editCategoryInput = "";
        public string EditCategoryInput
        {
            get => _editCategoryInput;
            set { if (_editCategoryInput == value) return; _editCategoryInput = value; OnPropertyChanged(); }
        }

        private string _editCost = "";
        public string EditCost
        {
            get => _editCost;
            set { if (_editCost == value) return; _editCost = value; OnPropertyChanged(); }
        }

        private string _editPrice = "";
        public string EditPrice
        {
            get => _editPrice;
            set { if (_editPrice == value) return; _editPrice = value; OnPropertyChanged(); }
        }

        public InventoryRow(Core.Models.GoodsR model)
        {
            Id = model.Id;
            _name = model.Name;
            _category = model.Category;
            _cost = model.Cost;
            _price = model.Price;
            Type = model.Type;
            _barcode = model.Barcode;
            _quantity = model.Quantity;
            _discountPercent = model.DiscountPercent;
            _discountEditInput = model.DiscountPercent.ToString(CultureInfo.InvariantCulture);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
