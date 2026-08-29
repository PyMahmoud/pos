using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();

            // Re-lock on navigate-away (per Mahmoud's explicit request) --
            // Unloaded fires whenever this cached view leaves the visual
            // tree (i.e. MainViewModel.CurrentView switches to another
            // screen), even though the view/ViewModel instance itself is
            // cached for the app's lifetime. See DashboardViewModel.LockAdmin's
            // doc comment for the full reasoning.
            Unloaded += (s, e) =>
            {
                if (DataContext is DashboardViewModel vm) vm.LockAdmin();

                // LockAdmin() above clears the ViewModel's
                // UnlockPasswordInput string, but PasswordBox.Password
                // deliberately can't be data-bound (see the comment on
                // UnlockPasswordBox_PasswordChanged below), so clearing the
                // ViewModel property alone never touches what's actually
                // displayed in the box. Without this, the typed dots stay
                // visible next time this cached view is shown again, even
                // though the screen is correctly re-locked underneath.
                UnlockPasswordBox.Clear();
            };
        }

        // PasswordBox deliberately doesn't support a Password binding (a
        // real WPF security decision — plaintext passwords shouldn't sit in
        // a bindable dependency property visible to any snooping tool), so
        // this is the standard, documented way to wire one to a ViewModel:
        // push the value across in code-behind on PasswordChanged. Same
        // shape every WPF PasswordBox/MVVM guide uses — not a workaround
        // specific to this app.
        private void UnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm && sender is PasswordBox box)
            {
                vm.UnlockPasswordInput = box.Password;
            }
        }
    }
}

