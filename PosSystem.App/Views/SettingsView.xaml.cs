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
    }
}

