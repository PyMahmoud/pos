using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PosSystem.App.Behaviors
{
    /// <summary>
    /// Attached behavior, not a full ScrollViewer ControlTemplate override —
    /// same "reach the standard control through its normal extension points
    /// instead of reproducing its whole part contract" reasoning as the
    /// ScrollBar/ComboBox work in CommonStyles.xaml, just for behavior
    /// instead of visuals this time.
    ///
    /// WPF's default mouse-wheel scroll is a flat ~48px (3 "lines") per
    /// notch regardless of how much content there is — fine for a short
    /// list, noticeably slow for a long one (Inventory's product grid,
    /// Checkout's item browser). AdaptiveWheel scales the per-notch
    /// distance by how much taller the content is than the visible
    /// viewport (ExtentHeight / ViewportHeight), capped both ends so a
    /// short list still feels like a normal scroll and a very long one
    /// doesn't start flying past content unreadably.
    ///
    /// Applied via CommonStyles.xaml's implicit ScrollViewer style, so
    /// every ScrollViewer in the app gets this automatically — including
    /// ones nested inside other controls' templates (a multi-line TextBox's
    /// PART_ContentHost, a ComboBox dropdown's internal ScrollViewer) —
    /// same reach-everything-for-free mechanism as the implicit ScrollBar
    /// style already uses. Harmless there: short content means the scale
    /// factor stays at its 1.0 floor, i.e. normal-speed scrolling.
    /// </summary>
    public static class ScrollBehavior
    {
        public static readonly DependencyProperty AdaptiveWheelProperty =
            DependencyProperty.RegisterAttached(
                "AdaptiveWheel", typeof(bool), typeof(ScrollBehavior),
                new PropertyMetadata(false, OnAdaptiveWheelChanged));

        public static void SetAdaptiveWheel(DependencyObject element, bool value) =>
            element.SetValue(AdaptiveWheelProperty, value);

        public static bool GetAdaptiveWheel(DependencyObject element) =>
            (bool)element.GetValue(AdaptiveWheelProperty);

        private static void OnAdaptiveWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ScrollViewer scrollViewer)) return;

            if ((bool)e.NewValue)
                scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            else
                scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        // Baseline matches WPF's own default (3 lines * ~16px = 48px per
        // notch) so a short list — where the scale factor bottoms out at
        // 1.0 — scrolls at the speed everyone already expects, not a
        // surprising new default.
        private const double BaselinePixelsPerNotch = 48;
        private const double MinScale = 1.0;
        private const double MaxScale = 6.0;

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;

            double extent = scrollViewer.ExtentHeight;
            double viewport = Math.Max(scrollViewer.ViewportHeight, 1);

            // Nothing to scroll — don't swallow the event, so a wheel tick
            // over an already-fully-visible ScrollViewer still bubbles up
            // to a parent that might actually need it (e.g. a short card's
            // internal ScrollViewer sitting inside a longer scrollable
            // page).
            if (extent <= viewport)
                return;

            double ratio = extent / viewport;
            double scale = Math.Min(Math.Max(ratio / 3.0, MinScale), MaxScale);
            double pixelsPerNotch = BaselinePixelsPerNotch * scale;

            double delta = -Math.Sign(e.Delta) * pixelsPerNotch;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + delta);
            e.Handled = true;
        }
    }
}
