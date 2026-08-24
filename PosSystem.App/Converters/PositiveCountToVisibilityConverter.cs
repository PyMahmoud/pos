using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PosSystem.App.Converters
{
    /// <summary>
    /// Converts a collection Count to Visibility — Visible when the count is
    /// greater than 0, Collapsed when it's 0. The inverse of
    /// CountToVisibilityConverter (which shows an "empty" message when
    /// Count is 0) — this one is for showing the actual data once there is
    /// some, e.g. CustomersView's sold-medications/stock-check tables.
    /// </summary>
    public class PositiveCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = value is int i ? i : 0;
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
