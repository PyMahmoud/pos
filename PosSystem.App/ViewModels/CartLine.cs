using System.ComponentModel;
using System.Runtime.CompilerServices;
using PosSystem.Core.Models;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One line in the current order. Snapshots name/price/cost/barcode from
    /// the GoodsR it was added from — if Mahmoud or I change a price in
    /// Inventory mid-shift, an order already in someone's cart shouldn't
    /// shift under them. Quantity is the only live-editable part, capped at
    /// MaxAvailable (the stock on hand at the moment this line was added).
    /// </summary>
    public class CartLine : INotifyPropertyChanged
    {
        public int GoodId { get; }
        public string Name { get; }
        public string Category { get; }
        public string Type { get; }
        public string Barcode { get; }
        public double Price { get; }
        public double Cost { get; }
        public double MaxAvailable { get; }

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set
            {
                if (value < 1) value = 1;
                if (value > MaxAvailable) value = MaxAvailable;
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        public double LineTotal => Price * Quantity;

        public CartLine(GoodsR good, double initialQuantity = 1)
        {
            GoodId = good.Id;
            Name = good.Name;
            Category = good.Category;
            Type = good.Type;
            Barcode = good.Barcode;
            Price = good.Price;
            Cost = good.Cost;
            MaxAvailable = good.Quantity;
            _quantity = initialQuantity > MaxAvailable ? MaxAvailable : initialQuantity;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
