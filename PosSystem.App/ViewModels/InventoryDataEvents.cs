using System;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// App-wide "the goods table changed" signal — same lightweight
    /// static-event pattern as <see cref="CustomerDataEvents"/> and
    /// <see cref="OrderEvents"/>. Needed for the same reason those exist:
    /// MainViewModel caches every screen's ViewModel forever after first
    /// visit, so InventoryViewModel has no other way to learn a Checkout
    /// sale just decremented stock while Inventory wasn't the active tab —
    /// and Checkout needs to know when Inventory adjusts a quantity
    /// directly, so its own cached goods list (and cart's MaxAvailable
    /// checks) don't go stale either.
    /// </summary>
    public static class InventoryDataEvents
    {
        public static event Action GoodsChanged;

        public static void RaiseGoodsChanged() => GoodsChanged?.Invoke();
    }
}
