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
    ///
    /// Discount stacking (added 2026-09-05, explicit request): a product's
    /// own Inventory discount is baked into Price right here at
    /// construction — Price IS the discounted per-unit price from this
    /// point on, not the sticker price (that's kept separately as
    /// OriginalPrice, for display only). Every downstream consumer of Price
    /// (LineTotal, CheckoutViewModel.Subtotal, the bill-level
    /// DiscountPercentInput applied on top of THAT Subtotal, InsertSells'
    /// saved Price column, and the profit calc off (Price - Cost)) already
    /// treats Price as "what this line actually charges" and needed no
    /// further special-casing — the two discounts stack automatically
    /// because the bill-level percentage is computed against a Subtotal
    /// that already reflects this one.
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

        // Discount (added 2026-09-05) -- see the class doc comment above
        // for why Price itself is already the discounted figure. These
        // three exist purely so the cart line can SHOW the markdown (the
        // "minus theme", same badge/strikethrough/-amount treatment as
        // Inventory's product card and Checkout's own item tile) without
        // the display needing to reverse-engineer the discount back out of
        // Price and OriginalPrice itself.
        public double OriginalPrice { get; }
        public double DiscountPercent { get; }
        public bool HasDiscount => DiscountPercent > 0;
        public double DiscountAmountPerUnit => System.Math.Round(OriginalPrice - Price, 2);

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
            OriginalPrice = good.Price;
            DiscountPercent = good.DiscountPercent;
            Price = System.Math.Round(good.Price * (1 - good.DiscountPercent / 100.0), 2);
            Cost = good.Cost;
            MaxAvailable = good.Quantity;
            _quantity = initialQuantity > MaxAvailable ? MaxAvailable : initialQuantity;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
