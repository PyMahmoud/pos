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
            // pull-direction sync SettingsView.xaml.cs does for its two
            // admin-password boxes, see that file's comment.
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
