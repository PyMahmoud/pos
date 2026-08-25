using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PosSystem.App.Behaviors
{
    /// <summary>
    /// Drop-in replacement for the standard Primitives.Track used inside
    /// ScrollBar's ControlTemplate (see CommonStyles.xaml's implicit
    /// ScrollBar style). Added 2026-08-24 because Thumb.MinHeight alone
    /// wasn't enough to keep the thumb a reasonable minimum size on a very
    /// long scrollable list (Inventory's 281-product grid) — Track computes
    /// and arranges the thumb via its own internal proportional
    /// (viewport/extent) math and evidently doesn't defer to the child
    /// Thumb's MinHeight/MinWidth the way FrameworkElement layout normally
    /// would, based on what Mahmoud's screenshot showed (a thumb nowhere
    /// close to 64px despite that MinHeight setting). Rather than keep
    /// trusting a property that visibly wasn't taking effect, this
    /// sidesteps the question entirely: let the base Track do its normal
    /// layout first, then explicitly re-arrange the Thumb to a guaranteed
    /// minimum length afterward — no dependency on how Track's internal
    /// sizing algorithm treats Thumb's own size properties.
    ///
    /// Preserves relative scroll position across the re-arrange (reads back
    /// wherever the base class actually placed the thumb via TranslatePoint
    /// — real rendered coordinates, so this works regardless of
    /// IsDirectionReversed — rather than recomputing position from Value/
    /// Minimum/Maximum, which would need to duplicate Track's own value
    /// math to get right).
    /// </summary>
    public class MinLengthTrack : Track
    {
        public static readonly DependencyProperty MinThumbLengthProperty =
            DependencyProperty.Register(
                nameof(MinThumbLength), typeof(double), typeof(MinLengthTrack),
                new PropertyMetadata(64.0));

        public double MinThumbLength
        {
            get => (double)GetValue(MinThumbLengthProperty);
            set => SetValue(MinThumbLengthProperty, value);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Size result = base.ArrangeOverride(arrangeSize);

            if (Thumb == null) return result;

            bool isVertical = Orientation == Orientation.Vertical;
            double trackLength = isVertical ? arrangeSize.Height : arrangeSize.Width;
            double currentThumbLength = isVertical ? Thumb.ActualHeight : Thumb.ActualWidth;

            // Already big enough (short list, small extent/viewport ratio),
            // or the track itself is too short for the minimum to make
            // sense (e.g. a tiny popup) — leave the base class's layout
            // alone in either case.
            if (currentThumbLength >= MinThumbLength || trackLength <= MinThumbLength)
                return result;

            Point thumbTopLeft = Thumb.TranslatePoint(new Point(0, 0), this);
            double currentStart = isVertical ? thumbTopLeft.Y : thumbTopLeft.X;
            double maxStart = Math.Max(trackLength - currentThumbLength, 0.0001);
            double fraction = Math.Min(Math.Max(currentStart / maxStart, 0.0), 1.0);

            double newStart = fraction * Math.Max(trackLength - MinThumbLength, 0.0);

            Rect thumbRect = isVertical
                ? new Rect(0, newStart, arrangeSize.Width, MinThumbLength)
                : new Rect(newStart, 0, MinThumbLength, arrangeSize.Height);

            Thumb.Arrange(thumbRect);

            return result;
        }
    }
}
