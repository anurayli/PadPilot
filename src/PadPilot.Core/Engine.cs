using System.Collections.Concurrent;

namespace PadPilot.Core;

/// <summary>Uygulamanin kalbi: pad'leri bulur, profili uygular, sanal pad'leri, isiklari ve bildirimleri yonetir.</summary>
public sealed class Engine : IDisposable
{
    readonly PadManager _manager = new();
    readonly VirtualPads _virtual = new();
    readonly ConcurrentDictionary<PadDevice, PadRuntime> _runtimes = new();
    readonly HashSet<PadDevice> _lowWarned = [];
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    Timer? _timer;
    volatile bool _dirty, _outputsDirty = true;
    double _dirtyAt, _lastFg;
    OutputKind _currentOutput;
    string? _autoExe;

    public AppSettings Settings { get; }
    /// <summary>O an uygulanan profil (otomatik profil devredeyse oyununki).</summary>
    public Profile ActiveProfile { get; private set; }
    public string? AutoProfileExe => _autoExe;
    public string? VirtualError => _virtual.Error;
    public bool VirtualAvailable => _virtual.Available;
    public string? ForegroundExe { get; private set; }

    static readonly HashSet<string> IgnoredExes = new(StringComparer.OrdinalIgnoreCase)
        { "PadPilot.exe", "explorer.exe", "ApplicationFrameHost.exe", "ShellExperienceHost.exe", "SearchHost.exe", "StartMenuExperienceHost.exe", "LockApp.exe", "Idle.exe" };
    readonly List<string> _recent = [];
    /// <summary>Son one gelen uygulamalar (otomatik profil eklemek icin).</summary>
    public IReadOnlyList<string> RecentExes { get { lock (_recent) return [.. _recent]; } }

    public IReadOnlyCollection<PadRuntime> Pads => [.. _runtimes.Values.OrderBy(r => r.Slot)];

    public event Action? PadsChanged;
    public event Action? ProfileChanged;
    /// <summary>Tepsi bildirimi icin (baslik, metin).</summary>
    public event Action<string, string, bool>? Notify;

    void Say(string trTitle, string enTitle, string text, bool important = false) =>
        Notify?.Invoke(Lang.T(trTitle, enTitle), text, important);

    public Engine()
    {
        Settings = SettingsStore.Load();
        ActiveProfile = FindProfile(Settings.ActiveProfile);
        _currentOutput = Settings.Output;
        Observable.AnyChanged += () => { _dirty = true; _dirtyAt = _clock.Elapsed.TotalSeconds; _outputsDirty = true; };
        _manager.PadAdded += OnPadAdded;
        _manager.PadRemoved += OnPadRemoved;
    }

    public void Start()
    {
        _manager.Start();
        _timer = new Timer(_ => Tick(), null, 100, 50);
    }

    Profile FindProfile(string name) => Settings.Profiles.FirstOrDefault(p => p.Name == name) ?? Settings.Profiles[0];

    // ---------- Pad'ler ----------

    void OnPadAdded(PadDevice pad)
    {
        var rt = new PadRuntime(pad);
        int slot = 1;
        while (_runtimes.Values.Any(r => r.Slot == slot)) slot++;
        rt.Slot = slot;
        rt.ActionRequested += OnAction;
        rt.Target = CreateTarget(pad);
        _runtimes[pad] = rt;
        pad.StateChanged += (d, s) => { if (_runtimes.TryGetValue(d, out var r)) r.Process(s, ActiveProfile, Settings); };
        _outputsDirty = true;
        if (Settings.HideRealPads) HideNewPad(pad);
        PadsChanged?.Invoke();
        Say("Pad bağlandı", "Controller connected", $"{pad.DisplayName} · {(pad.Bluetooth ? "Bluetooth" : "USB")} · {Lang.T("Oyuncu", "Player")} {slot}");
    }

    void OnPadRemoved(PadDevice pad)
    {
        if (_runtimes.TryRemove(pad, out var rt)) rt.Dispose();
        lock (_lowWarned) _lowWarned.Remove(pad);
        PadsChanged?.Invoke();
    }

    IVirtualTarget? CreateTarget(PadDevice pad)
    {
        try
        {
            return _virtual.Create(Settings.Output, (strong, weak) =>
            {
                double k = ActiveProfile.Light.RumbleStrength;
                pad.SetRumble((byte)Math.Clamp(strong * k, 0, 255), (byte)Math.Clamp(weak * k, 0, 255));
            });
        }
        catch { return null; }
    }

    void RecreateTargets()
    {
        foreach (var rt in _runtimes.Values)
        {
            var old = rt.Target;
            rt.Target = null;
            old?.Dispose();
            rt.Target = CreateTarget(rt.Pad);
        }
    }

    // ---------- Profiller ----------

    public void SelectProfile(string name)
    {
        Settings.ActiveProfile = FindProfile(name).Name;
        ApplyEffective();
    }

    void ApplyEffective()
    {
        var p = _autoExe != null ? Settings.Profiles.FirstOrDefault(x => x.MatchesExe(_autoExe)) : null;
        p ??= FindProfile(Settings.ActiveProfile);
        if (ReferenceEquals(p, ActiveProfile)) return;
        ActiveProfile = p;
        foreach (var rt in _runtimes.Values) rt.ReleaseAll();
        _outputsDirty = true;
        ProfileChanged?.Invoke();
    }

    void OnAction(PadRuntime rt, string code)
    {
        switch (code)
        {
            case "a:NextProfile":
            case "a:PrevProfile":
                int i = Settings.Profiles.IndexOf(FindProfile(Settings.ActiveProfile));
                int n = Settings.Profiles.Count;
                SelectProfile(Settings.Profiles[(i + (code == "a:NextProfile" ? 1 : n - 1)) % n].Name);
                Say("Profil", "Profile", ActiveProfile.Name, true);
                break;
            case "a:Disconnect":
                if (rt.Pad.DisconnectBluetooth()) Say("Pad kapatıldı", "Controller turned off", rt.Pad.DisplayName);
                break;
        }
    }

    // ---------- Periyodik isler (50 ms) ----------

    void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds;
        try
        {
            if (_currentOutput != Settings.Output) { _currentOutput = Settings.Output; RecreateTargets(); }

            if (now - _lastFg > 1)
            {
                _lastFg = now;
                ForegroundExe = SystemIntegration.ForegroundExe();
                if (ForegroundExe != null && !IgnoredExes.Contains(ForegroundExe))
                    lock (_recent) { _recent.Remove(ForegroundExe); _recent.Insert(0, ForegroundExe); if (_recent.Count > 10) _recent.RemoveAt(10); }
                string? match = Settings.AutoProfile && ForegroundExe != null && Settings.Profiles.Any(p => p.MatchesExe(ForegroundExe)) ? ForegroundExe : null;
                if (match != _autoExe)
                {
                    _autoExe = match;
                    var before = ActiveProfile;
                    ApplyEffective();
                    if (!ReferenceEquals(before, ActiveProfile)) Say("Profil değişti", "Profile changed", ActiveProfile.Name + (match != null ? $" ({match})" : ""), true);
                }
            }

            bool sendTriggers = _outputsDirty;
            _outputsDirty = false;
            var p = ActiveProfile;
            foreach (var rt in _runtimes.Values) UpdatePad(rt, p, now, sendTriggers);

            if (_dirty && now - _dirtyAt > 0.8) { _dirty = false; SettingsStore.Save(Settings); }
        }
        catch { /* bir sonraki turda yeniden denenir */ }
    }

    void UpdatePad(PadRuntime rt, Profile p, double now, bool sendTriggers)
    {
        var pad = rt.Pad;
        var s = pad.State;
        var l = p.Light;

        if (sendTriggers)
        {
            pad.SetTriggers(TriggerEffects.Build(p.R2), TriggerEffects.Build(p.L2));
            pad.SetPlayerLeds(l.PlayerLeds ? PlayerMask(rt.Slot) : (byte)0);
        }

        // Isik cubugu
        var (r, g, b) = l.Mode switch
        {
            LightMode.Off => (0.0, 0.0, 0.0),
            LightMode.Rainbow => Hsv((now * 45 + rt.Slot * 90) % 360),
            LightMode.Battery => Hsv(Math.Min((int)s.BatteryPercent, 100) * 1.2),
            _ => Hex(l.Color),
        };
        bool low = s.FullReport && !s.Charging && s.BatteryPercent <= Settings.LowBatteryWarn;
        if (low && l.LowBatteryFlash) (r, g, b) = (now % 1.2) < 0.6 ? (1.0, 0.0, 0.0) : (0.0, 0.0, 0.0);
        double k = l.Mode == LightMode.Off ? 0 : Math.Clamp(l.Brightness, 0.05, 1);
        pad.SetLightbar((byte)(r * k * 255), (byte)(g * k * 255), (byte)(b * k * 255));
        if (now - rt.LastResend > 1.0) { rt.LastResend = now; pad.Resend(); }

        // Pil uyarisi (bir kez)
        lock (_lowWarned)
        {
            if (low && _lowWarned.Add(pad)) Say("Pil azaldı", "Battery low", $"{pad.DisplayName}: {s.BatteryPercent}%", true);
            if (!low && s.BatteryPercent > Settings.LowBatteryWarn + 5) _lowWarned.Remove(pad);
        }

        // Bosta kalan Bluetooth pad'i kapat
        if (Settings.IdleDisconnectMinutes > 0 && pad.Bluetooth && rt.IdleSeconds > Settings.IdleDisconnectMinutes * 60)
        {
            rt.LastActivity = double.MaxValue / 2;   // tekrar denemesin
            if (pad.DisconnectBluetooth()) Say("Pad kapatıldı", "Controller turned off",
                Lang.T($"{Settings.IdleDisconnectMinutes} dk boyunca kullanılmadı.", $"Idle for {Settings.IdleDisconnectMinutes} min."));
        }
    }

    public static byte PlayerMask(int slot) => slot switch { 1 => 0x04, 2 => 0x0A, 3 => 0x15, 4 => 0x1B, _ => 0x1F };

    static (double, double, double) Hsv(double h)
    {
        double x = 1 - Math.Abs(h / 60 % 2 - 1);
        return (int)(h / 60) switch
        {
            0 => (1, x, 0), 1 => (x, 1, 0), 2 => (0, 1, x), 3 => (0, x, 1), 4 => (x, 0, 1), _ => (1, 0, x),
        };
    }

    public static (double, double, double) Hex(string hex)
    {
        try
        {
            var c = Convert.FromHexString(hex.TrimStart('#'));
            if (c.Length >= 3) return (c[0] / 255.0, c[1] / 255.0, c[2] / 255.0);
        }
        catch { }
        return (0, 0.25, 1);
    }

    // ---------- HidHide ----------

    void HideNewPad(PadDevice pad)
    {
        string id = HidHide.InstanceId(pad.Path);
        lock (_hiddenLock) { if (!_hiddenThisSession.Add(id)) return; }
        Task.Run(() => HidHide.Apply([id], true));
    }

    readonly object _hiddenLock = new();
    readonly HashSet<string> _hiddenThisSession = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ayar degisince bagli tum pad'ler icin gizlemeyi acar/kapatir. Sonuc: basarili mi.</summary>
    public Task<bool> ApplyHidHideAsync(bool hide)
    {
        var ids = _runtimes.Keys.Select(p => HidHide.InstanceId(p.Path)).ToList();
        lock (_hiddenLock) { if (hide) _hiddenThisSession.UnionWith(ids); else _hiddenThisSession.Clear(); }
        return Task.Run(() => HidHide.Apply(ids, hide));
    }

    public void SaveNow() => SettingsStore.Save(Settings);

    public void Dispose()
    {
        _timer?.Dispose();
        _manager.Dispose();
        foreach (var rt in _runtimes.Values) rt.Dispose();
        _runtimes.Clear();
        _virtual.Dispose();
        try { SettingsStore.Save(Settings); } catch { }
    }
}
