using System;
using System.Windows;
using Core.Licensing.Fingerprint;
using Core.Licensing.Storage;
using Core.Licensing.Validation;

namespace PosSystem.App.Views
{
    /// <summary>
    /// See ActivationWindow.xaml's class-level comment for when/why this
    /// window shows up. Deliberately plain code-behind, not MVVM — this
    /// runs once (or rarely, on reactivation), outside MainViewModel's
    /// normal navigation.
    /// </summary>
    public partial class ActivationWindow : Window
    {
        private HardwareFingerprint _fingerprint;

        public ActivationWindow()
        {
            InitializeComponent();
            LoadFingerprint();
        }

        private void LoadFingerprint()
        {
            _fingerprint = FingerprintCollector.Collect();
            HashTextBox.Text = _fingerprint.ComputeHardAnchorHash();
            RawDetailsTextBlock.Text = _fingerprint.ToDisplayString();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(HashTextBox.Text);
                CopiedStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                // Clipboard access can fail (locked by another process,
                // remote desktop session quirks, etc.) — not worth a hard
                // error for a convenience action; the hash is still
                // visible and selectable by hand in the box above.
            }
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string blob = (LicenseBlobTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(blob))
            {
                ShowError((string)FindResource("ActivationErrorMissing"));
                return;
            }

            LicenseValidationResult result = LicenseValidator.Validate(blob);
            if (!result.IsValid)
            {
                ShowError((string)FindResource("ActivationErrorGeneric"));
                return;
            }

            try
            {
                LicenseStorage.Save(blob);
            }
            catch (Exception)
            {
                ShowError((string)FindResource("ActivationSaveError"));
                return;
            }

            DialogResult = true;
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }
    }
}
