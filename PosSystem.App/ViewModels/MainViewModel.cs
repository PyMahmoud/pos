using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using PosSystem.App.Assets;
using PosSystem.App.Views;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for MainWindow. Owns the sidebar's nav items and swaps
    /// CurrentView when the selection changes. Views are created lazily and
    /// cached on first visit, so switching tabs doesn't re-run each screen's
    /// setup (e.g. DashboardView's data load) every single click.
    ///
    /// Unsaved-Inventory-changes guard (added 2026-09-03, explicit
    /// request, alongside Inventory's staged-edits/Undo/Redo/Save Changes
    /// feature -- see InventoryViewModel's class doc comment on that
    /// staging model): switching the sidebar selection AWAY from
    /// Inventory while it has anything staged-but-not-yet-saved now blocks
    /// the switch and shows a Save/Discard/Stay confirmation
    /// (MainWindow.xaml's overlay, bound to UnsavedChangesPrompt) instead
    /// of silently losing those edits. Reaching into the cached Inventory
    /// View's DataContext to check this (SelectedNavItem's setter below)
    /// is a deliberate, narrow exception to "MainViewModel doesn't know
    /// about screen internals" -- NavItem's view-factory Func pattern
    /// gives MainViewModel no other way to ask "does the screen I'm
    /// leaving have a reason to block this" without a much larger
    /// cross-ViewModel messaging system this app doesn't have anywhere
    /// else, for a need that today is genuinely specific to Inventory
    /// alone.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly Dictionary<NavItem, UserControl> _viewCache = new Dictionary<NavItem, UserControl>();

        public ObservableCollection<NavItem> NavItems { get; }

        private NavItem _selectedNavItem;
        private NavItem _pendingNavItem;

        public NavItem SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                if (value == null || value == _selectedNavItem) return;

                // Checked against _selectedNavItem (the screen being LEFT),
                // using the OLD value -- this has to run before any
                // assignment to the backing field, unlike the plain
                // SetProperty this setter used before 2026-09-03.
                if (_selectedNavItem != null &&
                    _viewCache.TryGetValue(_selectedNavItem, out var currentView) &&
                    currentView is InventoryView inventoryView &&
                    inventoryView.DataContext is InventoryViewModel inventoryVm &&
                    inventoryVm.HasUnsavedChanges)
                {
                    _pendingNavItem = value;
                    UnsavedChangesPrompt = inventoryVm;

                    // Forces the sidebar ListBox's SelectedItem binding back
                    // to the CURRENT screen -- the backing field hasn't
                    // actually changed at this point, so re-raising
                    // PropertyChanged for the same getter value snaps the
                    // ListBox's visual selection back to where it was,
                    // rather than showing the just-clicked item selected
                    // while CurrentView silently never swapped underneath
                    // it.
                    OnPropertyChanged(nameof(SelectedNavItem));
                    return;
                }

                ApplyNavItem(value);
            }
        }

        private void ApplyNavItem(NavItem value)
        {
            _selectedNavItem = value;
            OnPropertyChanged(nameof(SelectedNavItem));

            if (!_viewCache.TryGetValue(value, out var view))
            {
                view = value.CreateView();
                _viewCache[value] = view;
            }

            CurrentView = view;
        }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        // Non-null while MainWindow.xaml's unsaved-changes confirmation
        // overlay should be showing -- holds the SAME InventoryViewModel
        // instance the popup's Save/Discard buttons act on directly
        // (ConfirmSaveCommand/ConfirmDiscardCommand below), rather than
        // MainViewModel duplicating Inventory's own save/discard logic.
        private InventoryViewModel _unsavedChangesPrompt;
        public InventoryViewModel UnsavedChangesPrompt
        {
            get => _unsavedChangesPrompt;
            private set
            {
                if (SetProperty(ref _unsavedChangesPrompt, value))
                    OnPropertyChanged(nameof(IsShowingUnsavedChangesPrompt));
            }
        }

        public bool IsShowingUnsavedChangesPrompt => UnsavedChangesPrompt != null;

        public ICommand ConfirmSaveCommand { get; }
        public ICommand ConfirmDiscardCommand { get; }
        public ICommand ConfirmCancelCommand { get; }

        private void ResolvePendingNavigation()
        {
            UnsavedChangesPrompt = null;
            var target = _pendingNavItem;
            _pendingNavItem = null;
            if (target != null) ApplyNavItem(target);
        }

        public MainViewModel()
        {
            // NavItem takes a string-resource key now (not a literal label),
            // so its Label re-resolves against whichever Strings.*.xaml
            // LocalizationManager currently has loaded. See NavItem.cs.
            NavItems = new ObservableCollection<NavItem>
            {
                new NavItem("NavDashboard", IconGeometries.Dashboard, () => new DashboardView()),
                new NavItem("NavCheckout", IconGeometries.Checkout, () => new CheckoutView()),
                new NavItem("NavCustomers", IconGeometries.Customers, () => new CustomersView()),
                new NavItem("NavInventory", IconGeometries.Inventory, () => new InventoryView()),
                new NavItem("NavSettings", IconGeometries.Settings, () => new SettingsView()),
                // Help (Phase 11 #5, 2026-08-28) — static bilingual how-to-
                // use guide, last in the sidebar since it's reference
                // material, not a daily-use screen like everything above it.
                new NavItem("NavHelp", IconGeometries.Help, () => new HelpView()),
            };

            ConfirmSaveCommand = new RelayCommand(_ =>
            {
                UnsavedChangesPrompt?.SaveChangesCommand.Execute(null);
                ResolvePendingNavigation();
            });
            ConfirmDiscardCommand = new RelayCommand(_ =>
            {
                UnsavedChangesPrompt?.DiscardChangesCommand.Execute(null);
                ResolvePendingNavigation();
            });
            ConfirmCancelCommand = new RelayCommand(_ =>
            {
                _pendingNavItem = null;
                UnsavedChangesPrompt = null;
            });

            // Dashboard first — matches the sidebar order in all three
            // reference images and gives staff an overview on launch.
            SelectedNavItem = NavItems[0];
        }
    }
}
