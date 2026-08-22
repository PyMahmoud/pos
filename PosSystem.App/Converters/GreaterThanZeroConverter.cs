using System;
using System.Globalization;
using System.Windows.Data;

namespace PosSystem.App.Converters
{
    /// <summary>
    /// Converts a numeric quantity to bool (true if greater than 0). Used in
    /// Checkout's item grid to disable "Add" and dim the card for
    /// out-of-stock goods, without needing a separate IsAvailable flag on
    /// the Goods model.
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return d > 0;
            if (value is int i) return i > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
