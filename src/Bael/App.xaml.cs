using System.IO;
using System.Windows;

namespace Bael;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is not ["--smoke-test", var resultPath])
            return;

        StartupUri = null;
        var window = new MainWindow();
        MainWindow = window;
        window.ContentRendered += (_, _) =>
        {
            try
            {
                window.VerifyStartup();
                File.WriteAllText(resultPath, "OK");
                Shutdown(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(resultPath, error.ToString());
                Shutdown(1);
            }
        };
        window.Show();
    }
}
