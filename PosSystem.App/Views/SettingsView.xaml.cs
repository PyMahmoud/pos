using System.ComponentModel;
using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    // Language toggling moved into SettingsViewModel.SetLanguageCommand
    // (2026-08-26, alongside the rest of the Appearance section) -- this
    // code-behind has nothing of its own to do anymore, kept only because
    // InitializeComponent() still needs a partial class to attach to.
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            // Re-lock all three admin gates on navigate-away (per Mahmoud's
            // explicit request) -- same Unloaded-event pattern as
            // DashboardView/InventoryView; see
            // SettingsViewModel.LockAllAdminSections' doc comment.
            Unloaded += (s, e) =>
            {
                if (DataContext is SettingsViewModel lockVm) lockVm.LockAllAdminSections();
            };

            // Admin password (#7, 2026-08-27) -- PasswordBox can't bind
            // Password directly (see DashboardView.xaml.cs's identical
            // comment for why), so both directions need code-behind: typing
            // pushes into the ViewModel below, and this subscription pulls
            // the other way -- when SaveAdminPasswordCommand resets
            // NewAdminPasswordInput/ConfirmAdminPasswordInput to "" after a
            // successful save, the two PasswordBox controls need to be
            // cleared too, or the visible boxes would silently keep
            // showing the just-saved password after the button click.
            //
            // BUG FIXED (found via Access Control's unlock box not clearing,
            // reported by Mahmoud with a screenshot -- same family of issue
            // as DashboardView's PasswordBox-can't-bind fix, but a
            // different root cause underneath): this used to subscribe via
            // DataContextChanged instead of the direct call below. That
            // never actually fired here, because DataContext for this view
            // is set inline via XAML (<UserControl.DataContext><vm:.../
            // ></UserControl.DataContext>), which InitializeComponent()
            // above already applies -- so by the time DataContextChanged
            // += ran, the one and only DataContext change had already
            // happened, and the event never fired again for the rest of
            // this control's lifetime. SettingsViewModel_PropertyChanged
            // was therefore never actually wired to anything, for ANY of
            // the five PasswordBoxes below, not just Access Control's -- it
            // just went unnoticed elsewhere because a successfully-unlocked
            // section's box gets hidden by its own Visibility binding
            // right after, so nobody saw the stale dots underneath. Fixed
            // by subscribing directly against the DataContext that's
            // already there, right here, instead of waiting for a change
            // event that will never come.
            if (DataContext is SettingsViewModel vm0)
            {
                vm0.PropertyChanged += SettingsViewModel_PropertyChanged;
            }

            // Kept as a defensive fallback in case DataContext is ever
            // reassigned to a different instance later in this control's
            // lifetime (it isn't today, but costs nothing to keep covered).
            DataContextChanged += (_, __) =>
            {
                if (DataContext is SettingsViewModel vm)
                {
                    vm.PropertyChanged += SettingsViewModel_PropertyChanged;
                }
            };
        }

        private void SettingsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is SettingsViewModel vm)) return;

            if (e.PropertyName == nameof(SettingsViewModel.NewAdminPasswordInput)
                && vm.NewAdminPasswordInput == "")
            {
                NewAdminPasswordBox.Password = "";
            }
            if (e.PropertyName == nameof(SettingsViewModel.ConfirmAdminPasswordInput)
                && vm.ConfirmAdminPasswordInput == "")
            {
                ConfirmAdminPasswordBox.Password = "";
            }
            // Export admin unlock (Phase 11 #3, 2026-08-28) -- same PasswordBox
            // two-way-sync need as the admin password fields above: a
            // successful unlock resets ExportUnlockPasswordInput to "" in
            // the ViewModel, and the visible box needs to follow.
            if (e.PropertyName == nameof(SettingsViewModel.ExportUnlockPasswordInput)
                && vm.ExportUnlockPasswordInput == "")
            {
                ExportUnlockPasswordBox.Password = "";
            }
            // Preferences and Admin Password section admin-unlock gates
            // (added per Mahmoud's request) -- same PasswordBox two-way-sync
            // need as the Export unlock box above.
            if (e.PropertyName == nameof(SettingsViewModel.PreferencesUnlockPasswordInput)
                && vm.PreferencesUnlockPasswordInput == "")
            {
                PreferencesUnlockPasswordBox.Password = "";
            }
            if (e.PropertyName == nameof(SettingsViewModel.AdminPasswordUnlockPasswordInput)
                && vm.AdminPasswordUnlockPasswordInput == "")
            {
                AdminPasswordUnlockPasswordBox.Password = "";
            }
            // Access Control section's admin-unlock gate (added per
            // Mahmoud's request) -- same PasswordBox two-way-sync need as
            // every other unlock box on this screen.
            if (e.PropertyName == nameof(SettingsViewModel.AccessControlUnlockPasswordInput)
                && vm.AccessControlUnlockPasswordInput == "")
            {
                AccessControlUnlockPasswordBox.Password = "";
            }
        }

        private void NewAdminPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.NewAdminPasswordInput = NewAdminPasswordBox.Password;
            }
        }

        private void ConfirmAdminPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.ConfirmAdminPasswordInput = ConfirmAdminPasswordBox.Password;
            }
        }

        private void ExportUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.ExportUnlockPasswordInput = ExportUnlockPasswordBox.Password;
            }
        }

        private void PreferencesUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.PreferencesUnlockPasswordInput = PreferencesUnlockPasswordBox.Password;
            }
        }

        private void AdminPasswordUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.AdminPasswordUnlockPasswordInput = AdminPasswordUnlockPasswordBox.Password;
            }
        }

        private void AccessControlUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.AccessControlUnlockPasswordInput = AccessControlUnlockPasswordBox.Password;
            }
        }
    }
}

