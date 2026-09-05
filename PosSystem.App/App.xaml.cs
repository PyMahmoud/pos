using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PosSystem.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Added 2026-08-30: without these, an unhandled exception
            // anywhere just silently kills the process — no message, no
            // log, nothing. That's especially bad under Wine, where the
            // native crash dialog only ever shows a generic 0xe0434352
            // ("some .NET exception happened") with no managed stack trace
            // at all. Registered before EnsureSchema/anything else so even
            // a very-early startup exception gets caught.
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // StartupUri was removed from App.xaml (Licensing-Plan.md,
            // Phase 5) — MainWindow is no longer created automatically.
            // OnExplicitShutdown here so ActivationWindow closing (below)
            // never triggers the app quitting on its own before
            // MainWindow gets a chance to open; switched to
            // OnMainWindowClose once a real license is confirmed and
            // MainWindow is actually up.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Runs BEFORE base.OnStartup — StartupUri makes the base call
            // create and show MainWindow synchronously, and MainWindow's
            // default screen (Dashboard) reads Bills/Customers immediately
            // in its constructor. Bootstrap has to finish first or the very
            // first launch after this update would hit the old "table/
            // column doesn't exist yet" error this was written to prevent.
            Core.Data.DatabaseBootstrapper.EnsureSchema();

            // Must run after EnsureSchema (needs the `settings` table to
            // exist) and before base.OnStartup (Dashboard/Inventory/Checkout
            // all read AppSettings values as soon as they're constructed —
            // see AppSettings' own class doc comment).
            AppSettings.Load();

            base.OnStartup(e);

            if (!EnsureLicensed())
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        /// <summary>
        /// Returns true once this machine has a valid license — either it
        /// already did, or the person just activated it successfully in
        /// ActivationWindow. Returns false if they closed/exited that
        /// window without activating, in which case the caller shuts the
        /// app down rather than falling through to MainWindow.
        /// </summary>
        private static bool EnsureLicensed()
        {
            Core.Licensing.Validation.LicenseValidationResult result =
                Core.Licensing.Validation.LicenseValidator.ValidateStoredLicense();

            if (result.IsValid)
            {
                return true;
            }

            var activationWindow = new Views.ActivationWindow();
            bool? activated = activationWindow.ShowDialog();
            return activated == true;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShow(e.Exception);
            e.Handled = true; // prevents the silent-kill default; we choose to shut down ourselves below
            Shutdown();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogAndShow(e.ExceptionObject as Exception);
        }

        private static void LogAndShow(Exception ex)
        {
            string message = ex?.ToString() ?? "Unknown error (non-Exception object thrown).";
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "RovaShop_crash.log");
                File.AppendAllText(logPath, DateTime.Now + Environment.NewLine + message + Environment.NewLine + new string('-', 40) + Environment.NewLine);
            }
            catch
            {
                // If we can't even write the log, fall through to the message box below.
            }

            try
            {
                MessageBox.Show(
                    "RovaShop POS hit an unexpected error and needs to close.\n\n" + message,
                    "RovaShop POS - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // MessageBox itself failing (e.g. no display) — nothing more we can do here.
            }
        }
    }
}
