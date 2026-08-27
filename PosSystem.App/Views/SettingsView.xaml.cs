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
    }
}

