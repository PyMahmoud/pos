using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PosSystem.App.Localization;

namespace PosSystem.App.Views
{
    // Fully static, bilingual reference content (Phase 11 #5, 2026-08-28) --
    // every string on this screen comes straight from Strings.English.xaml/
    // Strings.Arabic.xaml via DynamicResource, the same as every other
    // screen's labels, so there's nothing dynamic here needing a ViewModel
    // or DataContext -- no admin gate either, unlike most of Settings,
    // since "how do I use this" has no sensitive data to protect.
    //
    // Contact Support (Phase 11 #4, moved here from Settings 2026-08-28)
    // is the one interactive part of this screen. It has no ViewModel of
    // its own (see doc comment above), so the three buttons are plain
    // Click handlers here instead of ICommand bindings -- same tel:/
    // wa.me/mailto: launch approach Settings' original card used, just
    // without a ViewModel layer this otherwise-static screen doesn't need.
    // Process.Start(uri) alone is enough on .NET Framework 4.8 (unlike
    // .NET Core, ShellExecute is the default here) -- no ProcessStartInfo
    // needed to hand the URI to the OS's registered handler (dialer,
    // WhatsApp, default mail client).
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();
        }

        private void CallSupport_Click(object sender, RoutedEventArgs e)
        {
            TryLaunch("tel:" + ContactInfo.PhoneUri);
        }

        private void WhatsAppSupport_Click(object sender, RoutedEventArgs e)
        {
            TryLaunch("https://wa.me/" + ContactInfo.WhatsAppNumber);
        }

        private void EmailSupport_Click(object sender, RoutedEventArgs e)
        {
            TryLaunch("mailto:" + ContactInfo.SupportEmail);
        }

        private void TryLaunch(string uri)
        {
            try
            {
                Process.Start(uri);
                ContactErrorText.Text = "";
            }
            catch (Exception)
            {
                // No handler registered for tel:/wa.me/mailto: on this
                // machine (or the placeholder values in ContactInfo.cs are
                // still unset) -- surface it inline rather than crashing
                // the app over a support-contact button.
                ContactErrorText.Text = LocalizationManager.GetString("HelpContactError");
            }
        }
    }
}
