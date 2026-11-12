using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PadPilot.Core;
using Forms = System.Windows.Forms;

namespace PadPilot.App;

public partial class MainWindow : Window
{
    sealed record Page(string Icon, string Title, string Subtitle, Func<FrameworkElement> Create);

    static readonly Page[] Pages =
    [
        new("", "Genel bakış", "Bağlı pad'in canlı durumu ve hızlı işlemler.", () => new Pages.OverviewPage()),
        new("", "Tuşlar", "Her tuşa pad tuşu, klavye, fare veya eylem ata. Uzun basış ve turbo dahil.", () => new Pages.ButtonsPage()),
        new("", "Analoglar", "Ölü bölge, eğri, hassasiyet; analoğu fare, flick stick veya tuş olarak kullan.", () => new Pages.SticksPage()),
        new("", "Tetikler", "Tetik ölü bölgesi, saç tetik ve DualSense adaptif tetikleri.", () => new Pages.TriggersPage()),
        new("", "Hareket (gyro)", "Pad'i çevirerek nişan al: gyro ile fare veya sağ analog.", () => new Pages.MotionPage()),
        new("", "Dokunmatik", "Touchpad'i fare gibi kullan: kaydır, dokun-tıkla, iki parmakla kaydır.", () => new Pages.TouchPage()),
        new("", "Işık ve titreşim", "Işık çubuğu, oyuncu LED'leri ve titreşim gücü.", () => new Pages.LightsPage()),
        new("", "Profiller", "Oyuna göre otomatik değişen profiller.", () => new Pages.ProfilesPage()),
        new("", "Ayarlar", "Sanal pad türü, çift girişi önleme, başlangıç ve güç.", () => new Pages.SettingsPage()),
    ];

    readonly MainViewModel _vm;
    readonly Dictionary<int, FrameworkElement> _cache = [];
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly Forms.NotifyIcon _tray;
    bool _exiting;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        foreach (var p in Pages)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = p.Icon, Style = (Style)FindResource("Icon"), Width = 22 });
            row.Children.Add(new TextBlock { Text = p.Title, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            Nav.Items.Add(new ListBoxItem { Content = row });
        }
        Nav.SelectedIndex = 0;

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
        _tray.ContextMenuStrip.Items.Add("Aç", null, (_, _) => ShowFromTray());
        _tray.ContextMenuStrip.Items.Add("-");
        _tray.ContextMenuStrip.Items.Add("Çıkış", null, (_, _) => ExitApp());
        _tray.DoubleClick += (_, _) => ShowFromTray();

        vm.Engine.Notify += (title, text) => Dispatcher.BeginInvoke(() =>
        {
            bool important = title.StartsWith("Pil") || title.StartsWith("Profil");
            if (important || !IsVisible) _tray.ShowBalloonTip(3000, title, text, Forms.ToolTipIcon.None);
        });
    }

    void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int i = Nav.SelectedIndex;
        if (i < 0) return;
        if (!_cache.TryGetValue(i, out var page)) _cache[i] = page = Pages[i].Create();
        PageTitle.Text = Pages[i].Title;
        PageSub.Text = Pages[i].Subtitle;
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

    public void ExitApp()
    {
        _exiting = true;
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        Close();
        _vm.Engine.Dispose();
        Application.Current.Shutdown();
    }
}
