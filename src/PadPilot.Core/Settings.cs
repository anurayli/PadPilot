using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadPilot.Core;

/// <summary>Ayar siniflari icin temel: degisiklik bildirir ve kaydetmeyi tetikler.</summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Herhangi bir ayar degistiginde (kaydetme ve cikislari yenileme icin).</summary>
    public static event Action? AnyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        AnyChanged?.Invoke();
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public static void NotifyAnyChanged() => AnyChanged?.Invoke();
}

public enum OutputKind { Xbox360, DualShock4, None }
public enum StickMode { Normal, Mouse, Flick, Wasd, Arrows }
public enum StickCurve { Linear, Precise, Smooth, Aggressive }
public enum GyroMode { Off, Mouse, RightStick }
public enum GyroActivation { Always, WhileHeld, WhileNotHeld, Toggle }
public enum GyroAxis { Yaw, Roll, Both }
public enum TouchMode { Off, Mouse }
public enum LightMode { Static, Battery, Rainbow, Off }
public enum TriggerEffectKind { Off, Resistance, Weapon, Bow, Vibration, Galloping, Machine, Slope }

public sealed class StickSettings : Observable
{
    double _dz = 0.06, _outer = 0.97, _anti, _sens = 1;
    StickCurve _curve;
    StickMode _mode;
    bool _invX, _invY;
    int _mouseSpeed = 1600, _flick360 = 6000;

    public double Deadzone { get => _dz; set => Set(ref _dz, value); }
    public double OuterZone { get => _outer; set => Set(ref _outer, value); }
    public double AntiDeadzone { get => _anti; set => Set(ref _anti, value); }
    public double Sensitivity { get => _sens; set => Set(ref _sens, value); }
    public StickCurve Curve { get => _curve; set => Set(ref _curve, value); }
    public StickMode Mode { get => _mode; set => Set(ref _mode, value); }
    public bool InvertX { get => _invX; set => Set(ref _invX, value); }
    public bool InvertY { get => _invY; set => Set(ref _invY, value); }
    /// <summary>Analog fare modunda tam itiste saniyedeki piksel.</summary>
    public int MouseSpeed { get => _mouseSpeed; set => Set(ref _mouseSpeed, value); }
    /// <summary>Flick stick: oyunda 360 derece donus icin gereken fare pikseli.</summary>
    public int Flick360 { get => _flick360; set => Set(ref _flick360, value); }
}

public sealed class TriggerSettings : Observable
{
    double _dz = 0.02, _max = 1;
    TriggerEffectKind _effect;
    int _start = 2, _end = 6, _strength = 5, _snap = 6, _freq = 10;

    public double Deadzone { get => _dz; set => Set(ref _dz, value); }
    /// <summary>Bu noktada tetik tam basilmis sayilir (sac tetik).</summary>
    public double MaxZone { get => _max; set => Set(ref _max, value); }
    public TriggerEffectKind Effect { get => _effect; set => Set(ref _effect, value); }
    public int Start { get => _start; set => Set(ref _start, value); }
    public int End { get => _end; set => Set(ref _end, value); }
    public int Strength { get => _strength; set => Set(ref _strength, value); }
    public int Snap { get => _snap; set => Set(ref _snap, value); }
    public int Frequency { get => _freq; set => Set(ref _freq, value); }
}

public sealed class ButtonMap : Observable
{
    string _tap = "", _hold = "";
    bool _turbo;

    public PadButtons Button { get; set; }
    /// <summary>Hedef kodu; bos = varsayilan esleme. Bkz. <see cref="Targets"/>.</summary>
    public string Tap { get => _tap; set => Set(ref _tap, value ?? ""); }
    /// <summary>Uzun basis hedefi; bos = kullanilmiyor.</summary>
    public string Hold { get => _hold; set => Set(ref _hold, value ?? ""); }
    public bool Turbo { get => _turbo; set => Set(ref _turbo, value); }
    [JsonIgnore] public string Label => Targets.ButtonLabel(Button);
}

public sealed class GyroSettings : Observable
{
    GyroMode _mode;
    GyroActivation _act = GyroActivation.WhileHeld;
    PadButtons _btn = PadButtons.L2;
    GyroAxis _axis = GyroAxis.Yaw;
    double _sx = 12, _sy = 10, _smooth = 0.6, _stickGain = 1;
    bool _invX, _invY;

    public GyroMode Mode { get => _mode; set => Set(ref _mode, value); }
    public GyroActivation Activation { get => _act; set => Set(ref _act, value); }
    public PadButtons ActivationButton { get => _btn; set => Set(ref _btn, value); }
    public GyroAxis Axis { get => _axis; set => Set(ref _axis, value); }
    /// <summary>Fare: derece basina piksel.</summary>
    public double SensX { get => _sx; set => Set(ref _sx, value); }
    public double SensY { get => _sy; set => Set(ref _sy, value); }
    /// <summary>Bu hizin (derece/sn) altindaki titremeler yumusatilir.</summary>
    public double Smoothing { get => _smooth; set => Set(ref _smooth, value); }
    public double StickGain { get => _stickGain; set => Set(ref _stickGain, value); }
    public bool InvertX { get => _invX; set => Set(ref _invX, value); }
    public bool InvertY { get => _invY; set => Set(ref _invY, value); }
}

public sealed class TouchSettings : Observable
{
    TouchMode _mode;
    double _sens = 1;
    bool _tap = true, _scroll = true, _split = true;

    public TouchMode Mode { get => _mode; set => Set(ref _mode, value); }
    public double Sensitivity { get => _sens; set => Set(ref _sens, value); }
    public bool TapToClick { get => _tap; set => Set(ref _tap, value); }
    public bool TwoFingerScroll { get => _scroll; set => Set(ref _scroll, value); }
    /// <summary>Touchpad'in sag yarisina basmak sag tik olur.</summary>
    public bool SplitClick { get => _split; set => Set(ref _split, value); }
}

public sealed class LightSettings : Observable
{
    LightMode _mode;
    string _color = "#2F6BFF";
    double _bright = 1, _rumble = 1;
    bool _lowFlash = true, _playerLeds = true;

    public LightMode Mode { get => _mode; set => Set(ref _mode, value); }
    public string Color { get => _color; set => Set(ref _color, value); }
    public double Brightness { get => _bright; set => Set(ref _bright, value); }
    public bool LowBatteryFlash { get => _lowFlash; set => Set(ref _lowFlash, value); }
    public bool PlayerLeds { get => _playerLeds; set => Set(ref _playerLeds, value); }
    /// <summary>Oyundan gelen titresimin carpani (0 = kapali, 2 = iki kat).</summary>
    public double RumbleStrength { get => _rumble; set => Set(ref _rumble, value); }
}

public sealed class Profile : Observable
{
    string _name = "Varsayılan", _exes = "";

    public string Name { get => _name; set => Set(ref _name, value); }
    /// <summary>Bu profili otomatik acan oyunlar, ";" ile ayrilmis (ornek: "eldenring.exe; cyberpunk2077.exe").</summary>
    public string Exes { get => _exes; set => Set(ref _exes, value); }
    public StickSettings LeftStick { get; set; } = new();
    public StickSettings RightStick { get; set; } = new();
    public TriggerSettings L2 { get; set; } = new();
    public TriggerSettings R2 { get; set; } = new();
    public List<ButtonMap> Buttons { get; set; } = [];
    public GyroSettings Gyro { get; set; } = new();
    public TouchSettings Touch { get; set; } = new();
    public LightSettings Light { get; set; } = new();

    /// <summary>Eksik tus satirlarini tamamlar (eski dosyalar ve yeni profiller icin).</summary>
    public Profile Normalize()
    {
        var list = new List<ButtonMap>();
        foreach (var b in Targets.MappableButtons)
            list.Add(Buttons.FirstOrDefault(m => m.Button == b) ?? new ButtonMap { Button = b });
        Buttons = list;
        return this;
    }

    public bool MatchesExe(string exe)
    {
        foreach (var part in Exes.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string p = part.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? part : part + ".exe";
            if (string.Equals(p, exe, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public Profile Clone(string name)
    {
        var p = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this, SettingsStore.Json), SettingsStore.Json)!;
        p.Name = name; p.Exes = "";
        return p.Normalize();
    }
}

public sealed class AppSettings : Observable
{
    OutputKind _output;
    bool _hide, _startup, _startMin, _closeTray = true, _auto = true, _psOpt = true;
    int _lowBat = 20, _idle;
    string _active = "Varsayılan";

    public OutputKind Output { get => _output; set => Set(ref _output, value); }
    public bool HideRealPads { get => _hide; set => Set(ref _hide, value); }
    public bool StartWithWindows { get => _startup; set => Set(ref _startup, value); }
    public bool StartMinimized { get => _startMin; set => Set(ref _startMin, value); }
    public bool CloseToTray { get => _closeTray; set => Set(ref _closeTray, value); }
    public bool AutoProfile { get => _auto; set => Set(ref _auto, value); }
    /// <summary>PS + Options basili tutulunca Bluetooth pad'i kapat.</summary>
    public bool PsOptionsDisconnect { get => _psOpt; set => Set(ref _psOpt, value); }
    public int LowBatteryWarn { get => _lowBat; set => Set(ref _lowBat, value); }
    /// <summary>Bu kadar dakika dokunulmayan Bluetooth pad kapatilir (0 = kapali).</summary>
    public int IdleDisconnectMinutes { get => _idle; set => Set(ref _idle, value); }
    public string ActiveProfile { get => _active; set => Set(ref _active, value); }
    public List<Profile> Profiles { get; set; } = [];
}

/// <summary>%AppData%\PadPilot\settings.json okuma/yazma.</summary>
public static class SettingsStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Folder { get; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadPilot");
    static string FilePath => System.IO.Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        AppSettings? s = null;
        try { if (File.Exists(FilePath)) s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json); }
        catch { /* bozuk dosya: varsayilanlarla basla, eskisini yedekle */
            try { File.Copy(FilePath, FilePath + ".bozuk", true); } catch { }
        }
        s ??= new AppSettings();
        if (s.Profiles.Count == 0) s.Profiles.Add(new Profile());
        foreach (var p in s.Profiles) p.Normalize();
        if (s.Profiles.All(p => p.Name != s.ActiveProfile)) s.ActiveProfile = s.Profiles[0].Name;
        return s;
    }

    public static void Save(AppSettings s)
    {
        Directory.CreateDirectory(Folder);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, FilePath, true);
    }
}
