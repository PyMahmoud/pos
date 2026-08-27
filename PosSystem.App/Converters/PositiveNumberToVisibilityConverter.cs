using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PosSystem.App.Converters
{
    /// <summary>
    /// Converts a numeric value (double or int) to Visibility — Visible
    /// when greater than 0, Collapsed otherwise. Added 2026-08-26 for
    /// Checkout's Tax line (CheckoutViewModel.TaxAmount): shown only once
    /// AppSettings.TaxRatePercent is actually set to something above 0, so
    /// a shop that never touches that Settings field sees the exact same
    /// two-line Subtotal/Total cart summary this screen always had.
    ///
    /// Same shape as PositiveCountToVisibilityConverter (its int-Count
    /// counterpart for "show this section once there's data") but for a
    /// plain double/int value rather than a collection Count — kept as a
    /// separate small converter rather than widening that one's contract,
    /// consistent with this app's existing one-converter-per-purpose style
    /// (see GreaterThanZeroConverter's own narrow bool-only scope).
    /// </summary>
    public class PositiveNumberToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double number = value is double d ? d : value is int i ? i : 0;
            return number > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
