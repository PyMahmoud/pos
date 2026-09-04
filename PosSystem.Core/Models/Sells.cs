using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Models
{
    public class Sells : INotifyPropertyChanged
    {
        private int id;
        private string name;
        private string category;
        private double quantity;
        private double cost;
        private double price;
        private string type;
        private string time;
        private string datex;
        private string barcode;
        private int billnumber;
        private double earned;
        private string details;
        private string returned;

        // Added 2026-08-28 for receipt revisioning -- see
        // DatabaseBootstrapper's matching comment. The specific bills.ID
        // row this line belongs to, since Billnumber alone can now be
        // shared by several bills rows (an original plus its revisions).
        private int billId;

        // Added 2026-09-04 for the Bills browser's "stage several returns,
        // then Save" flow (see BillsBrowserViewModel's class doc comment).
        // NOT a database column and never sent to Data.Sells.InsertSells --
        // purely in-memory UI state on the Sells object the Bills detail
        // view is already bound to, tracking how much of THIS line's
        // Quantity the person has marked to return but not yet committed.
        // Reset to 0 every time BillsBrowserViewModel.LoadBillLines re-reads
        // fresh Sells objects from the database (i.e. on every OpenBill), so
        // nothing needs to explicitly "discard" it when navigating away from
        // an unsaved bill -- the staged objects are simply dropped.
        private double pendingReturnQuantity;
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
        public Sells()
        {
        }

        public Sells(int id, string name, string category, double quantity, double cost, double price, string type, string time, string datex, string barcode, int billnumber, double earned, string details, string returned)
        {
            this.id = id;
            this.name = name;
            this.category = category;
            this.quantity = quantity;
            this.cost = cost;
            this.price = price;
            this.type = type;
            this.time = time;
            this.datex = datex;
            this.barcode = barcode;
            this.billnumber = billnumber;
            this.earned = earned;
            this.details = details;
            this.returned = returned;
        }

        public int Id { get => id; set => id = value;  }
        public string Name
        {
            get { return name; }
            set { name = value; NotifyPropertyChanged("Name"); }
        }
        public string Category { get => category; set => category = value; }
       
        public double Quantity
        {
            get { return quantity; }
            set { quantity = value; NotifyPropertyChanged("Quantity"); }
        }

        public double Cost
        {
            get { return cost; }
            set { cost = value; NotifyPropertyChanged("Cost"); }
        }
        public double Price
        {
            get { return price; }
            set { price = value; NotifyPropertyChanged("Price");
                NotifyPropertyChanged("PriceBrush");
            }
        }
        public string PriceBrush
        {
            get
            {
                if (Price < (Cost * Quantity))
                {

                    return "Red";
                }
                else
                {
                    return "Black";
                }
               
            }
        }
        public string Type { get => type; set => type = value; }
        public string Time { get => time; set => time = value; }
        public string Datex { get => datex; set => datex = value; }
        public string Barcode { get => barcode; set => barcode = value; }
        public double Earned
        {
            get { return earned; }
            set { earned = value; NotifyPropertyChanged("Earned"); }
        }
        public string Details
        {
            get { return details; }
            set { details = value; NotifyPropertyChanged("Details"); }
        }

        public int Billnumber { get => billnumber; set => billnumber = value; }
        public string Returned { get => returned; set => returned = value; }
        public int BillId { get => billId; set => billId = value; }

        public double PendingReturnQuantity
        {
            get { return pendingReturnQuantity; }
            set
            {
                pendingReturnQuantity = value;
                NotifyPropertyChanged("PendingReturnQuantity");
                NotifyPropertyChanged("RemainingQuantity");
                NotifyPropertyChanged("HasPendingReturn");
            }
        }

        // What will actually remain on this line once staged returns are
        // saved -- what the Bills detail view shows instead of the raw
        // Quantity, without mutating Quantity itself until the save
        // actually happens (Quantity here stays exactly what was sold).
        public double RemainingQuantity => quantity - pendingReturnQuantity;
        public bool HasPendingReturn => pendingReturnQuantity > 0;
    }
       
}
