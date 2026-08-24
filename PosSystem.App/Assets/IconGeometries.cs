using System.Windows.Media;

namespace PosSystem.App.Assets
{
    /// <summary>
    /// Small hand-authored flat icon outlines for the sidebar nav. Deliberately
    /// not using an icon font (Segoe MDL2 Assets isn't guaranteed present on
    /// very old Windows installs) or an external icon package (extra
    /// dependency weight on machines this app is meant to stay light on).
    /// Swap for a proper icon set later if the client wants something more
    /// polished — these exist so navigation is usable and legible today.
    /// 24x24 viewBox convention throughout.
    /// </summary>
    public static class IconGeometries
    {
        public static readonly Geometry Dashboard = Geometry.Parse(
            "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z");

        public static readonly Geometry Checkout = Geometry.Parse(
            "M6,8 H18 L17,20 H7 Z M9,8 V6 A3,3 0 0 1 15,6 V8");

        public static readonly Geometry Customers = Geometry.Parse(
            "M12,3 A3,3 0 1 1 12,9 A3,3 0 1 1 12,3 Z M5,20 C5,15 8,13 12,13 C16,13 19,15 19,20 Z");

        public static readonly Geometry Inventory = Geometry.Parse(
            "M4,7 L12,3 L20,7 L12,11 Z M4,7 V17 L12,21 V11 Z M20,7 V17 L12,21 M12,11 V21");

        // Sliders/equalizer glyph — three tracks with knobs at different
        // positions. Common, unambiguous "Settings" read at nav-icon size.
        public static readonly Geometry Settings = Geometry.Parse(
            "M4,6 L20,6 L20,8 L4,8 Z M12.5,7 A2.5,2.5 0 1 0 17.5,7 A2.5,2.5 0 1 0 12.5,7 Z " +
            "M4,11 L20,11 L20,13 L4,13 Z M6.5,12 A2.5,2.5 0 1 0 11.5,12 A2.5,2.5 0 1 0 6.5,12 Z " +
            "M4,16 L20,16 L20,18 L4,18 Z M12.5,17 A2.5,2.5 0 1 0 17.5,17 A2.5,2.5 0 1 0 12.5,17 Z");

        // Calendar/date glyph — added 2026-08-24 for the Dashboard's themed
        // DatePicker toggle button (replacing the stock system calendar
        // icon). Simple flat outline: rounded page with a header band and
        // two binder-ring tabs, same 24x24 convention as everything above.
        public static readonly Geometry Calendar = Geometry.Parse(
            "M6,3 V6 M18,3 V6 M4,8 H20 M4,6 A2,2 0 0 1 6,4 H18 A2,2 0 0 1 20,6 V19 A2,2 0 0 1 18,21 H6 A2,2 0 0 1 4,19 Z " +
            "M7,11 H10 V14 H7 Z");
    }
}
