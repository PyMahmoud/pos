using System;
using System.Globalization;
using System.Windows.Data;
using PosSystem.App.Localization;

namespace PosSystem.App.Converters
{
    /// <summary>
    /// Converts a customer's Remain balance to the localized badge text on
    /// the Customers screen — "Paid up" at 0, "Owes 150.00" otherwise. Kept
    /// as a converter (rather than a bindable string on CustomerRow) so it
    /// re-resolves against whichever language is active at render time
    /// without CustomerRow needing to know about LocalizationManager at all.
    /// </summary>
    public class CustomerBalanceTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double remain = values.Length > 0 && values[0] is double d ? d : 0;

            if (remain <= 0) return LocalizationManager.GetString("CustomersBalancePaidUp");

            string format = LocalizationManager.GetString("CustomersBalanceOwes");
            return string.Format(format, remain);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
