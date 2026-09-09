using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();

            // Re-lock on navigate-away (per Mahmoud's explicit request) --
            // same pattern as DashboardView.xaml.cs; see
            // InventoryViewModel.LockAdmin's doc comment.
            Unloaded += (s, e) =>
            {
                if (DataContext is InventoryViewModel vm) vm.LockAdmin();
            };

            // Clears the visible PasswordBox after a successful unlock
            // (ViewModel resets AdminUnlockPasswordInput to "") -- same
            // pull-direction sync SettingsView.xaml.cs does for its
            // admin-password boxes, see that file's comment.
            //
            // BUG FIXED (same root cause found and fixed in
            // SettingsView.xaml.cs -- see that file's constructor comment
            // for the full explanation): this used to subscribe only via
            // DataContextChanged, which never actually fires here because
            // DataContext is set inline via XAML
            // (<UserControl.DataContext><vm:InventoryViewModel/>
            // </UserControl.DataContext>) during InitializeComponent()
            // above, before this subscription ever runs -- so the handler
            // below was never actually wired to anything, and
            // AdminUnlockPasswordBox never cleared on re-lock. Fixed by
            // subscribing directly against the DataContext that's already
            // there.
            if (DataContext is InventoryViewModel vm0)
            {
                vm0.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(InventoryViewModel.AdminUnlockPasswordInput)
                        && vm0.AdminUnlockPasswordInput == "")
                    {
                        AdminUnlockPasswordBox.Password = "";
                        // Bug fix (2026-09-09): the Discounts overlay's own
                        // unlock box (added alongside that overlay's admin-
                        // unlock fix) shares this same AdminUnlockPasswordInput
                        // property with the main panel's box above -- both
                        // PasswordBoxes write into it via the same sender-cast
                        // handler below, but only the main box was ever being
                        // cleared back out on a successful unlock. Harmless in
                        // practice today (the overlay's unlock panel collapses
                        // the instant IsAdminLocked flips, hiding the box right
                        // along with the stale text), but leaving it uncleared
                        // meant the old password would still be sitting there,
                        // visible, the next time that panel re-appears after a
                        // re-lock -- same reasoning as clearing the main box.
                        DiscountsAdminUnlockPasswordBox.Password = "";
                    }
                };
            }

            // Kept as a defensive fallback in case DataContext is ever
            // reassigned to a different instance later (it isn't today).
            DataContextChanged += (_, __) =>
            {
                if (DataContext is InventoryViewModel vm)
                {
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(InventoryViewModel.AdminUnlockPasswordInput)
                            && vm.AdminUnlockPasswordInput == "")
                        {
                            AdminUnlockPasswordBox.Password = "";
                            DiscountsAdminUnlockPasswordBox.Password = "";
                        }
                    };
                }
            };
        }

        // Admin unlock (#7, 2026-08-27 extension) -- same PasswordBox ->
        // ViewModel wiring as DashboardView.xaml.cs's UnlockPasswordBox_
        // PasswordChanged; see that file's comment for why PasswordBox
        // can't bind Password directly.
        private void AdminUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is InventoryViewModel vm && sender is PasswordBox box)
            {
                vm.AdminUnlockPasswordInput = box.Password;
            }
        }
    }
}
