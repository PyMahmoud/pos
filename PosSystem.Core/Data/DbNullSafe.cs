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
    }
}
