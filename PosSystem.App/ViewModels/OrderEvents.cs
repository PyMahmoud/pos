using System;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// App-wide "a sale was just completed" signal — same lightweight
    /// static-event pattern CustomerDataEvents.CustomersChanged already
    /// uses. Needed for the same reason: MainViewModel caches every screen's
    /// ViewModel forever after first visit, so DashboardViewModel has no
    /// other way to learn a sale just happened on Checkout while Dashboard
    /// wasn't the active tab. This is what makes Phase 6's "event-driven,
    /// not a polling timer" requirement possible.
    /// </summary>
    public static class OrderEvents
    {
        public static event Action OrderCompleted;

        public static void RaiseOrderCompleted() => OrderCompleted?.Invoke();
    }
}
