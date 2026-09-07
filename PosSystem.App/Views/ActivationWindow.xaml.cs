using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
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

        /// <summary>
        /// Loads a .lic file directly, bypassing copy/paste entirely --
        /// added after a real license got corrupted in transit when a
        /// terminal/chat app soft-wrapped the long base64 text and the
        /// wrap points became literal newlines on paste. Reading the file
        /// straight from disk sidesteps that whole class of problem.
        /// </summary>
        private void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            var dialog = new OpenFileDialog
            {
                Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                string content = System.IO.File.ReadAllText(dialog.FileName);
                string cleaned = new string(content.Where(c => !char.IsWhiteSpace(c)).ToArray());
                LicenseBlobTextBox.Text = cleaned;
            }
            catch (Exception ex)
            {
                ShowError("Couldn't read that file: " + ex.Message);
            }
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            // Strip ALL whitespace, not just leading/trailing (Trim()) --
            // pasting from a terminal or chat app that soft-wrapped this
            // long base64 text can turn wrap points into real embedded
            // newline characters once pasted into a multiline TextBox.
            // Base64 never contains whitespace, so stripping every
            // whitespace character anywhere in the string is always safe
            // and can't corrupt a valid blob -- it only fixes broken ones.
            string blob = new string((LicenseBlobTextBox.Text ?? string.Empty)
                .Where(c => !char.IsWhiteSpace(c)).ToArray());

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
