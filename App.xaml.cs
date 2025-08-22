using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace PerspectiveKeyboard
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Global exception handling to avoid app crash without feedback
            DispatcherUnhandledException += (_, args) =>
            {
                try
                {
                    Debug.WriteLine($"[App] DispatcherUnhandledException: {args.Exception}");
                    MessageBox.Show(
                        $"An unexpected error occurred and was handled.\n\n{args.Exception.Message}",
                        "Unexpected Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    args.Handled = true;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args2) =>
            {
                var ex = args2.ExceptionObject as Exception;
                Debug.WriteLine($"[App] UnhandledException: {ex}");
            };

            TaskScheduler.UnobservedTaskException += (_, args3) =>
            {
                Debug.WriteLine($"[App] UnobservedTaskException: {args3.Exception}");
                args3.SetObserved();
            };

            base.OnStartup(e);
        }
    }
}
