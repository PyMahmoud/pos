using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Models
{
    public class Customers
    {
        private int id;
        private string ownername;
        private string ownerid;
        private string ownernumber;
        private double paid;
        private double remain;
        private double creditOwed;
        private double discountPercent;

        public Customers()
        {

        }

        public Customers(int id, string ownername, string ownerid, string ownernumber, double paid, double remain)
        {
            this.id = id;
            this.ownername = ownername;
            this.ownerid = ownerid;
            this.ownernumber = ownernumber;
            this.paid = paid;
            this.remain = remain;
        }

        // Added for "money we owe the customer" (2026-08-31) — a second
        // overload, not a change to the constructor above, so every
        // existing call site that only knows about Paid/Remain keeps
        // compiling unchanged and just gets CreditOwed = 0.
        public Customers(int id, string ownername, string ownerid, string ownernumber, double paid, double remain, double creditOwed)
            : this(id, ownername, ownerid, ownernumber, paid, remain)
        {
            this.creditOwed = creditOwed;
        }

        // Added for per-customer default discount percentage (2026-09-01)
        // -- a third overload, same reasoning as the CreditOwed one above:
        // every existing call site that only knows Paid/Remain/CreditOwed
        // keeps compiling unchanged and just gets DiscountPercent = 0.
        public Customers(int id, string ownername, string ownerid, string ownernumber, double paid, double remain, double creditOwed, double discountPercent)
            : this(id, ownername, ownerid, ownernumber, paid, remain, creditOwed)
        {
            this.discountPercent = discountPercent;
        }

        public int Id { get => id; set => id = value; }
        public string Ownername { get => ownername; set => ownername = value; }
        public string Ownerid { get => ownerid; set => ownerid = value; }
        public string Ownernumber { get => ownernumber; set => ownernumber = value; }
        public double Paid { get => paid; set => paid = value; }
        public double Remain { get => remain; set => remain = value; }

        // What the shop owes THIS customer — the flip side of Remain (what
        // the customer owes the shop). See DatabaseBootstrapper's
        // customers.CreditOwed comment for how this can grow.
        public double CreditOwed { get => creditOwed; set => creditOwed = value; }

        // This customer's standing default discount percentage (2026-09-01)
        // -- auto-applied to a new Checkout bill the moment this customer is
        // selected (CheckoutViewModel.SelectedCustomer), editable per-bill
        // from there without changing this stored default. Edited from the
        // customer detail page (CustomerDetailViewModel), gated the same way
        // as every other sensitive customer-money field in this app.
        public double DiscountPercent { get => discountPercent; set => discountPercent = value; }
    }
}
