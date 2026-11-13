using System.Threading;
using System.Windows;
using PadPilot.Core;

namespace PadPilot.App;

public partial class App : Application
{
    Mutex? _single;
    MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var engine = new Engine();   // ayarlari ve dili yukler
        _single = new Mutex(true, "PadPilot.TekKopya", out bool first);
        if (!first)
        {
            MessageBox.Show(Loc.S("AlreadyRunning"), "PadPilot");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                File.AppendAllText(Path.Combine(SettingsStore.Folder, "hata.log"), $"{DateTime.Now:u} {ex.Exception}\n\n");
            }
            catch { }
            ex.Handled = true;
        };

        engine.Start();
        _vm = new MainViewModel(engine);
        _vm.LanguageChanged += RebuildWindow;
        var win = new MainWindow(_vm);
        MainWindow = win;
        bool tray = e.Args.Contains("--tepsi") || e.Args.Contains("--tray") || engine.Settings.StartMinimized;
        if (!tray) win.Show();
    }

    // Dil degisince tum metinler yeni dilde kurulsun diye pencere yeniden olusturulur.
    void RebuildWindow() => Dispatcher.BeginInvoke(() =>
    {
        if (MainWindow is not MainWindow old || _vm == null) return;
        var win = new MainWindow(_vm, old.CurrentPage) { Left = old.Left, Top = old.Top, Width = old.Width, Height = old.Height, WindowState = old.WindowState };
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        MainWindow = win;
        win.Show();
        old.CloseForRebuild();
    });
}
