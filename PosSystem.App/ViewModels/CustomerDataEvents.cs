using System;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// App-wide "a customer's Paid/Remain changed" signal — same lightweight
    /// static-event pattern LocalizationManager.LanguageChanged already
    /// uses. Needed because MainViewModel caches every screen's view (and
    /// its ViewModel) forever after first visit, so CustomersViewModel has
    /// no other way to learn that a Pay Later sale on the Checkout screen
    /// just changed a customer's balance while Customers wasn't the active
    /// tab — and vice versa, Checkout needs to know when a new customer is
    /// added or a payment is recorded on the Customers screen, so its own
    /// customer picker doesn't go stale either.
    /// </summary>
    public static class CustomerDataEvents
    {
        public static event Action CustomersChanged;

        public static void RaiseCustomersChanged() => CustomersChanged?.Invoke();
    }
}
