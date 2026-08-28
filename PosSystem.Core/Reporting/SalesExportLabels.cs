namespace PosSystem.Core.Reporting
{
    /// <summary>
    /// Every piece of display text SalesExportService writes into the
    /// workbook — sheet names, section headers, column headers. Core has
    /// no dependency on PosSystem.App's LocalizationManager (Core is meant
    /// to be UI/localization-agnostic, matching every other class in this
    /// namespace and its own class doc comments), so rather than hardcode
    /// English strings into the export logic itself, the caller (App layer
    /// — SettingsViewModel) builds one of these from the currently-active
    /// language and passes it in. Every property has a sensible English
    /// default so the service is still usable (e.g. from a future
    /// command-line/scheduled-report tool with no App layer at all)
    /// without a caller having to fill in all 25 fields just to get
    /// something working.
    /// </summary>
    public class SalesExportLabels
    {
        public string ReportTitle { get; set; } = "Sales Report";
        public string DateRangeLabel { get; set; } = "Date range";
        public string GeneratedLabel { get; set; } = "Generated";

        public string SummarySheetName { get; set; } = "Summary";
        public string TotalRevenueLabel { get; set; } = "Total Revenue";
        public string TotalProfitLabel { get; set; } = "Total Profit";
        public string TotalTransactionsLabel { get; set; } = "Total Transactions";
        public string CashTotalLabel { get; set; } = "Cash";
        public string CardTotalLabel { get; set; } = "Card";
        public string PayLaterTotalLabel { get; set; } = "Pay Later";
        public string NoDataMessage { get; set; } = "No sales in this date range.";

        public string BillsSheetName { get; set; } = "Bills";
        public string ColBillNumber { get; set; } = "Bill #";
        public string ColDate { get; set; } = "Date";
        public string ColTime { get; set; } = "Time";
        public string ColCustomer { get; set; } = "Customer";
        // Added alongside Discount/Items/Payment Status (see class doc
        // comment on this batch) -- Bills.Ownernumber was already read for
        // every bill but never surfaced in the export; a pharma
        // distributor visiting pharmacies in person wants the phone number
        // right next to the customer name, not a reason to flip back to
        // the Customers screen.
        public string ColPhone { get; set; } = "Phone";
        public string ColPaymentMethod { get; set; } = "Payment Method";
        // Derived from Paid vs Remaining, not a stored column -- see
        // SalesExportService.PaymentStatusFor's doc comment.
        public string ColPaymentStatus { get; set; } = "Payment Status";
        public string PaymentStatusPaidLabel { get; set; } = "Paid in Full";
        public string PaymentStatusPartialLabel { get; set; } = "Partial";
        public string PaymentStatusUnpaidLabel { get; set; } = "Unpaid";
        // One cell per bill, e.g. "Panadol x2, Amoxicillin x1" -- built
        // from the same Sales Detail rows that already exist on their own
        // sheet, so a quick look at the Bills sheet doesn't require
        // flipping to Sales Detail and filtering by bill number just to see
        // what was actually sold.
        public string ColItems { get; set; } = "Items";
        public string ColSubtotal { get; set; } = "Subtotal";
        // Bills.Discount exists and is read here, but every bill currently
        // writes 0 at sale time (CheckoutViewModel.CompleteSale -- no UI
        // sets a discount yet). Exported anyway so the column is already in
        // place, correct, and non-breaking the day a discount UI ships,
        // instead of this being a second change later.
        public string ColDiscount { get; set; } = "Discount";
        public string ColTax { get; set; } = "Tax";
        public string ColTotal { get; set; } = "Total";
        public string ColPaid { get; set; } = "Paid";
        public string ColRemaining { get; set; } = "Remaining";

        public string SalesDetailSheetName { get; set; } = "Sales Detail";
        public string ColProduct { get; set; } = "Product";
        public string ColCategory { get; set; } = "Category";
        public string ColQuantity { get; set; } = "Quantity";
        public string ColUnitPrice { get; set; } = "Unit Price";
        public string ColUnitCost { get; set; } = "Unit Cost";
        public string ColLineTotal { get; set; } = "Line Total";
        public string ColProfit { get; set; } = "Profit";
        // Sells.Returned is written "No" on every line at sale time and
        // never flipped elsewhere in this app today (no returns workflow
        // exists yet) -- surfaced anyway so a return doesn't silently read
        // as an ordinary sale in this sheet the day that workflow does
        // exist, and so this isn't a second export change when it ships.
        public string ColReturned { get; set; } = "Returned";
        public string ReturnedYesLabel { get; set; } = "Yes";
        public string ReturnedNoLabel { get; set; } = "No";

        // Walk-in bills store an empty Ownername (see CheckoutViewModel.
        // CompleteSale) — this is what the Customer column shows instead of
        // a blank cell, same "Walk-in" concept Checkout's own customer
        // picker and the Bills browser's list already use
        // (CheckoutWalkIn / BillsBrowserWalkInLabel).
        public string WalkInLabel { get; set; } = "Walk-in";
    }
}
