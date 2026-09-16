using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PosSystem.App.Behaviors
{
    /// <summary>
    /// Attached behavior, same shape as ScrollBehavior/MinLengthTrack in this
    /// folder — reach TextBox through its normal PreviewTextInput extension
    /// point instead of a custom control. Added 2026-09-10,
    /// Reference-Repo-Features-Plan.md item #4, ported (as a concept, not a
    /// code port — different TFM) from mohamedelareeg/POS's equivalent.
    ///
    /// Every numeric field in this app (DiscountPercentInput, Cost, Price,
    /// TaxRatePercentInput, LowStockThresholdInput, MinStock, ...) already
    /// validates on change via double.TryParse in its ViewModel — that stays
    /// exactly as-is and is still the real safety net (it's what catches
    /// paste and IME input, which this behavior deliberately does NOT
    /// intercept). What this adds is purely a UX improvement layered on top:
    /// stop an obviously-invalid keystroke (a letter, a second decimal
    /// point, a space) from landing in the box in the first place, so the
    /// cashier isn't typing "12a.5" and watching it get silently rejected
    /// three characters later.
    ///
    /// Attach via Behaviors:DecimalTextBoxBehavior.AllowDecimalPoint="True"
    /// (or "False" for a field that should never take a decimal point at
    /// all, e.g. a pure whole-number count) — setting the property to
    /// either value is what turns the filtering on for that TextBox; there
    /// is no separate on/off switch.
    /// </summary>
    public static class DecimalTextBoxBehavior
    {
        public static readonly DependencyProperty AllowDecimalPointProperty =
            DependencyProperty.RegisterAttached(
                "AllowDecimalPoint", typeof(bool), typeof(DecimalTextBoxBehavior),
                new PropertyMetadata(true, OnAllowDecimalPointChanged));

        public static void SetAllowDecimalPoint(DependencyObject element, bool value) =>
            element.SetValue(AllowDecimalPointProperty, value);

        public static bool GetAllowDecimalPoint(DependencyObject element) =>
            (bool)element.GetValue(AllowDecimalPointProperty);

        private static void OnAllowDecimalPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox textBox)) return;

            // Re-attaching (e.g. the property value flips at runtime) would
            // otherwise stack a second handler rather than replace the
            // first -- unsubscribe unconditionally before the conditional
            // subscribe below so this stays idempotent no matter how many
            // times the property changes.
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewTextInput += OnPreviewTextInput;
        }

        // Digits only, or digits with at most one decimal point -- matched
        // against the PROSPECTIVE resulting text (current text with the
        // selection replaced by the incoming keystroke), not just the
        // keystroke in isolation, so typing "." when the box already
        // contains "12.5" is correctly rejected (a second decimal point)
        // even though "." alone would pass a keystroke-only digit check.
        private static readonly Regex DigitsOnly = new Regex(@"^\d*$");
        private static readonly Regex DigitsWithOptionalPoint = new Regex(@"^\d*(\.\d*)?$");

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = (TextBox)sender;
            bool allowDecimalPoint = GetAllowDecimalPoint(textBox);

            string prospectiveText = textBox.Text
                .Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, e.Text);

            Regex pattern = allowDecimalPoint ? DigitsWithOptionalPoint : DigitsOnly;
            e.Handled = !pattern.IsMatch(prospectiveText);
        }
    }
}
