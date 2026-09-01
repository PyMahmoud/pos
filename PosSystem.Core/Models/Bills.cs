using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Models
{
    public class Bills : INotifyPropertyChanged
    {
        private int id;
        private int billnumber;
        private double billcost;
        private string time;
        private string datex;
        private string ownername;
        private string ownerid;
        private string ownernumber;
        private double paid;
        private double remain;
        private double earned;
        private double tax;
        private double discount;
        private string details;

        // Added 2026-08-27 for item #6 (Bills view + delete/reversal) --
        // needed to find and reverse a linked customer's Paid/Remain when a
        // bill or a line within it is deleted. Mirrors the same column
        // Data.Bills.InsertBills already writes (bills.CustomerId, see that
        // method) -- this model class just never had a property for reading
        // it back until now, since nothing needed to read it before.
        private int? customerId;

        // Added 2026-08-28 for receipt revisioning -- see
        // DatabaseBootstrapper's matching comment for the full schema
        // reasoning. IsCurrent defaults to true here so that any code
        // constructing a Bills object directly (rather than reading one
        // back from SQLite) gets the safer "this is a normal, current
        // bill" default rather than silently constructing an
        // already-superseded one. RevisionSuffix null means "original,
        // never-edited receipt" -- see DisplayNumber below.
        private bool isCurrent = true;
        private string revisionSuffix;

        // Added 2026-09-01 alongside per-customer/per-bill discounts -- the
        // discount PERCENTAGE actually applied to this specific bill (the
        // existing Discount property above stays the resulting currency
        // amount, same convention Tax/TaxAmount already split). Kept so a
        // return's partial-revision math (BillsBrowserViewModel.
        // CreateReturnRevision) can recover the exact original percentage
        // rather than re-deriving it from a shrinking subtotal.
        private double discountPercent;

        public int Id { get => id; set => id = value; }
        public int Billnumber { get => billnumber; set => billnumber = value; }
        public double Billcost { get => billcost; set => billcost = value; }
        public string Time { get => time; set => time = value; }
        public string Datex { get => datex; set => datex = value; }
        public string Ownername { get => ownername; set => ownername = value; }
        public string Ownerid { get => ownerid; set => ownerid = value; }
        public string Ownernumber { get => ownernumber; set => ownernumber = value; }
        public double Paid { get => paid; set => paid = value; }
        public double Remain { get => remain; set => remain = value; }
        public double Earned { get => earned; set => earned = value; }
        public double Tax { get => tax; set => tax = value; }
        public double Discount { get => discount; set => discount = value; }
        public string Details { get => details; set => details = value; }
        public int? CustomerId { get => customerId; set => customerId = value; }
        public bool IsCurrent { get => isCurrent; set => isCurrent = value; }
        public string RevisionSuffix { get => revisionSuffix; set => revisionSuffix = value; }
        public double DiscountPercent { get => discountPercent; set => discountPercent = value; }

        // "210" for an original receipt, "210-e1"/"210-e2"/... once it's
        // been returned-from one or more times (see BillsBrowserViewModel
        // for where RevisionSuffix values get assigned) -- what every
        // screen that shows a bill number to a person should bind to
        // instead of the raw Billnumber, so the UI never shows two
        // different receipts both labeled plain "Bill #210" once a return
        // has happened.
        public string DisplayNumber => string.IsNullOrEmpty(RevisionSuffix)
            ? Billnumber.ToString()
            : Billnumber + "-" + RevisionSuffix;

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
        public Bills()
        {
        }

        public Bills(int id, int billnumber, double billcost, string time, string datex, string ownername, string ownerid, string ownernumber, double paid, double remain, double earned, double tax, double discount, string details)
        {
            this.id = id;
            this.billnumber = billnumber;
            this.billcost = billcost;
            this.time = time;
            this.datex = datex;
            this.ownername = ownername;
            this.ownerid = ownerid;
            this.ownernumber = ownernumber;
            this.paid = paid;
            this.remain = remain;
            this.earned = earned;
            this.tax = tax;
            this.discount = discount;
            this.details = details;
        }
    }
}