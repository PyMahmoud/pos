using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace PosSystem.Core.Reporting
{
    /// <summary>
    /// Writes a real .xlsx workbook (via ClosedXML — writes OpenXML
    /// directly, no Microsoft Excel installation required on the machine
    /// running this, unlike the Microsoft.Office.Interop.Excel package
    /// still sitting unused in this solution's packages folder from the
    /// original repo) covering every bill (and its line items) whose date
    /// falls within [startDate, endDate] inclusive. Three sheets:
    ///
    /// - Summary: report title, the date range actually covered, total
    ///   revenue/profit/transaction count, and a Cash/Card/Pay Later
    ///   breakdown by bill total — same shape as Dashboard's own KPI cards
    ///   and payment-split chart, just as flat numbers instead of a chart,
    ///   since a spreadsheet export is read later/offline, not watched live.
    /// - Bills: one row per bill (number, date, time, customer, phone,
    ///   payment method, payment status, an item summary, subtotal,
    ///   discount, tax, total, paid, remaining).
    /// - Sales Detail: one row per sold line item across every bill in
    ///   range (bill #, date, product, category, quantity, unit price/cost,
    ///   line total, profit, returned) — the level of detail an accountant
    ///   or the client himself would actually need to audit a period, not
    ///   just see totals.
    ///
    /// Reads via the same Data.Bills/Data.Sells classes every other screen
    /// uses (ReadBills/ReadPendingSell — the latter despite its name reads
    /// the whole `sells` table, see Dashboard's own use of it) rather than
    /// writing new date-filtered SQL — this app's tables are small enough
    /// (a pharma distributor's daily volume, not a supermarket chain's)
    /// that reading everything and filtering in memory, exactly like
    /// DashboardViewModel already does for its own date-range filter, costs
    /// nothing measurable and reuses proven, already-correct date-parsing
    /// logic (Datex is always "dd/MM/yyyy", written that way at sale time
    /// by CheckoutViewModel.CompleteSale) instead of duplicating a second,
    /// SQL-side date filter that would need to agree with it exactly.
    ///
    /// Sales Detail is filtered by BILL NUMBER membership in the in-range
    /// bill set, not by re-parsing each sell row's own Datex — a sell's
    /// Datex is always written identical to its parent bill's Datex at sale
    /// time (see CompleteSale — both come from the same `now`), so the two
    /// filters are equivalent in every real case; keying off the bill set
    /// once is simpler than repeating the same date-range check a second
    /// time in a way that could in principle drift out of sync with it.
    /// </summary>
    public static class SalesExportService
    {
        /// <summary>
        /// Builds the workbook and saves it to outputPath (parent folder
        /// created if needed). labels is optional — omitting it exports
        /// with English headers (SalesExportLabels' own defaults); the App
        /// layer normally builds one from whichever language is currently
        /// active (see SalesExportLabels' class doc comment for why that
        /// happens there, not here).
        /// </summary>
        public static void Export(DateTime startDate, DateTime endDate, string outputPath, SalesExportLabels labels = null)
        {
            labels = labels ?? new SalesExportLabels();

            var billsData = new Data.Bills();
            var sellsData = new Data.Sells();

            DateTime start = startDate.Date;
            DateTime end = endDate.Date;

            // Filtered to IsCurrent = 1 (2026-08-28, receipt revisioning --
            // see DatabaseBootstrapper's matching comment), same reasoning
            // as DashboardViewModel/CustomerDetailViewModel's matching
            // filters: a returned bill's original row stays in `bills` as
            // history, so an unfiltered export would report a returned
            // sale's revenue/profit twice -- once from the now-superseded
            // original, once from its replacement receipt. Sales Detail is
            // matched by BillId, not Billnumber, for the same reason those
            // two ViewModels' filters are -- a superseded bill's own line
            // items share their replacement's Billnumber, so a Billnumber
            // filter alone couldn't tell the two apart.
            var billsInRange = billsData.ReadBills("bills")
                .Where(b => b.IsCurrent && TryParseDatex(b.Datex, out DateTime d) && d.Date >= start && d.Date <= end)
                .OrderBy(b => b.Billnumber)
                .ToList();

            var billIdsInRange = new HashSet<int>(billsInRange.Select(b => b.Id));
            var sellsInRange = sellsData.ReadPendingSell("sells")
                .Where(s => billIdsInRange.Contains(s.BillId))
                .OrderBy(s => s.Billnumber)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                BuildSummarySheet(workbook, labels, billsInRange, sellsInRange, start, end);
                BuildBillsSheet(workbook, labels, billsInRange, sellsInRange);
                BuildSalesDetailSheet(workbook, labels, sellsInRange);

                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                workbook.SaveAs(outputPath);
            }
        }

        private static void BuildSummarySheet(
            XLWorkbook workbook, SalesExportLabels labels,
            List<Models.Bills> billsInRange, List<Models.Sells> sellsInRange,
            DateTime start, DateTime end)
        {
            var ws = workbook.Worksheets.Add(labels.SummarySheetName);
            int row = 1;

            ws.Cell(row, 1).Value = labels.ReportTitle;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 16;
            row += 2;

            ws.Cell(row, 1).Value = labels.DateRangeLabel;
            ws.Cell(row, 2).Value = start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " \u2013 " + end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            row++;
            ws.Cell(row, 1).Value = labels.GeneratedLabel;
            ws.Cell(row, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            row += 2;

            double totalRevenue = sellsInRange.Sum(s => s.Price * s.Quantity);
            double totalProfit = sellsInRange.Sum(s => s.Earned);
            int totalTransactions = billsInRange.Count;

            // Bills.Details stores the payment tag written at sale time
            // (CheckoutViewModel.CompleteSale: "Cash" / "Card" / "Credit" —
            // "Credit" is the stored tag for what the UI calls Pay Later
            // throughout, same mapping Dashboard's own payment-split chart
            // uses).
            double cashTotal = billsInRange.Where(b => b.Details == "Cash").Sum(b => b.Billcost);
            double cardTotal = billsInRange.Where(b => b.Details == "Card").Sum(b => b.Billcost);
            double payLaterTotal = billsInRange.Where(b => b.Details == "Credit").Sum(b => b.Billcost);

            void WriteMetric(string label, double value, string numberFormat)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 1).Style.Font.Bold = true;
                var cell = ws.Cell(row, 2);
                cell.Value = value;
                cell.Style.NumberFormat.Format = numberFormat;
                row++;
            }

            WriteMetric(labels.TotalRevenueLabel, totalRevenue, "#,##0.00");
            WriteMetric(labels.TotalProfitLabel, totalProfit, "#,##0.00");
            WriteMetric(labels.TotalTransactionsLabel, totalTransactions, "#,##0");
            WriteMetric(labels.CashTotalLabel, cashTotal, "#,##0.00");
            WriteMetric(labels.CardTotalLabel, cardTotal, "#,##0.00");
            WriteMetric(labels.PayLaterTotalLabel, payLaterTotal, "#,##0.00");

            if (billsInRange.Count == 0)
            {
                row++;
                ws.Cell(row, 1).Value = labels.NoDataMessage;
            }

            ws.Columns().AdjustToContents();
        }

        private static void BuildBillsSheet(XLWorkbook workbook, SalesExportLabels labels, List<Models.Bills> billsInRange, List<Models.Sells> sellsInRange)
        {
            var ws = workbook.Worksheets.Add(labels.BillsSheetName);

            string[] headers =
            {
                labels.ColBillNumber, labels.ColDate, labels.ColTime, labels.ColCustomer, labels.ColPhone,
                labels.ColPaymentMethod, labels.ColPaymentStatus, labels.ColItems,
                labels.ColSubtotal, labels.ColDiscount, labels.ColTax, labels.ColTotal,
                labels.ColPaid, labels.ColRemaining
            };
            WriteHeaderRow(ws, headers);

            // One "Panadol x2, Amoxicillin x1" string per bill, built once
            // up front rather than re-filtering sellsInRange per bill row —
            // same reasoning as billNumbersInRange above in Export().
            var itemsByBill = sellsInRange
                .GroupBy(s => s.Billnumber)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(s => FormatQuantity(s.Quantity) + "x " + s.Name)));

            int row = 2;
            foreach (var bill in billsInRange)
            {
                ws.Cell(row, 1).Value = bill.Billnumber;
                WriteDateCell(ws.Cell(row, 2), bill.Datex);
                ws.Cell(row, 3).Value = bill.Time;
                ws.Cell(row, 4).Value = string.IsNullOrWhiteSpace(bill.Ownername) ? labels.WalkInLabel : bill.Ownername;
                ws.Cell(row, 5).Value = bill.Ownernumber ?? "";
                ws.Cell(row, 6).Value = bill.Details;
                ws.Cell(row, 7).Value = PaymentStatusLabel(labels, bill);
                ws.Cell(row, 8).Value = itemsByBill.TryGetValue(bill.Billnumber, out string items) ? items : "";

                // Billcost is always subtotal + tax (see CheckoutViewModel.
                // CompleteSale and BillsBrowserViewModel.
                // RecomputeBillAfterLineChange — both compute it exactly
                // this way, so subtracting Tax back out here is exact, not
                // an approximation). Discount is reported separately
                // (currently always 0 — see SalesExportLabels.ColDiscount's
                // doc comment) rather than subtracted from Subtotal, since
                // Billcost/Total was never actually reduced by it.
                ws.Cell(row, 9).Value = bill.Billcost - bill.Tax;
                ws.Cell(row, 10).Value = bill.Discount;
                ws.Cell(row, 11).Value = bill.Tax;
                ws.Cell(row, 12).Value = bill.Billcost;
                ws.Cell(row, 13).Value = bill.Paid;
                ws.Cell(row, 14).Value = bill.Remain;
                for (int c = 9; c <= 14; c++) ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";

                row++;
            }

            FinishSheet(ws, headers.Length, billsInRange.Count);
        }

        /// <summary>
        /// Paid/Remain aren't stored as a status, just the two raw amounts
        /// (see BillsBrowserViewModel's own doc comment on why Bills.Paid/
        /// Remain are set once at InsertBills and never touched again
        /// elsewhere) — so status is derived here the same way
        /// CheckoutViewModel.CompleteSale decides Paid/Remain in the first
        /// place: Remain at or below 0 means it settled in full (Cash/Card,
        /// or a Pay Later bill paid down since), any Paid at all with
        /// Remain still outstanding means a partial payment happened after
        /// the sale, and zero Paid with Remain outstanding is an
        /// unpaid-since-creation Pay Later bill.
        /// </summary>
        private static string PaymentStatusLabel(SalesExportLabels labels, Models.Bills bill)
        {
            if (bill.Remain <= 0) return labels.PaymentStatusPaidLabel;
            if (bill.Paid > 0) return labels.PaymentStatusPartialLabel;
            return labels.PaymentStatusUnpaidLabel;
        }

        /// <summary>
        /// "2x" for a whole number, "2.5x" for a fractional quantity (some
        /// goods are sold by weight/volume, not just whole units — see
        /// Sells.Quantity's type, double not int) — trims the ".00" a plain
        /// ToString would otherwise put on every ordinary whole-unit sale.
        /// </summary>
        private static string FormatQuantity(double quantity) =>
            quantity == Math.Floor(quantity)
                ? quantity.ToString("0", CultureInfo.InvariantCulture)
                : quantity.ToString("0.##", CultureInfo.InvariantCulture);

        private static void BuildSalesDetailSheet(XLWorkbook workbook, SalesExportLabels labels, List<Models.Sells> sellsInRange)
        {
            var ws = workbook.Worksheets.Add(labels.SalesDetailSheetName);

            string[] headers =
            {
                labels.ColBillNumber, labels.ColDate, labels.ColProduct, labels.ColCategory,
                labels.ColQuantity, labels.ColUnitPrice, labels.ColUnitCost, labels.ColLineTotal, labels.ColProfit,
                labels.ColReturned
            };
            WriteHeaderRow(ws, headers);

            int row = 2;
            foreach (var sell in sellsInRange)
            {
                ws.Cell(row, 1).Value = sell.Billnumber;
                WriteDateCell(ws.Cell(row, 2), sell.Datex);
                ws.Cell(row, 3).Value = sell.Name;
                ws.Cell(row, 4).Value = sell.Category;

                var qtyCell = ws.Cell(row, 5);
                qtyCell.Value = sell.Quantity;
                qtyCell.Style.NumberFormat.Format = "#,##0.##";

                ws.Cell(row, 6).Value = sell.Price;
                ws.Cell(row, 7).Value = sell.Cost;
                ws.Cell(row, 8).Value = sell.Price * sell.Quantity;
                ws.Cell(row, 9).Value = sell.Earned;
                for (int c = 6; c <= 9; c++) ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";

                // Returned is stored as the literal string "Yes"/"No" on
                // the Sells row itself (see SalesExportLabels.ColReturned's
                // doc comment) -- normalized against the label set here
                // rather than writing the raw DB string straight through,
                // so a future Arabic export shows a translated word, not
                // English leaking into an otherwise fully localized sheet.
                ws.Cell(row, 10).Value = string.Equals(sell.Returned, "Yes", StringComparison.OrdinalIgnoreCase)
                    ? labels.ReturnedYesLabel
                    : labels.ReturnedNoLabel;

                row++;
            }

            FinishSheet(ws, headers.Length, sellsInRange.Count);
        }

        private static void WriteHeaderRow(IXLWorksheet ws, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDE7FA");
            }
        }

        /// <summary>
        /// Writes a real Excel date (sortable/filterable in Excel, not just
        /// date-shaped text) when datex parses as expected; falls back to
        /// the raw string on the rare row where it doesn't, rather than
        /// throwing and failing the whole export over one bad row.
        /// </summary>
        private static void WriteDateCell(IXLCell cell, string datex)
        {
            if (TryParseDatex(datex, out DateTime date))
            {
                cell.Value = date;
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
            }
            else
            {
                cell.Value = datex;
            }
        }

        private static void FinishSheet(IXLWorksheet ws, int columnCount, int rowCount)
        {
            if (rowCount > 0)
            {
                ws.SheetView.FreezeRows(1);
                ws.Range(1, 1, rowCount + 1, columnCount).SetAutoFilter();
            }
            ws.Columns().AdjustToContents();
        }

        private static bool TryParseDatex(string datex, out DateTime date) =>
            DateTime.TryParseExact(datex, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
