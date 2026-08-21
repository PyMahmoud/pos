using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using PosSystem.App.Assets;
using PosSystem.App.Views;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for MainWindow. Owns the sidebar's nav items and swaps
    /// CurrentView when the selection changes. Views are created lazily and
    /// cached on first visit, so switching tabs doesn't re-run each screen's
    /// setup (e.g. DashboardView's data load) every single click.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly Dictionary<NavItem, UserControl> _viewCache = new Dictionary<NavItem, UserControl>();

        public ObservableCollection<NavItem> NavItems { get; }

        private NavItem _selectedNavItem;
        public NavItem SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                if (!SetProperty(ref _selectedNavItem, value) || value == null) return;

                if (!_viewCache.TryGetValue(value, out var view))
                {
                    view = value.CreateView();
                    _viewCache[value] = view;
                }

                CurrentView = view;
            }
        }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        public MainViewModel()
        {
            NavItems = new ObservableCollection<NavItem>
            {
                new NavItem("Dashboard", IconGeometries.Dashboard, () => new DashboardView()),
                new NavItem("Checkout", IconGeometries.Checkout, () => new CheckoutView()),
                new NavItem("Customers", IconGeometries.Customers, () => new CustomersView()),
                new NavItem("Inventory", IconGeometries.Inventory, () => new InventoryView()),
            };

            // Dashboard first — matches the sidebar order in all three
            // reference images and gives staff an overview on launch.
            SelectedNavItem = NavItems[0];
        }
    }
}
