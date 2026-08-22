using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using PosSystem.App.Localization;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One entry in the sidebar (Dashboard, Checkout, Customers, Inventory,
    /// Settings). Label resolves live from the current language's
    /// Strings.*.xaml dictionary (via LocalizationManager.GetString) and
    /// raises PropertyChanged whenever LocalizationManager.Toggle() runs, so
    /// the sidebar's ListBox — bound to Label — updates immediately without
    /// rebuilding NavItems or touching the selected screen.
    /// </summary>
    public class NavItem : INotifyPropertyChanged
    {
        private readonly string _labelKey;

        public string Label => LocalizationManager.GetString(_labelKey);
        public Geometry IconData { get; }

        private readonly Func<UserControl> _viewFactory;

        public event PropertyChangedEventHandler PropertyChanged;

        public NavItem(string labelKey, Geometry iconData, Func<UserControl> viewFactory)
        {
            _labelKey = labelKey;
            IconData = iconData;
            _viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));

            // NavItems live for the app's whole lifetime (cached once in
            // MainViewModel), so this subscription is never unhooked — that's
            // fine, it's not a per-instance leak.
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(AppLanguage _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));

        public UserControl CreateView() => _viewFactory();
    }
}
