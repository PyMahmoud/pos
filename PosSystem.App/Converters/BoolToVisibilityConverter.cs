using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PosSystem.App.Converters
{
    /// <summary>
    /// Standard bool-to-Visibility (true = Visible, false = Collapsed).
    /// Distinct from CountToVisibilityConverter, which is deliberately
    /// inverted (0 = Visible) for the "empty state" use case — this one is
    /// the plain, non-inverted version, used e.g. to show the Customers
    /// screen's "record a payment" row only when CustomerRow.HasDebt is true.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
