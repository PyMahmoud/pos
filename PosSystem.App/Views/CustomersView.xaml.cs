using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();
        }

        // Discount admin unlock (2026-09-01) -- same PasswordBox ->
        // ViewModel wiring as every other unlock box in this app (PasswordBox
        // can't bind Password directly). Reads box.DataContext at event time
        // rather than capturing a fixed ViewModel reference, since
        // CustomersViewModel.SelectedDetail is a fresh CustomerDetailViewModel
        // instance every time "View Details" is opened (see that class's own
        // doc comment) -- the ancestor Grid's DataContext binding keeps this
        // PasswordBox's inherited DataContext pointed at whichever instance is
        // currently open. Same reasoning as CheckoutView's
        // BillsAdminUnlockPasswordBox_PasswordChanged: not auto-cleared after
        // a successful unlock, since a fresh instance appears each time the
        // detail page reopens anyway -- cosmetic only if a stray glyph is
        // left behind.
        private void DiscountUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is PasswordBox box && box.DataContext is CustomerDetailViewModel vm)
            {
                vm.DiscountUnlockPasswordInput = box.Password;
            }
        }
    }
}
