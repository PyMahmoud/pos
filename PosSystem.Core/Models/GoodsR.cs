using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Models
{
    public class GoodsR : INotifyPropertyChanged
    {
        private int id;
        private string name;
        private string category;
        private double quantity;
        private double cost;
        private double price;
        private string type;
        private string barcode;
        private double earned;
        private string datex;
        private string datee;
       
        public GoodsR()
        {
        }

        public GoodsR(int id, string category )
        {
            this.id = id;
            this.category = category;
           
        }

        public GoodsR(int id, string name, string category, double quantity, double cost, double price, string type, string barcode, double earned, string datex, string datee)
        {
            this.id = id;
            this.name = name;
            this.category = category;
            this.quantity = quantity;
            this.cost = cost;
            this.price = price;
            this.type = type;
            this.barcode = barcode;
            this.earned = earned;
            this.datex = datex;
            this.datee = datee;
           
        }

        public int Id { get => id; set => id = value; }
        public string Name { get => name; set => name = value; }
       
        public string Category
        {
            get { return category; }
            set { category = value; NotifyPropertyChanged("Category"); }
        }
        public double Quantity { get => quantity; set => quantity = value; }
        public double Cost { get => cost; set => cost = value; }
        public double Price
        {
            get { return price; }
            set
            {
                price = value; NotifyPropertyChanged("Price");
                // Discount (2026-09-05) -- these derive from Price, so a
                // Price change has to re-notify them too, same reasoning
                // as InventoryRow.Price's setter (App project).
                NotifyPropertyChanged("HasDiscount");
                NotifyPropertyChanged("DiscountedPrice");
                NotifyPropertyChanged("DiscountAmount");
                //NotifyPropertyChanged("PriceBrush");
            }
        }
        //public string PriceBrush
        //{
        //    get
        //    {
        //        if (Quantity == 0)
        //        {

        //            return "Red";
        //        }
        //        else
        //        {
        //            return "Blue";
        //        }

        //    }
        //}
        public string Type { get => type; set => type = value; }
        public string Barcode { get => barcode; set => barcode = value; }
        public double Earned { get => earned; set => earned = value; }
        public string Datex { get => datex; set => datex = value; }
        public string Datee { get => datee; set => datee = value; }

        // Added 2026-09-04, same field/reasoning as Core.Models.Goods.
        // DiscountPercent -- see that class's doc comment. Not wired into
        // the constructor (both constructors are used positionally at
        // several existing call sites -- adding a 12th parameter would
        // touch every one of them for a value that's always 0 at
        // construction anyway); callers that need to carry a real
        // DiscountPercent through set this property directly after
        // construction instead (see InventoryViewModel.LoadGoods).
        private double discountPercent;
        public double DiscountPercent
        {
            get => discountPercent;
            set
            {
                discountPercent = value;
                NotifyPropertyChanged("DiscountPercent");
                NotifyPropertyChanged("HasDiscount");
                NotifyPropertyChanged("DiscountedPrice");
                NotifyPropertyChanged("DiscountAmount");
            }
        }

        // Discount display helpers (added 2026-09-05, Checkout's item grid
        // and CartLine both need these) -- identical formulas to
        // InventoryRow's own HasDiscount/DiscountedPrice/DiscountAmount
        // (App project) so a product's markdown reads the same way
        // everywhere it's shown: Inventory's product card, Checkout's item
        // tile, and a cart line once it's added.
        public bool HasDiscount => DiscountPercent > 0;
        public double DiscountedPrice => Math.Round(Price * (1 - DiscountPercent / 100.0), 2);
        public double DiscountAmount => Math.Round(Price * DiscountPercent / 100.0, 2);

        // Added 2026-09-10, same reasoning as Core.Models.Goods.MinStock --
        // not wired into either constructor for the same reason
        // DiscountPercent above isn't (see that property's own comment);
        // InventoryViewModel.LoadGoods sets this directly after
        // construction, same pattern.
        private double? minStock;
        public double? MinStock
        {
            get => minStock;
            set { minStock = value; NotifyPropertyChanged("MinStock"); }
        }

        // Added 2026-09-10 for pricing guardrails (Reference-Repo-Features-
        // Plan.md item #3), same not-in-either-constructor reasoning as
        // MinStock/DiscountPercent above -- CartLine reads this straight
        // off the GoodsR it's constructed from (see CartLine's own doc
        // comment); InventoryViewModel.LoadGoods sets it after
        // construction the same way it does MinStock.
        private double? minSalePrice;
        public double? MinSalePrice
        {
            get => minSalePrice;
            set { minSalePrice = value; NotifyPropertyChanged("MinSalePrice"); }
        }
       

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
    }
}
