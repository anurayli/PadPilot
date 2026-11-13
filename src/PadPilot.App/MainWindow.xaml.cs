using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PadPilot.App;

public partial class MainWindow : Window
{
    sealed record Page(string Icon, string Key, Func<FrameworkElement> Create);

    static readonly Page[] Pages =
    [
        new("", "PgOverview", () => new Pages.OverviewPage()),
        new("", "PgButtons", () => new Pages.ButtonsPage()),
        new("", "PgSticks", () => new Pages.SticksPage()),
        new("", "PgTriggers", () => new Pages.TriggersPage()),
        new("", "PgMotion", () => new Pages.MotionPage()),
        new("", "PgTouch", () => new Pages.TouchPage()),
        new("", "PgLights", () => new Pages.LightsPage()),
        new("", "PgProfiles", () => new Pages.ProfilesPage()),
        new("", "PgSettings", () => new Pages.SettingsPage()),
    ];

    readonly MainViewModel _vm;
    readonly Dictionary<int, FrameworkElement> _cache = [];
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly Forms.NotifyIcon _tray;
    readonly Action<string, string, bool> _notify;
    bool _exiting;

    public MainWindow(MainViewModel vm, int page = 0)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        foreach (var p in Pages)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = p.Icon, Style = (Style)FindResource("Icon"), Width = 22 });
            row.Children.Add(new TextBlock { Text = Loc.S(p.Key), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            Nav.Items.Add(new ListBoxItem { Content = row });
        }
        Nav.SelectedIndex = page;

        _timer.Tick += (_, _) =>
        {
            vm.Tick();
            AutoNote.Visibility = vm.AutoProfileNote == null ? Visibility.Collapsed : Visibility.Visible;
        };
        _timer.Start();

        _tray = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/padpilot.ico")).Stream),
            Text = "PadPilot",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        _tray.ContextMenuStrip.Items.Add(Loc.S("TrayOpen"), null, (_, _) => ShowFromTray());
        _tray.ContextMenuStrip.Items.Add("-");
        _tray.ContextMenuStrip.Items.Add(Loc.S("TrayExit"), null, (_, _) => ExitApp());
        _tray.DoubleClick += (_, _) => ShowFromTray();

        _notify = (title, text, important) => Dispatcher.BeginInvoke(() =>
        {
            if (important || !IsVisible) _tray.ShowBalloonTip(3000, title, text, Forms.ToolTipIcon.None);
        });
        vm.Engine.Notify += _notify;
    }

    public int CurrentPage => Nav.SelectedIndex;

    void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int i = Nav.SelectedIndex;
        if (i < 0) return;
        if (!_cache.TryGetValue(i, out var page)) _cache[i] = page = Pages[i].Create();
        PageTitle.Text = Loc.S(Pages[i].Key);
        PageSub.Text = Loc.S(Pages[i].Key + "Sub");
        PageHost.Content = page;
    }

    void PadChip_Checked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PadVm p) _vm.SelectedPad = p;
    }

    void PadChip_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton t && t.DataContext == _vm.SelectedPad) t.IsChecked = true;
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && _vm.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>Pencereyi kapatir ama uygulamayi kapatmaz (dil degisiminde yeni pencere acilir).</summary>
    public void CloseForRebuild()
    {
        _exiting = true;
        _timer.Stop();
        _vm.Engine.Notify -= _notify;
        _tray.Visible = false;
        _tray.Dispose();
        Close();
    }

    public void ExitApp()
    {
        CloseForRebuild();
        _vm.Engine.Dispose();
        Application.Current.Shutdown();
    }
}
