using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using PadPilot.Core;

namespace PadPilot.App;

public sealed record Choice(object Value, string Label);

/// <summary>Acilir listelerin secenekleri (gecerli dilde; sayfa her kurulusta yeniden okur).</summary>
public static class Choices
{
    static string T(string tr, string en) => Lang.T(tr, en);

    public static Choice[] Languages =>
    [
        new("auto", T("Otomatik (Windows dili)", "Automatic (Windows language)")), new("tr", "Türkçe"), new("en", "English"),
    ];
    public static Choice[] OutputKinds =>
    [
        new(OutputKind.Xbox360, T("Xbox 360 kumandası (en uyumlu)", "Xbox 360 controller (most compatible)")),
        new(OutputKind.DualShock4, T("DualShock 4 (oyunda PlayStation simgeleri)", "DualShock 4 (PlayStation icons in games)")),
        new(OutputKind.None, T("Sanal pad yok (yalnızca klavye/fare)", "No virtual pad (keyboard/mouse only)")),
    ];
    public static Choice[] StickModes =>
    [
        new(StickMode.Normal, T("Analog", "Analog stick")), new(StickMode.Mouse, T("Fare imleci", "Mouse cursor")),
        new(StickMode.Flick, T("Flick stick (FPS)", "Flick stick (FPS)")),
        new(StickMode.Wasd, T("WASD tuşları", "WASD keys")), new(StickMode.Arrows, T("Ok tuşları", "Arrow keys")),
    ];
    public static Choice[] StickCurves =>
    [
        new(StickCurve.Linear, T("Doğrusal", "Linear")), new(StickCurve.Precise, T("Hassas (merkezde yavaş)", "Precise (slow near center)")),
        new(StickCurve.Smooth, T("Yumuşak (S eğrisi)", "Smooth (S-curve)")), new(StickCurve.Aggressive, T("Çevik (hızlı tepki)", "Aggressive (fast response)")),
    ];
    public static Choice[] GyroModes =>
    [
        new(GyroMode.Off, T("Kapalı", "Off")), new(GyroMode.Mouse, T("Fare (nişan için en iyisi)", "Mouse (best for aiming)")),
        new(GyroMode.RightStick, T("Sağ analoğa ekle", "Add to right stick")),
    ];
    public static Choice[] GyroActivations =>
    [
        new(GyroActivation.Always, T("Her zaman", "Always")), new(GyroActivation.WhileHeld, T("Tuş basılıyken", "While button held")),
        new(GyroActivation.WhileNotHeld, T("Tuş basılı değilken", "While button not held")), new(GyroActivation.Toggle, T("Tuşla aç / kapat", "Toggle with button")),
    ];
    public static Choice[] GyroAxes =>
    [
        new(GyroAxis.Yaw, T("Sağa-sola çevirme (yaw)", "Turn left/right (yaw)")), new(GyroAxis.Roll, T("Yana yatırma (roll)", "Tilt sideways (roll)")),
        new(GyroAxis.Both, T("İkisi birden", "Both")),
    ];
    public static Choice[] Buttons => [.. Targets.MappableButtons.Select(b => new Choice(b, Targets.ButtonLabel(b)))];
    public static Choice[] TouchModes =>
    [
        new(TouchMode.Off, T("Kapalı (yalnızca tıklama tuşu)", "Off (click button only)")), new(TouchMode.Mouse, T("Fare / touchpad", "Mouse / touchpad")),
    ];
    public static Choice[] LightModes =>
    [
        new(LightMode.Static, T("Sabit renk", "Solid color")), new(LightMode.Battery, T("Pil seviyesine göre", "By battery level")),
        new(LightMode.Rainbow, T("Gökkuşağı", "Rainbow")), new(LightMode.Off, T("Kapalı", "Off")),
    ];
    public static Choice[] TriggerKinds =>
    [
        new(TriggerEffectKind.Off, T("Kapalı", "Off")), new(TriggerEffectKind.Resistance, T("Sabit direnç", "Constant resistance")),
        new(TriggerEffectKind.Slope, T("Giderek artan direnç", "Increasing resistance")), new(TriggerEffectKind.Weapon, T("Silah (sonda tık)", "Weapon (click at end)")),
        new(TriggerEffectKind.Bow, T("Yay (ger ve bırak)", "Bow (pull and release)")), new(TriggerEffectKind.Vibration, T("Titreşim / otomatik silah", "Vibration / automatic gun")),
        new(TriggerEffectKind.Galloping, T("Dörtnala", "Galloping")), new(TriggerEffectKind.Machine, T("Makine", "Machine")),
    ];
    public static IReadOnlyList<TargetInfo> Targets_ => Targets.All;
    public static IReadOnlyList<TargetInfo> HoldTargets => Targets.HoldTargets;
    public static string[] Swatches { get; } =
        ["#2F6BFF", "#00B7FF", "#00D4A0", "#3DD68C", "#FFD23F", "#FF8A3D", "#FF3B4E", "#FF4FB5", "#9B5CFF", "#FFFFFF"];
}

public abstract class Vm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    protected void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

/// <summary>Bagli bir pad'in arayuz gorunumu (canli degerler 30 Hz yenilenir).</summary>
public sealed class PadVm(PadRuntime rt) : Vm
{
    PadState _s;
    public PadRuntime Runtime { get; } = rt;
    public PadDevice Device => Runtime.Pad;
    public PadState State => _s;

    public string Name => Device.DisplayName;
    public bool IsDualSense => Device.Kind != PadKind.DualShock4;
    public string Connection => Device.Bluetooth ? "Bluetooth" : "USB";
    public string ConnectionIcon => Device.Bluetooth ? "" : "";
    public string Info => $"{Loc.S("Player")} {Runtime.Slot} · {Connection} · {Device.ReportRateHz:0} Hz";
    public bool FullReport => _s.FullReport;
    public string Battery => _s.FullReport ? $"{_s.BatteryPercent}%" : "—";
    public string BatteryLong => !_s.FullReport ? Loc.S("NoBattery") : _s.Charging ? $"{_s.BatteryPercent}% · {Loc.S("Charging")}" : $"{_s.BatteryPercent}%";
    public string BatteryIcon => !_s.FullReport ? "" : _s.Charging ? "" : _s.BatteryPercent switch
    {
        >= 90 => "", >= 70 => "", >= 50 => "", >= 30 => "", >= 15 => "", _ => "",
    };
    public Brush BatteryBrush => !_s.FullReport ? Brushes.Gray : _s.Charging || _s.BatteryPercent > 30
        ? (Brush)App.Current.Resources["Good"] : _s.BatteryPercent > 15 ? (Brush)App.Current.Resources["Warn"] : (Brush)App.Current.Resources["Bad"];
    public string OutputStatus => Device.OutputStatus;

    public double RawLX => StickMath.Norm(_s.LX);
    public double RawLY => -StickMath.Norm(_s.LY);
    public double RawRX => StickMath.Norm(_s.RX);
    public double RawRY => -StickMath.Norm(_s.RY);
    public double OutLX => Runtime.OutLX;
    public double OutLY => Runtime.OutLY;
    public double OutRX => Runtime.OutRX;
    public double OutRY => Runtime.OutRY;
    public double L2 => _s.L2 / 255.0;
    public double R2 => _s.R2 / 255.0;
    public string L2Text => $"{Loc.S("Raw")} {_s.L2 * 100 / 255}% → {Loc.S("Out")} {Runtime.LastOutput.LT * 100 / 255}%";
    public string R2Text => $"{Loc.S("Raw")} {_s.R2 * 100 / 255}% → {Loc.S("Out")} {Runtime.LastOutput.RT * 100 / 255}%";
    public string GyroText => !_s.FullReport ? Loc.S("NoGyro")
        : Runtime.GyroCalibrating ? Loc.S("GyroCalibrating")
        : $"Yaw {Runtime.GyroYawDps,6:0.0}°/s   Pitch {Runtime.GyroPitchDps,6:0.0}°/s   {Loc.S(Runtime.GyroActive ? "GyroActive" : "GyroIdle")}";
    public string PressedText => _s.Buttons == PadButtons.None ? Loc.S("NoButtons")
        : string.Join("  ", Targets.MappableButtons.Where(b => (_s.Buttons & b) != 0).Select(Targets.ButtonLabel));

    public void Refresh() { _s = Device.State; RaiseAll(); }
}

public sealed class MainViewModel : Vm
{
    public Engine Engine { get; }
    public AppSettings Settings => Engine.Settings;
    public ObservableCollection<PadVm> Pads { get; } = [];
    public ObservableCollection<Profile> Profiles { get; }

    PadVm? _selected;
    public PadVm? SelectedPad { get => _selected; set { _selected = value; Raise(); Raise(nameof(HasPad)); Raise(nameof(NoPad)); } }
    public bool HasPad => _selected != null;
    public bool NoPad => _selected == null;

    public Profile Profile => Engine.ActiveProfile;
    public Profile SelectedProfile
    {
        get => Engine.ActiveProfile;
        set { if (value != null && !ReferenceEquals(value, Engine.ActiveProfile)) Engine.SelectProfile(value.Name); }
    }
    public string? AutoProfileNote => Engine.AutoProfileExe is string exe ? string.Format(Loc.S("AutoNote"), exe) : null;
    public bool AnyDualSense => Pads.Any(p => p.IsDualSense);

    public string DriverStatus => Loc.S(Engine.VirtualAvailable ? "ViGEmOk" : "ViGEmMissing");
    public Brush DriverBrush => Engine.VirtualAvailable ? (Brush)App.Current.Resources["Good"] : (Brush)App.Current.Resources["Bad"];
    public string HidHideStatus => Loc.S(HidHide.Installed ? "HidHideOk" : "HidHideMissing");
    public IEnumerable<string> RecentExes => Engine.RecentExes;

    /// <summary>Dil degisince pencereyi yeniden kurmak icin.</summary>
    public event Action? LanguageChanged;
    public string Language
    {
        get => Settings.Language;
        set
        {
            if (value == null || value == Settings.Language) return;
            Settings.Language = value;
            Lang.Apply(value);
            LanguageChanged?.Invoke();
        }
    }

    public MainViewModel(Engine engine)
    {
        Engine = engine;
        Profiles = new(engine.Settings.Profiles);
        engine.ProfileChanged += () => App.Current.Dispatcher.BeginInvoke(() =>
        {
            Raise(nameof(Profile)); Raise(nameof(SelectedProfile)); Raise(nameof(AutoProfileNote));
        });
        engine.PadsChanged += () => App.Current.Dispatcher.BeginInvoke(SyncPads);
        SyncPads();
    }

    void SyncPads()
    {
        var live = Engine.Pads.ToList();
        foreach (var vm in Pads.Where(v => !live.Contains(v.Runtime)).ToList()) Pads.Remove(vm);
        foreach (var rt in live.Where(r => Pads.All(v => v.Runtime != r))) Pads.Add(new PadVm(rt));
        if (SelectedPad == null || !Pads.Contains(SelectedPad)) SelectedPad = Pads.FirstOrDefault();
        Raise(nameof(AnyDualSense));
    }

    int _slow;
    public void Tick()
    {
        foreach (var p in Pads) if (p == SelectedPad || _slow == 0) p.Refresh();
        if (++_slow >= 15) { _slow = 0; Raise(nameof(RecentExes)); Raise(nameof(LightColor)); Raise(nameof(OutputText)); }
    }

    public string OutputText => Choices.OutputKinds.First(c => Equals(c.Value, Settings.Output)).Label;

    // ---- Profil islemleri ----
    string UniqueName(string baseName)
    {
        string n = baseName; int i = 2;
        while (Settings.Profiles.Any(p => p.Name == n)) n = $"{baseName} {i++}";
        return n;
    }

    public void NewProfile() => AddProfile(new Profile { Name = UniqueName(Loc.S("NewProfileName")) }.Normalize());
    public void DuplicateProfile() => AddProfile(Profile.Clone(UniqueName(Profile.Name + " " + Loc.S("CopySuffix"))));

    void AddProfile(Profile p)
    {
        Settings.Profiles.Add(p); Profiles.Add(p);
        Engine.SelectProfile(p.Name);
        Observable.NotifyAnyChanged();
    }

    public bool RenameProfile(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || Settings.Profiles.Any(p => p.Name == name && !ReferenceEquals(p, Profile))) return false;
        bool wasManual = Settings.ActiveProfile == Profile.Name;
        Profile.Name = name;
        if (wasManual) Settings.ActiveProfile = name;
        int i = Profiles.IndexOf(Profile);
        if (i >= 0) { Profiles[i] = Profile; Raise(nameof(SelectedProfile)); }
        return true;
    }

    public void DeleteProfile()
    {
        if (Settings.Profiles.Count <= 1) return;
        var p = Profile;
        int i = Settings.Profiles.IndexOf(p);
        Settings.Profiles.Remove(p); Profiles.Remove(p);
        Engine.SelectProfile(Settings.Profiles[Math.Max(0, i - 1)].Name);
        Observable.NotifyAnyChanged();
    }

    public void AddExe(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe) || Profile.MatchesExe(exe)) return;
        Profile.Exes = string.IsNullOrWhiteSpace(Profile.Exes) ? exe : Profile.Exes.TrimEnd(' ', ';') + "; " + exe;
    }

    public void ResetProfile()
    {
        var fresh = new Profile { Name = Profile.Name, Exes = Profile.Exes }.Normalize();
        int i = Settings.Profiles.IndexOf(Profile);
        Settings.Profiles[i] = fresh; Profiles[i] = fresh;
        Engine.SelectProfile(fresh.Name);
        Observable.NotifyAnyChanged();
    }

    public Color LightColor
    {
        get { var (r, g, b) = Engine.Hex(Profile.Light.Color); return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)); }
    }
}
