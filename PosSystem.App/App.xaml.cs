using System.Windows;

namespace PosSystem.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Runs BEFORE base.OnStartup — StartupUri makes the base call
            // create and show MainWindow synchronously, and MainWindow's
            // default screen (Dashboard) reads Bills/Customers immediately
            // in its constructor. Bootstrap has to finish first or the very
            // first launch after this update would hit the old "table/
            // column doesn't exist yet" error this was written to prevent.
            Core.Data.DatabaseBootstrapper.EnsureSchema();

            base.OnStartup(e);
        }
    }
}
