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
    }
}
