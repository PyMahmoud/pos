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
        public double DiscountPercent { get; set; }
       

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
