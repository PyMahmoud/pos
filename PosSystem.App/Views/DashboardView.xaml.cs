using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
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

