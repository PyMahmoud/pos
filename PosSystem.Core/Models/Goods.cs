using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Models
{
    public class Goods : INotifyPropertyChanged
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

        // Added 2026-09-04 for Inventory's product-level Discounts feature
        // (bulk "Add Discounts" button + Discounts management page,
        // explicit request from a screenshot of Checkout's own discount
        // line -- "a minus price like one in checkout"). Deliberately a
        // single percentage field, not a separate discounts table: a
        // product can never have more than one standing discount at a
        // time, which this design guarantees structurally (setting a new
        // discount always overwrites the old one -- there is nowhere a
        // second one could be stored) rather than needing an app-level
        // "only one active discount per product" check that could drift
        // out of sync with the data. 0 means "no discount", same convention
        // customers.DiscountPercent/bills.DiscountPercent already use (see
        // DatabaseBootstrapper's comment on those). This is a STANDING
        // markdown on the product itself, separate from and not currently
        // combined with Checkout's existing per-bill/per-customer discount
        // -- a discounted product is not yet sold at its discounted price
        // at Checkout; see InventoryViewModel's class doc comment for the
        // full reasoning and the flagged follow-up.
        private double discountPercent;
       

        public Goods()
        {
        }

        public Goods(int id, string category , byte[] image)
        {
            this.id = id;
            this.category = category;
          
        }

        public Goods(int id, string name, string category, double quantity, double cost, double price, string type, string barcode, double earned, string datex, string datee)
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
                NotifyPropertyChanged("PriceBrush");
            }
        }
        public string PriceBrush
        {
            get
            {
                if (Quantity == 0)
                {

                    return "Red";
                }
                else
                {
                    return "Blue";
                }

            }
        }
        public string Type { get => type; set => type = value; }
        public string Barcode { get => barcode; set => barcode = value; }
        public double Earned { get => earned; set => earned = value; }
        public string Datex { get => datex; set => datex = value; }
        public string Datee { get => datee; set => datee = value; }
        public double DiscountPercent { get => discountPercent; set => discountPercent = value; }
   

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
