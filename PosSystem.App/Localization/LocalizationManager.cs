using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PosSystem.App.Localization
{
    public enum AppLanguage
    {
        English,
        Arabic
    }

    /// <summary>
    /// Swaps the active Strings.*.xaml merged dictionary at runtime — the
    /// same pattern Theming/ThemeManager.cs uses for Colors.*.xaml. Both
    /// Strings.English.xaml and Strings.Arabic.xaml define the identical
    /// set of string keys, so any screen bound to those keys (via
    /// DynamicResource) updates the instant this runs, with no per-screen
    /// language logic needed anywhere else in the app.
    ///
    /// Also flips the app-wide FlowDirection resource, since Arabic is a
    /// right-to-left language and the whole layout needs to mirror, not
    /// just the text — WPF handles most of that mirroring automatically
    /// once FlowDirection is set on a subtree's root element.
    /// </summary>
    public static class LocalizationManager
    {
        public static AppLanguage Current { get; private set; } = AppLanguage.English;

        public static event Action<AppLanguage> LanguageChanged;

        public static void SwitchLanguage(AppLanguage language)
        {
            var uri = language == AppLanguage.Arabic
                ? new Uri("Localization/Strings.Arabic.xaml", UriKind.Relative)
                : new Uri("Localization/Strings.English.xaml", UriKind.Relative);

            var appResources = Application.Current.Resources.MergedDictionaries;

            var existingStringsDict = appResources.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("Localization/Strings."));

            var newDict = new ResourceDictionary { Source = uri };

            if (existingStringsDict != null)
            {
                var index = appResources.IndexOf(existingStringsDict);
                appResources[index] = newDict;
            }
            else
            {
                appResources.Add(newDict);
            }

            Application.Current.Resources["AppFlowDirection"] =
                language == AppLanguage.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            Thread.CurrentThread.CurrentUICulture = language == AppLanguage.Arabic
                ? CultureInfo.GetCultureInfo("ar")
                : CultureInfo.GetCultureInfo("en-US");

            Current = language;
            LanguageChanged?.Invoke(language);
        }

        public static void Toggle()
        {
            SwitchLanguage(Current == AppLanguage.English ? AppLanguage.Arabic : AppLanguage.English);
        }

        /// <summary>
        /// Resolves a string key against the currently active Strings.*.xaml
        /// dictionary. Used by NavItem.Label, which can't use a XAML
        /// DynamicResource binding directly since it's a plain C# property,
        /// not a dependency property.
        /// </summary>
        public static string GetString(string key)
        {
            return Application.Current.Resources[key] as string ?? key;
        }
    }
}
