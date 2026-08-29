using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PosSystem.App.Converters
{
    /// <summary>
    /// Inverted bool-to-Visibility (false = Visible, true = Collapsed) —
    /// added 2026-08-28 for receipt revisioning's "Edited" tag on a
    /// superseded bill in the Bills browser list (Core.Models.Bills.
    /// IsCurrent == false means "show the tag"). Distinct from the plain
    /// BoolToVisibilityConverter (true = Visible) rather than adding a
    /// ConverterParameter-based invert flag to that one, matching how
    /// CountToVisibilityConverter already exists as its own separate,
    /// deliberately-inverted sibling instead of a parameterized general
    /// converter — same pattern, same reasoning.
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            return flag ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
