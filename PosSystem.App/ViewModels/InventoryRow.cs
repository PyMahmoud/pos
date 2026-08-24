using System.ComponentModel;
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
        // Placeholder default, not confirmed with the client — see
        // InventoryViewModel's class-level comment for why this is a plain
        // constant rather than a stored-per-product or stored-global
        // setting (same "flag rather than add a schema column unasked"
        // reasoning Phase 4 already applied to the Goods IsAvailable
        // question).
        public const double LowStockThreshold = 10;

        public int Id { get; }
        public string Name { get; }
        public string Category { get; }
        public double Cost { get; }
        public double Price { get; }
        public string Type { get; }
        public string Barcode { get; }

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

        public InventoryRow(Core.Models.GoodsR model)
        {
            Id = model.Id;
            Name = model.Name;
            Category = model.Category;
            Cost = model.Cost;
            Price = model.Price;
            Type = model.Type;
            Barcode = model.Barcode;
            _quantity = model.Quantity;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
