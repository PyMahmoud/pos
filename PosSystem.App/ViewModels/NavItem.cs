using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One entry in the sidebar (Dashboard, Checkout, Customers, Inventory).
    /// Plain model, not a ViewModelBase — nothing here changes after
    /// construction, so it doesn't need INotifyPropertyChanged.
    /// </summary>
    public class NavItem
    {
        public string Label { get; }
        public Geometry IconData { get; }

        private readonly Func<UserControl> _viewFactory;

        public NavItem(string label, Geometry iconData, Func<UserControl> viewFactory)
        {
            Label = label;
            IconData = iconData;
            _viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
        }

        public UserControl CreateView() => _viewFactory();
    }
}
