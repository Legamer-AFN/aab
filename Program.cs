using System;
using System.Windows;

namespace AbyssXLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show("Une erreur inattendue est survenue : " + e.Exception.Message, "AbyssX Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
            app.Run(new MainWindow());
        }
    }
}
