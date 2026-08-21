using System.Windows;
using PosSystem.App.Theming;

namespace PosSystem.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Throwaway proof that Colors.Light.xaml / Colors.Dark.xaml swap
        // cleanly at runtime. Move this into a real Settings screen once one
        // exists; ThemeManager.Toggle() itself is not throwaway.
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
        }
    }
}
