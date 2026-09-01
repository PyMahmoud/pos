using System.ComponentModel;
using System.Windows.Controls;
using PosSystem.App.ViewModels;

namespace PosSystem.App.Views
{
    public partial class CheckoutView : UserControl
    {
        public CheckoutView()
        {
            InitializeComponent();

            // Discount admin gate (2026-09-01) -- re-lock on navigate-away,
            // same Unloaded-event pattern as Dashboard/Inventory/Settings;
            // see CheckoutViewModel.LockDiscountAdmin's doc comment.
            Unloaded += (s, e) =>
            {
                if (DataContext is CheckoutViewModel vm) vm.LockDiscountAdmin();
            };

            // Same PasswordBox-can't-bind-Password two-way-sync need as
            // Settings' own unlock boxes (see SettingsView.xaml.cs's
            // matching comment for the full reasoning) -- CheckoutViewModel
            // is cached for the app's lifetime (MainViewModel's view
            // cache), so a successful unlock resetting
            // DiscountUnlockPasswordInput to "" needs this box cleared to
            // match, or a stray password glyph would sit behind the box's
            // own Visibility binding until it's shown again.
            if (DataContext is CheckoutViewModel vm0)
            {
                vm0.PropertyChanged += CheckoutViewModel_PropertyChanged;
            }
        }

        private void CheckoutViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is CheckoutViewModel vm)) return;
            if (e.PropertyName == nameof(CheckoutViewModel.DiscountUnlockPasswordInput)
                && vm.DiscountUnlockPasswordInput == "")
            {
                DiscountUnlockPasswordBox.Password = "";
            }
        }

        // Discount admin unlock (2026-09-01) -- same PasswordBox -> ViewModel
        // wiring as every other unlock box in this app (PasswordBox can't
        // bind Password directly).
        private void DiscountUnlockPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is CheckoutViewModel vm)
            {
                vm.DiscountUnlockPasswordInput = DiscountUnlockPasswordBox.Password;
            }
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
