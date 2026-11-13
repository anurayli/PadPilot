using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PadPilot.Core;

namespace PadPilot.App.Pages;

static class PageHelpers
{
    public static MainViewModel Vm(this FrameworkElement e) => (MainViewModel)Application.Current.MainWindow.DataContext;
    public static PadVm? Pad(this FrameworkElement e) => e.Vm().SelectedPad;

    public static async void PulseRumble(PadDevice? pad, byte strong, byte weak, int ms = 450)
    {
        if (pad == null) return;
        pad.SetRumble(strong, weak);
        await Task.Delay(ms);
        pad.SetRumble(0, 0);
    }
}

public partial class OverviewPage : UserControl
{
    public OverviewPage() => InitializeComponent();

    void Rumble_Click(object s, RoutedEventArgs e) => PageHelpers.PulseRumble(this.Pad()?.Device, 200, 200);

    void Calibrate_Click(object s, RoutedEventArgs e)
    {
        this.Pad()?.Runtime.CalibrateGyro();
        ActionResult.Text = Loc.S("Calibrating");
    }

    void Disconnect_Click(object s, RoutedEventArgs e)
    {
        var p = this.Pad();
        if (p == null) return;
        ActionResult.Text = Loc.S(!p.Device.Bluetooth ? "WiredNoOff" : p.Device.DisconnectBluetooth() ? "TurnedOff" : "TurnOffFailed");
    }

    void OpenBluetooth_Click(object s, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
}

public partial class ButtonsPage : UserControl
{
    readonly System.Windows.Threading.DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public ButtonsPage()
    {
        InitializeComponent();
        _t.Tick += (_, _) => LightDots();
        IsVisibleChanged += (_, _) => { if (IsVisible) _t.Start(); else _t.Stop(); };
    }

    // Basilan tusun satirindaki noktayi yak
    void LightDots()
    {
        var pressed = this.Pad()?.State.Buttons ?? PadButtons.None;
        var on = (Brush)FindResource("Accent");
        var off = (Brush)FindResource("CardBorder");
        for (int i = 0; i < Rows.Items.Count; i++)
        {
            if (Rows.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter cp || VisualTreeHelper.GetChildrenCount(cp) == 0) continue;
            if (FindDot(cp) is Ellipse dot && dot.Tag is PadButtons b) dot.Fill = (pressed & b) != 0 ? on : off;
        }
    }

    static Ellipse? FindDot(DependencyObject d)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = VisualTreeHelper.GetChild(d, i);
            if (c is Ellipse e) return e;
            if (FindDot(c) is Ellipse r) return r;
        }
        return null;
    }

    void ResetButtons_Click(object s, RoutedEventArgs e)
    {
        foreach (var m in this.Vm().Profile.Buttons) { m.Tap = ""; m.Hold = ""; m.Turbo = false; }
    }
}

public partial class StickEditor : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(StickEditor));
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(StickSettings), typeof(StickEditor),
        new(null, (d, e) => ((StickEditor)d).OnSettings(e.OldValue as StickSettings, e.NewValue as StickSettings)));
    public static readonly DependencyProperty RawXProperty = DependencyProperty.Register(nameof(RawX), typeof(double), typeof(StickEditor));
    public static readonly DependencyProperty RawYProperty = DependencyProperty.Register(nameof(RawY), typeof(double), typeof(StickEditor));
    public static readonly DependencyProperty OutXProperty = DependencyProperty.Register(nameof(OutX), typeof(double), typeof(StickEditor));
    public static readonly DependencyProperty OutYProperty = DependencyProperty.Register(nameof(OutY), typeof(double), typeof(StickEditor));
    public static readonly DependencyProperty IsMouseModeProperty = DependencyProperty.Register(nameof(IsMouseMode), typeof(bool), typeof(StickEditor));
    public static readonly DependencyProperty IsFlickModeProperty = DependencyProperty.Register(nameof(IsFlickMode), typeof(bool), typeof(StickEditor));

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public StickSettings? Settings { get => (StickSettings?)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
    public double RawX { get => (double)GetValue(RawXProperty); set => SetValue(RawXProperty, value); }
    public double RawY { get => (double)GetValue(RawYProperty); set => SetValue(RawYProperty, value); }
    public double OutX { get => (double)GetValue(OutXProperty); set => SetValue(OutXProperty, value); }
    public double OutY { get => (double)GetValue(OutYProperty); set => SetValue(OutYProperty, value); }
    public bool IsMouseMode { get => (bool)GetValue(IsMouseModeProperty); set => SetValue(IsMouseModeProperty, value); }
    public bool IsFlickMode { get => (bool)GetValue(IsFlickModeProperty); set => SetValue(IsFlickModeProperty, value); }

    public StickEditor() => InitializeComponent();

    void OnSettings(StickSettings? old, StickSettings? s)
    {
        if (old != null) old.PropertyChanged -= ModeChanged;
        if (s != null) s.PropertyChanged += ModeChanged;
        ModeChanged(null, null);
    }

    void ModeChanged(object? sender, PropertyChangedEventArgs? e)
    {
        IsMouseMode = Settings?.Mode == StickMode.Mouse;
        IsFlickMode = Settings?.Mode == StickMode.Flick;
    }
}

public partial class SticksPage : UserControl { public SticksPage() => InitializeComponent(); }

public partial class TriggerEditor : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TriggerEditor));
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(TriggerSettings), typeof(TriggerEditor));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(TriggerEditor));
    public static readonly DependencyProperty LiveTextProperty = DependencyProperty.Register(nameof(LiveText), typeof(string), typeof(TriggerEditor));

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public TriggerSettings? Settings { get => (TriggerSettings?)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? LiveText { get => (string?)GetValue(LiveTextProperty); set => SetValue(LiveTextProperty, value); }

    public TriggerEditor() => InitializeComponent();

    void Preset_Click(object sender, RoutedEventArgs e)
    {
        var t = Settings;
        if (t == null || (sender as FrameworkElement)?.Tag is not string tag) return;
        void Set(TriggerEffectKind k, int start, int end, int str, int snap = 6, int freq = 10)
        { t.Effect = k; t.Start = start; t.End = end; t.Strength = str; t.Snap = snap; t.Frequency = freq; }
        switch (tag)
        {
            case "off": t.Effect = TriggerEffectKind.Off; break;
            case "pistol": Set(TriggerEffectKind.Weapon, 3, 6, 7); break;
            case "auto": Set(TriggerEffectKind.Vibration, 3, 9, 6, freq: 12); break;
            case "bow": Set(TriggerEffectKind.Bow, 1, 6, 6, snap: 8); break;
            case "brake": Set(TriggerEffectKind.Slope, 1, 8, 8); break;
            case "gas": Set(TriggerEffectKind.Resistance, 2, 9, 3); break;
            case "machine": Set(TriggerEffectKind.Machine, 1, 9, 7, snap: 3, freq: 8); break;
            case "gallop": Set(TriggerEffectKind.Galloping, 0, 9, 5, freq: 8); break;
            case "hard": Set(TriggerEffectKind.Resistance, 0, 9, 8); break;
        }
    }
}

public partial class TriggersPage : UserControl { public TriggersPage() => InitializeComponent(); }

public partial class MotionPage : UserControl
{
    public MotionPage() => InitializeComponent();
    void Calibrate_Click(object s, RoutedEventArgs e) => this.Pad()?.Runtime.CalibrateGyro();
}

public partial class TouchPage : UserControl { public TouchPage() => InitializeComponent(); }

public partial class LightsPage : UserControl
{
    public LightsPage() => InitializeComponent();

    void Swatch_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.Tag is string hex)
        {
            var l = this.Vm().Profile.Light;
            l.Color = hex;
            if (l.Mode == LightMode.Off) l.Mode = LightMode.Static;
        }
    }

    void Rumble_Click(object s, RoutedEventArgs e)
    {
        string tag = (s as FrameworkElement)?.Tag as string ?? "both";
        PageHelpers.PulseRumble(this.Pad()?.Device, tag == "weak" ? (byte)0 : (byte)220, tag == "strong" ? (byte)0 : (byte)220, 600);
    }
}

public partial class ProfilesPage : UserControl
{
    public ProfilesPage() => InitializeComponent();

    void New_Click(object s, RoutedEventArgs e) => this.Vm().NewProfile();
    void Duplicate_Click(object s, RoutedEventArgs e) => this.Vm().DuplicateProfile();
    void Reset_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show(string.Format(Loc.S("ConfirmReset"), this.Vm().Profile.Name), "PadPilot", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            this.Vm().ResetProfile();
    }
    void Delete_Click(object s, RoutedEventArgs e)
    {
        var vm = this.Vm();
        if (vm.Settings.Profiles.Count <= 1) { MessageBox.Show(Loc.S("LastProfile"), "PadPilot"); return; }
        if (MessageBox.Show(string.Format(Loc.S("ConfirmDelete"), vm.Profile.Name), "PadPilot", MessageBoxButton.YesNo) == MessageBoxResult.Yes) vm.DeleteProfile();
    }
    void Rename_Click(object s, RoutedEventArgs e) =>
        RenameError.Visibility = this.Vm().RenameProfile(NameBox.Text) ? Visibility.Collapsed : Visibility.Visible;
    void NameBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) Rename_Click(s, e); }
    void AddExe_Click(object s, RoutedEventArgs e) { if ((s as Button)?.Content is string exe) this.Vm().AddExe(exe); }
}

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        VersionRun.Text = v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    void Feedback_TextChanged(object s, TextChangedEventArgs e)
    {
        FeedbackHint.Visibility = FeedbackBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmailHint.Visibility = FeedbackEmail.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    async void SendFeedback_Click(object s, RoutedEventArgs e)
    {
        FeedbackResult.Visibility = Visibility.Visible;
        if (FeedbackBox.Text.Trim().Length == 0) { FeedbackResult.Text = Loc.S("EmptyMessage"); return; }
        SendButton.IsEnabled = false;
        FeedbackResult.Text = Loc.S("Sending");
        var (ok, err) = await Feedback.SendAsync(FeedbackBox.Text.Trim(), FeedbackEmail.Text, "Windows", VersionRun.Text);
        SendButton.IsEnabled = true;
        if (ok) { FeedbackResult.Text = Loc.S("Sent"); FeedbackBox.Clear(); }
        else FeedbackResult.Text = string.Format(Loc.S("SendFailed"), err);
    }

    void OpenUrl_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.Tag is string url) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    async void Hide_Click(object s, RoutedEventArgs e)
    {
        var vm = this.Vm();
        bool want = HideSwitch.IsChecked == true;
        HideResult.Visibility = Visibility.Visible;
        if (!HidHide.Installed)
        {
            HideSwitch.IsChecked = false;
            HideResult.Text = Loc.S("HidHideNotInstalled");
            return;
        }
        HideResult.Text = Loc.S("WaitingUac");
        bool ok = await vm.Engine.ApplyHidHideAsync(want);
        if (ok) vm.Settings.HideRealPads = want;
        HideSwitch.IsChecked = vm.Settings.HideRealPads;
        HideResult.Text = Loc.S(ok ? (want ? "Hidden" : "Unhidden") : "HideFailed");
    }

    void Startup_Click(object s, RoutedEventArgs e)
    {
        try { SystemIntegration.SetStartup(this.Vm().Settings.StartWithWindows); } catch { }
    }

    void OpenFolder_Click(object s, RoutedEventArgs e)
    {
        Directory.CreateDirectory(SettingsStore.Folder);
        Process.Start(new ProcessStartInfo(SettingsStore.Folder) { UseShellExecute = true });
    }
}
