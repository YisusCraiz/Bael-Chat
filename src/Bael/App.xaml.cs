using System.IO;
using System.Windows;

namespace Bael;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is ["--smoke-test", var resultPath])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var window = new MainWindow();
                window.VerifyStartup();
                window.Close();
                File.WriteAllText(resultPath, "OK");
                Shutdown(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(resultPath, error.ToString());
                Shutdown(1);
            }

            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
