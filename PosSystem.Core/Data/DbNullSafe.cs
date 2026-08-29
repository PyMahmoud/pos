using System;

namespace PosSystem.Core.Data
{
    /// <summary>
    /// SQLite returns DBNull.Value (not C# null) for a null cell.
    /// Convert.ToInt32/ToDouble throw InvalidCastException on that
    /// ("Object cannot be cast from DBNull to other types") — every
    /// ReadXxx() method in this folder used raw Convert.ToInt32/ToDouble
    /// directly on reader/row values, so any row with an unexpected null in
    /// a numeric column crashes the whole read, not just that row.
    /// Use these instead of Convert.ToInt32/ToDouble wherever a column
    /// might legitimately (or accidentally) be null.
    /// </summary>
    public static class DbNullSafe
    {
        public static int ToInt32(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

        public static double ToDouble(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToDouble(value);

        public static string ToStringSafe(object value) =>
            value == null || value == DBNull.Value ? "" : value.ToString();

        // Added for bills.CustomerId (nullable FK -- a bill may or may not
        // be linked to a customer, see CheckoutViewModel.CompleteSale).
        public static int? ToNullableInt32(object value) =>
            value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);

        // Added 2026-08-28 for bills.IsCurrent (receipt revisioning, see
        // BillsBrowserViewModel's class doc comment) -- stored as SQLite
        // INTEGER 0/1, same as every other boolean-ish column in this app's
        // schema (there's no real BOOLEAN type in SQLite). Defaults to true
        // on a null cell: DatabaseBootstrapper's one-time backfill already
        // sets every pre-existing bill's IsCurrent to 1 right after adding
        // the column, so a null should only ever be seen, in practice,
        // before that backfill has run once -- treating it as "current" in
        // that narrow window is the safer default (a receipt silently
        // vanishing from the Bills list/Dashboard would be a worse bug than
        // one briefly still showing as current).
        public static bool ToBool(object value) =>
            value == null || value == DBNull.Value || Convert.ToInt32(value) != 0;
    }
}
