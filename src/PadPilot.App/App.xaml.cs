using System.Threading;
using System.Windows;
using PadPilot.Core;

namespace PadPilot.App;

public partial class App : Application
{
    Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _single = new Mutex(true, "PadPilot.TekKopya", out bool first);
        if (!first)
        {
            MessageBox.Show("PadPilot zaten çalışıyor (sistem tepsisine bak).", "PadPilot");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            try { File.AppendAllText(Path.Combine(SettingsStore.Folder, "hata.log"), $"{DateTime.Now:u} {ex.Exception}\n\n"); } catch { }
            ex.Handled = true;
        };

        var engine = new Engine();
        engine.Start();
        var win = new MainWindow(new MainViewModel(engine));
        MainWindow = win;
        bool tray = e.Args.Contains("--tepsi") || engine.Settings.StartMinimized;
        if (!tray) win.Show();
    }
}
