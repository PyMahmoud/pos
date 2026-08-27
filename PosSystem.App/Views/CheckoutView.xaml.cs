using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    public partial class CheckoutView : UserControl
    {
        public CheckoutView()
        {
            InitializeComponent();
        }

        // Bills browser admin unlock (#6, 2026-08-27/28) — same PasswordBox
        // -> ViewModel wiring as DashboardView/InventoryView's own unlock
        // boxes (see DashboardView.xaml.cs's comment for why PasswordBox
        // can't bind Password directly). Reads box.DataContext at event
        // time rather than capturing a fixed ViewModel reference, since
        // CheckoutViewModel.SelectedBillsBrowser is a fresh
        // BillsBrowserViewModel instance every time Bills is reopened (see
        // that property's doc comment) — the ancestor Grid's DataContext
        // binding keeps this PasswordBox's inherited DataContext pointed at
        // whichever instance is currently open. Unlike Dashboard/Inventory,
        // this box is NOT auto-cleared after a successful unlock (those two
        // do it via a DataContextChanged subscription against a
        // ViewModel that's cached for the app's lifetime — not a clean fit
        // here, since a fresh BillsBrowserViewModel appears each time the
        // browser reopens). Cosmetic only: a stray password glyph left
        // behind doesn't affect the unlock state itself.
        private void BillsAdminUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is PasswordBox box && box.DataContext is BillsBrowserViewModel vm)
            {
                vm.AdminUnlockPasswordInput = box.Password;
            }
        }
    }
}
