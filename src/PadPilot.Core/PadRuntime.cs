using System.Diagnostics;

namespace PadPilot.Core;

public static class StickMath
{
    public static float Norm(byte v) => Math.Clamp((v - 127.5f) / 127.5f, -1f, 1f);

    /// <summary>Ham analog (-1..1, Y yukari) -> olu bolge, dis bolge, egri, anti olu bolge, hassasiyet uygulanmis deger.</summary>
    public static (float X, float Y) Process(float x, float y, StickSettings s)
    {
        if (s.InvertX) x = -x;
        if (s.InvertY) y = -y;
        float mag = MathF.Sqrt(x * x + y * y);
        float dz = (float)Math.Clamp(s.Deadzone, 0, 0.9);
        if (mag <= dz || mag < 1e-5f) return (0, 0);
        float outer = (float)Math.Max(s.OuterZone, dz + 0.05);
        float t = Math.Clamp((mag - dz) / (outer - dz), 0f, 1f);
        t = Curve(t, s.Curve);
        float anti = (float)Math.Clamp(s.AntiDeadzone, 0, 0.9);
        t = anti + (1 - anti) * t;
        t = Math.Min(1f, t * (float)s.Sensitivity);
        return (x / mag * t, y / mag * t);
    }

    public static float Curve(float t, StickCurve c) => c switch
    {
        StickCurve.Precise => MathF.Pow(t, 1.8f),            // merkezde ince nisan
        StickCurve.Smooth => t * t * (3 - 2 * t),             // S egrisi
        StickCurve.Aggressive => MathF.Sqrt(t),              // hizli tepki
        _ => t,
    };

    public static byte Trigger(byte raw, TriggerSettings s)
    {
        double t = raw / 255.0, dz = Math.Clamp(s.Deadzone, 0, 0.9), max = Math.Max(s.MaxZone, dz + 0.02);
        if (t <= dz) return 0;
        return (byte)Math.Round(Math.Clamp((t - dz) / (max - dz), 0, 1) * 255);
    }
}

/// <summary>Bir pad'in canli isleme durumu: esleme, turbo, uzun basis, gyro, touchpad, flick stick.</summary>
public sealed class PadRuntime : IDisposable
{
    const float GyroLsbPerDps = 16.4f;
    const double LongPress = 0.35, TapPulse = 0.07;

    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static double Now => Clock.Elapsed.TotalSeconds;

    public PadDevice Pad { get; }
    public int Slot { get; set; }
    public IVirtualTarget? Target { get; set; }

    /// <summary>Kullanici arayuzu icin: islenmis analoglar ve son cikis.</summary>
    public float OutLX, OutLY, OutRX, OutRY;
    public VState LastOutput;
    public float GyroYawDps, GyroPitchDps;
    public bool GyroActive, GyroCalibrating;
    public double LastActivity = Now;
    public double IdleSeconds => Now - LastActivity;
    /// <summary>Motorun son periyodik cikis yenilemesi (motor saatine gore).</summary>
    public double LastResend;

    /// <summary>Profil degistirme gibi motorun ele alacagi eylemler.</summary>
    public event Action<PadRuntime, string>? ActionRequested;

    readonly double[] _start = new double[32], _pulse = new double[32];
    readonly bool[] _was = new bool[32], _long = new bool[32];
    readonly HashSet<string> _held = [], _wanted = [], _actions = [], _prevActions = [];
    double _last = Now;
    bool _gyroEnabled = true, _gyroToggled, _gyroBtnWas;
    float _biasX, _biasY, _biasZ, _emaX, _emaY, _emaZ, _still;
    double _calibUntil; float _calSumX, _calSumY, _calSumZ; int _calN;
    float _remX, _remY;
    // touchpad
    bool _touchWas, _twoWas; short _tx, _ty; double _touchStart; float _touchMoved; string? _touchClick;
    // flick stick
    bool _flicking; float _flickTarget, _flickDone, _lastAngle; double _flickStart;
    double _psOptStart = -1;

    public PadRuntime(PadDevice pad) { Pad = pad; }

    public void CalibrateGyro() { _calibUntil = Now + 1.5; _calSumX = _calSumY = _calSumZ = 0; _calN = 0; }
    public void ToggleGyro() => _gyroEnabled = !_gyroEnabled;

    public void Process(in PadState s, Profile p, AppSettings app)
    {
        double now = Now;
        float dt = (float)Math.Clamp(now - _last, 0, 0.05);
        _last = now;
        var v = new VState();
        _wanted.Clear(); _actions.Clear();
        float mx = 0, my = 0;

        // --- Analoglar ---
        float rlx = StickMath.Norm(s.LX), rly = -StickMath.Norm(s.LY), rrx = StickMath.Norm(s.RX), rry = -StickMath.Norm(s.RY);
        var (lx, ly) = StickMath.Process(rlx, rly, p.LeftStick);
        var (rx, ry) = StickMath.Process(rrx, rry, p.RightStick);
        OutLX = lx; OutLY = ly; OutRX = rx; OutRY = ry;
        ApplyStick(p.LeftStick, lx, ly, true, ref v, ref mx, ref my, dt, now);
        ApplyStick(p.RightStick, rx, ry, false, ref v, ref mx, ref my, dt, now);
        if (s.Buttons != PadButtons.None || Math.Abs(rlx) > 0.25 || Math.Abs(rly) > 0.25 || Math.Abs(rrx) > 0.25 || Math.Abs(rry) > 0.25 || s.L2 > 40 || s.R2 > 40)
            LastActivity = now;

        // --- Tetikler ---
        byte lt = StickMath.Trigger(s.L2, p.L2), rt = StickMath.Trigger(s.R2, p.R2);

        // --- Tuslar ---
        bool touchMouse = p.Touch.Mode == TouchMode.Mouse && s.FullReport;
        for (int i = 0; i < p.Buttons.Count; i++)
        {
            var m = p.Buttons[i];
            if (m.Button == PadButtons.Touchpad && touchMouse) continue;
            bool down = (s.Buttons & m.Button) != 0;
            if (down && !_was[i]) { _start[i] = now; _long[i] = false; }
            string? active = null;
            bool isTap = true;
            if (m.Hold.Length > 0)
            {
                if (down && now - _start[i] >= LongPress) { active = m.Hold; _long[i] = true; isTap = false; }
                else if (!down && _was[i] && !_long[i]) _pulse[i] = now + TapPulse;
                if (!down && now < _pulse[i]) active = m.Tap;
            }
            else if (down) active = m.Tap;
            _was[i] = down;
            if (active == null) continue;
            if (m.Turbo && isTap && down && ((int)((now - _start[i]) * 16) & 1) == 1) continue;   // ~8 basis/sn
            Resolve(active, m.Button, ref v);
        }
        if (p.Buttons.FirstOrDefault(b => b.Button == PadButtons.L2) is { Tap.Length: > 0 }) lt = 0;
        if (p.Buttons.FirstOrDefault(b => b.Button == PadButtons.R2) is { Tap.Length: > 0 }) rt = 0;
        v.LT = lt; v.RT = rt;

        // --- Gyro ---
        UpdateGyro(s, p.Gyro, ref v, ref mx, ref my, dt, now);

        // --- Touchpad ---
        if (touchMouse) UpdateTouch(s, p.Touch, ref mx, ref my, now);
        else { _touchWas = false; _touchClick = null; }

        // --- Ozel: PS + Options basili tut = Bluetooth'u kapat ---
        if (app.PsOptionsDisconnect && Pad.Bluetooth && (s.Buttons & (PadButtons.PS | PadButtons.Options)) == (PadButtons.PS | PadButtons.Options))
        {
            if (_psOptStart < 0) _psOptStart = now;
            else if (now - _psOptStart > 1.0) { _psOptStart = double.MaxValue; _actions.Add("a:Disconnect"); }
        }
        else _psOptStart = -1;

        // --- Ciktilar ---
        _remX += mx; _remY += my;
        int ix = (int)_remX, iy = (int)_remY;
        _remX -= ix; _remY -= iy;
        InputSim.MouseMove(ix, iy);
        SyncHeld();
        foreach (var a in _actions) if (!_prevActions.Contains(a)) Fire(a);
        _prevActions.Clear(); _prevActions.UnionWith(_actions);

        LastOutput = v;
        try { Target?.Submit(v); } catch { /* sanal pad koptu; motor yeniden olusturur */ }
    }

    void Resolve(string code, PadButtons src, ref VState v)
    {
        if (code.Length == 0) { v.Buttons |= Targets.DefaultButton(src); return; }
        if (code == "none") return;
        switch (code[0])
        {
            case 'x': v.Buttons |= Targets.ParsePad(code); break;
            case 'k': case 'm': _wanted.Add(code); break;
            case 'a': _actions.Add(code); break;
        }
    }

    void ApplyStick(StickSettings st, float x, float y, bool left, ref VState v, ref float mx, ref float my, float dt, double now)
    {
        switch (st.Mode)
        {
            case StickMode.Mouse:
                mx += x * st.MouseSpeed * dt; my -= y * st.MouseSpeed * dt;
                break;
            case StickMode.Flick:
                Flick(st, x, y, ref mx, now);
                break;
            case StickMode.Wasd:
            case StickMode.Arrows:
                bool w = st.Mode == StickMode.Wasd;
                if (y > 0.5f) _wanted.Add(w ? "k:87" : "k:38");
                if (y < -0.5f) _wanted.Add(w ? "k:83" : "k:40");
                if (x < -0.5f) _wanted.Add(w ? "k:65" : "k:37");
                if (x > 0.5f) _wanted.Add(w ? "k:68" : "k:39");
                break;
            default:
                if (left) { v.LX = x; v.LY = y; } else { v.RX = x; v.RY = y; }
                break;
        }
    }

    // Flick stick: analog itildigi yone aninda donus, sonra analogu cevirerek donmeye devam.
    void Flick(StickSettings st, float x, float y, ref float mx, double now)
    {
        float pxPerDeg = st.Flick360 / 360f;
        float mag = MathF.Sqrt(x * x + y * y);
        if (mag >= 0.85f)
        {
            float angle = MathF.Atan2(x, y) * 180f / MathF.PI;
            if (!_flicking) { _flicking = true; _flickTarget = angle; _flickDone = 0; _flickStart = now; }
            else
            {
                float d = angle - _lastAngle;
                if (d > 180) d -= 360; else if (d < -180) d += 360;
                mx += d * pxPerDeg;
            }
            _lastAngle = angle;
        }
        else _flicking = false;
        if (_flickDone < 1 && _flickStart > 0)
        {
            float t = (float)Math.Min(1, (now - _flickStart) / 0.1);
            float eased = 1 - (1 - t) * (1 - t);
            mx += (eased - _flickDone) * _flickTarget * pxPerDeg;
            _flickDone = eased;
        }
    }

    void UpdateGyro(in PadState s, GyroSettings g, ref VState v, ref float mx, ref float my, float dt, double now)
    {
        if (!s.FullReport) { GyroActive = false; return; }
        float gx = s.GyroX, gy = s.GyroY, gz = s.GyroZ;

        // Kalibrasyon: elle (1,5 sn ortalama) ve otomatik (pad hareketsizken sapmayi yavasca ogren).
        GyroCalibrating = now < _calibUntil;
        if (GyroCalibrating)
        {
            _calSumX += gx; _calSumY += gy; _calSumZ += gz; _calN++;
            if (now + 0.02 >= _calibUntil && _calN > 10) { _biasX = _calSumX / _calN; _biasY = _calSumY / _calN; _biasZ = _calSumZ / _calN; }
        }
        _emaX += (gx - _emaX) * 0.05f; _emaY += (gy - _emaY) * 0.05f; _emaZ += (gz - _emaZ) * 0.05f;
        float dev = Math.Max(Math.Abs(gx - _emaX), Math.Max(Math.Abs(gy - _emaY), Math.Abs(gz - _emaZ)));
        if (dev < 25) { _still += dt; if (_still > 1.2f) { _biasX += (_emaX - _biasX) * 0.02f; _biasY += (_emaY - _biasY) * 0.02f; _biasZ += (_emaZ - _biasZ) * 0.02f; } }
        else _still = 0;

        float pitch = (gx - _biasX) / GyroLsbPerDps;
        float yawRaw = (gy - _biasY) / GyroLsbPerDps, roll = (gz - _biasZ) / GyroLsbPerDps;
        float yaw = g.Axis switch { GyroAxis.Roll => -roll, GyroAxis.Both => yawRaw - roll, _ => yawRaw };
        GyroYawDps = yaw; GyroPitchDps = pitch;

        bool btn = (s.Buttons & g.ActivationButton) != 0;
        if (g.Activation == GyroActivation.Toggle && btn && !_gyroBtnWas) _gyroToggled = !_gyroToggled;
        _gyroBtnWas = btn;
        bool on = g.Mode != GyroMode.Off && _gyroEnabled && !GyroCalibrating && g.Activation switch
        {
            GyroActivation.Always => true,
            GyroActivation.WhileHeld => btn,
            GyroActivation.WhileNotHeld => !btn,
            _ => _gyroToggled,
        };
        GyroActive = on;
        if (!on) return;

        // Yumusatma: cok kucuk hizlar (el titremesi) orantili olarak bastirilir.
        float speed = MathF.Sqrt(yaw * yaw + pitch * pitch), thr = (float)g.Smoothing * 3;
        if (thr > 0 && speed < thr) { float k = speed / thr; yaw *= k; pitch *= k; }
        float sx = g.InvertX ? 1 : -1, sy = g.InvertY ? 1 : -1;

        if (g.Mode == GyroMode.Mouse)
        {
            mx += sx * yaw * dt * (float)g.SensX;
            my += sy * pitch * dt * (float)g.SensY;
        }
        else
        {
            float gain = (float)g.StickGain / 150f;   // ~150 derece/sn = tam itis
            v.RX = Math.Clamp(v.RX + sx * yaw * gain, -1, 1);
            v.RY = Math.Clamp(v.RY - sy * pitch * gain, -1, 1);
        }
    }

    void UpdateTouch(in PadState s, TouchSettings t, ref float mx, ref float my, double now)
    {
        bool touch = s.Touch0, two = s.Touch0 && s.Touch1;
        if (touch && _touchWas)
        {
            float dx = s.Touch0X - _tx, dy = s.Touch0Y - _ty;
            _touchMoved += Math.Abs(dx) + Math.Abs(dy);
            if (two && _twoWas && t.TwoFingerScroll) InputSim.Wheel((int)(-dy * 4 * t.Sensitivity));
            else if (!two && !_twoWas) { mx += dx * 0.6f * (float)t.Sensitivity; my += dy * 0.6f * (float)t.Sensitivity; }
        }
        if (touch && !_touchWas) { _touchStart = now; _touchMoved = 0; }
        if (!touch && _touchWas && t.TapToClick && now - _touchStart < 0.18 && _touchMoved < 40 && _touchClick == null)
        {
            InputSim.MouseButton("L", true); InputSim.MouseButton("L", false);
        }
        _touchWas = touch; _twoWas = two;
        if (touch) { _tx = s.Touch0X; _ty = s.Touch0Y; }

        bool click = (s.Buttons & PadButtons.Touchpad) != 0;
        if (click && _touchClick == null) _touchClick = two || (t.SplitClick && s.Touch0X > 960) ? "m:R" : "m:L";
        if (!click) _touchClick = null;
        if (_touchClick != null) _wanted.Add(_touchClick);
    }

    void SyncHeld()
    {
        foreach (var c in _held.ToArray())
            if (!_wanted.Contains(c)) { Emit(c, false); _held.Remove(c); }
        foreach (var c in _wanted)
            if (_held.Add(c)) Emit(c, true);
    }

    static void Emit(string code, bool down)
    {
        if (code[0] == 'k' && int.TryParse(code.AsSpan(2), out int vk)) InputSim.Key(vk, down);
        else if (code[0] == 'm') InputSim.MouseButton(code[2..], down);
    }

    void Fire(string code)
    {
        switch (code)
        {
            case "a:GyroToggle": ToggleGyro(); break;
            case "a:Snip": InputSim.Chord(0x5B, 0xA0, 0x53); break;
            case "a:Desktop": InputSim.Chord(0x5B, 0x44); break;
            case "a:Osk": try { System.Diagnostics.Process.Start(new ProcessStartInfo("osk.exe") { UseShellExecute = true }); } catch { } break;
            default: ActionRequested?.Invoke(this, code); break;
        }
    }

    /// <summary>Basili kalan klavye/fare tuslarini birakir.</summary>
    public void ReleaseAll()
    {
        _wanted.Clear();
        SyncHeld();
    }

    public void Dispose()
    {
        ReleaseAll();
        Target?.Dispose();
        Target = null;
    }
}
