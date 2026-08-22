using System.Windows;
using System.Windows.Controls;
using PosSystem.App.Localization;

namespace PosSystem.App.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void LanguageToggleButton_Click(object sender, RoutedEventArgs e)
        {
            LocalizationManager.Toggle();
        }
    }
}
