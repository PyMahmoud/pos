namespace PosSystem.Core.Models
{
    /// <summary>
    /// One row in a customer's payment/credit history (`payments` table —
    /// see DatabaseBootstrapper for the schema). Two purposes drove this
    /// shape:
    ///
    /// 1. "Money we owe the customer" (CreditOwed on Customers) can arise
    ///    two ways — Type "Payment" when a customer pays more than their
    ///    Remain balance (the excess becomes credit instead of being
    ///    silently discarded, which is what the old RecordPayment capping
    ///    behavior did), and Type "Credit" for a manual entry (e.g. a
    ///    refund or goodwill credit) that isn't tied to any payment at all.
    ///
    /// 2. Revert support — Amount/AppliedToRemain/AppliedToCredit are what
    ///    a revert actually needs: reverting is done by SUBTRACTING this
    ///    row's effect back out of the customer's CURRENT balances
    ///    (CustomerDetailViewModel.RevertPayment), not by restoring the
    ///    Previous* snapshot below wholesale — subtracting stays correct
    ///    even if payments are reverted out of order, where restoring an
    ///    absolute snapshot from row N would silently undo whatever
    ///    happened in row N+1 too. Previous* is kept purely for the
    ///    history display (what the balance was right before this row),
    ///    not read by the revert logic itself.
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }

        // "Payment" (recorded from the Customers list's payment box — may
        // partially or fully overpay the Remain balance) or "Credit" (a
        // manual credit entry from the customer detail page, never tied to
        // an actual payment).
        public string Type { get; set; }

        public double Amount { get; set; }
        public double AppliedToRemain { get; set; }
        public double AppliedToCredit { get; set; }

        public double PreviousPaid { get; set; }
        public double PreviousRemain { get; set; }
        public double PreviousCredit { get; set; }

        public bool IsReverted { get; set; }
        public string Notes { get; set; }
        public string PaymentDate { get; set; }
        public string PaymentTime { get; set; }
    }
}
