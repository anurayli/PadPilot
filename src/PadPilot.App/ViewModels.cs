using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using PadPilot.Core;

namespace PadPilot.App;

public sealed record Choice(object Value, string Label);

/// <summary>Acilir listelerin Turkce secenekleri.</summary>
public static class Choices
{
    public static Choice[] OutputKinds { get; } =
    [
        new(OutputKind.Xbox360, "Xbox 360 kumandası (en uyumlu)"),
        new(OutputKind.DualShock4, "DualShock 4 (oyunda PlayStation simgeleri)"),
        new(OutputKind.None, "Sanal pad yok (yalnızca klavye/fare)"),
    ];
    public static Choice[] StickModes { get; } =
    [
        new(StickMode.Normal, "Analog"), new(StickMode.Mouse, "Fare imleci"), new(StickMode.Flick, "Flick stick (FPS)"),
        new(StickMode.Wasd, "WASD tuşları"), new(StickMode.Arrows, "Ok tuşları"),
    ];
    public static Choice[] StickCurves { get; } =
    [
        new(StickCurve.Linear, "Doğrusal"), new(StickCurve.Precise, "Hassas (merkezde yavaş)"),
        new(StickCurve.Smooth, "Yumuşak (S eğrisi)"), new(StickCurve.Aggressive, "Çevik (hızlı tepki)"),
    ];
    public static Choice[] GyroModes { get; } =
    [
        new(GyroMode.Off, "Kapalı"), new(GyroMode.Mouse, "Fare (nişan için en iyisi)"), new(GyroMode.RightStick, "Sağ analoğa ekle"),
    ];
    public static Choice[] GyroActivations { get; } =
    [
        new(GyroActivation.Always, "Her zaman"), new(GyroActivation.WhileHeld, "Tuş basılıyken"),
        new(GyroActivation.WhileNotHeld, "Tuş basılı değilken"), new(GyroActivation.Toggle, "Tuşla aç / kapat"),
    ];
    public static Choice[] GyroAxes { get; } =
    [
        new(GyroAxis.Yaw, "Sağa-sola çevirme (yaw)"), new(GyroAxis.Roll, "Yana yatırma (roll)"), new(GyroAxis.Both, "İkisi birden"),
    ];
    public static Choice[] Buttons { get; } = [.. Targets.MappableButtons.Select(b => new Choice(b, Targets.ButtonLabel(b)))];
    public static Choice[] TouchModes { get; } = [new(TouchMode.Off, "Kapalı (yalnızca tıklama tuşu)"), new(TouchMode.Mouse, "Fare / touchpad")];
    public static Choice[] LightModes { get; } =
    [
        new(LightMode.Static, "Sabit renk"), new(LightMode.Battery, "Pil seviyesine göre"),
        new(LightMode.Rainbow, "Gökkuşağı"), new(LightMode.Off, "Kapalı"),
    ];
    public static Choice[] TriggerKinds { get; } =
    [
        new(TriggerEffectKind.Off, "Kapalı"), new(TriggerEffectKind.Resistance, "Sabit direnç"),
        new(TriggerEffectKind.Slope, "Giderek artan direnç"), new(TriggerEffectKind.Weapon, "Silah (sonda tık)"),
        new(TriggerEffectKind.Bow, "Yay (ger ve bırak)"), new(TriggerEffectKind.Vibration, "Titreşim / otomatik silah"),
        new(TriggerEffectKind.Galloping, "Dörtnala"), new(TriggerEffectKind.Machine, "Makine"),
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
    public string Info => $"Oyuncu {Runtime.Slot} · {Connection} · {Device.ReportRateHz:0} Hz";
    public bool FullReport => _s.FullReport;
    public string Battery => _s.FullReport ? $"%{_s.BatteryPercent}" : "—";
    public string BatteryLong => !_s.FullReport ? "Pil bilgisi yok (basit mod)" : _s.Charging ? $"%{_s.BatteryPercent} · şarj oluyor" : $"%{_s.BatteryPercent}";
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
    public string L2Text => $"ham %{_s.L2 * 100 / 255} → çıkış %{Runtime.LastOutput.LT * 100 / 255}";
    public string R2Text => $"ham %{_s.R2 * 100 / 255} → çıkış %{Runtime.LastOutput.RT * 100 / 255}";
    public string GyroText => !_s.FullReport ? "Gyro verisi yok (basit mod)"
        : Runtime.GyroCalibrating ? "Kalibre ediliyor… pad'i kıpırdatma"
        : $"Yaw {Runtime.GyroYawDps,6:0.0}°/sn   Pitch {Runtime.GyroPitchDps,6:0.0}°/sn   {(Runtime.GyroActive ? "● etkin" : "○ beklemede")}";
    public string PressedText => _s.Buttons == PadButtons.None ? "Basılı tuş yok"
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
    public string? AutoProfileNote => Engine.AutoProfileExe is string exe ? $"{exe} açık olduğu için bu profil otomatik seçildi." : null;
    public bool AnyDualSense => Pads.Any(p => p.IsDualSense);

    public string DriverStatus => Engine.VirtualAvailable ? "ViGEmBus hazır" : "ViGEmBus bulunamadı";
    public Brush DriverBrush => Engine.VirtualAvailable ? (Brush)App.Current.Resources["Good"] : (Brush)App.Current.Resources["Bad"];
    public string HidHideStatus => HidHide.Installed ? "HidHide kurulu" : "HidHide kurulu değil";
    public IEnumerable<string> RecentExes => Engine.RecentExes;

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

    public string OutputText => (string)Choices.OutputKinds.First(c => Equals(c.Value, Settings.Output)).Label;

    // ---- Profil islemleri ----
    string UniqueName(string baseName)
    {
        string n = baseName; int i = 2;
        while (Settings.Profiles.Any(p => p.Name == n)) n = $"{baseName} {i++}";
        return n;
    }

    public void NewProfile() => AddProfile(new Profile { Name = UniqueName("Yeni profil") }.Normalize());
    public void DuplicateProfile() => AddProfile(Profile.Clone(UniqueName(Profile.Name + " kopya")));

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
