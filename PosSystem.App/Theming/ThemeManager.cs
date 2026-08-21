using System;
using System.Linq;
using System.Windows;

namespace PosSystem.App.Theming
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    /// <summary>
    /// Swaps the active Colors.*.xaml merged dictionary at runtime. Both
    /// Colors.Light.xaml and Colors.Dark.xaml define the identical set of
    /// brush keys (PrimaryBrush, SurfaceBrush, etc.), so any screen bound to
    /// those keys repaints automatically the moment this runs — no per-screen
    /// theme logic needed anywhere else in the app.
    /// </summary>
    public static class ThemeManager
    {
        public static AppTheme Current { get; private set; } = AppTheme.Light;

        public static event Action<AppTheme> ThemeChanged;

        public static void SwitchTheme(AppTheme theme)
        {
            var uri = theme == AppTheme.Dark
                ? new Uri("Themes/Colors.Dark.xaml", UriKind.Relative)
                : new Uri("Themes/Colors.Light.xaml", UriKind.Relative);

            var appResources = Application.Current.Resources.MergedDictionaries;

            var existingColorsDict = appResources.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("Themes/Colors."));

            var newDict = new ResourceDictionary { Source = uri };

            if (existingColorsDict != null)
            {
                var index = appResources.IndexOf(existingColorsDict);
                appResources[index] = newDict;
            }
            else
            {
                appResources.Add(newDict);
            }

            Current = theme;
            ThemeChanged?.Invoke(theme);
        }

        public static void Toggle()
        {
            SwitchTheme(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
    }
}
