using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PadPilot.Core.Hid;

namespace PadPilot.Core;

public enum PadKind { DualShock4, DualSense, DualSenseEdge }

/// <summary>Tek bir fiziksel pad: okuma is parcacigi + cikis raporlari.</summary>
public sealed class PadDevice : IDisposable
{
    const ushort SonyVid = 0x054C;
    const string BluetoothHidGuid = "00001124-0000-1000-8000-00805f9b34fb";
    static readonly string[] WriteMethods = ["WriteFile", "WriteFile (dolgulu)", "SetOutputReport"];

    readonly SafeFileHandle _read, _write;
    readonly int _inLen, _outLen, _featLen;
    readonly object _stateLock = new(), _outLock = new();
    readonly byte[] _outBuf;
    byte[]? _padded;
    Thread? _thread;
    volatile bool _stop;
    PadState _state;
    PadOutput _out;
    byte _seq;
    int _writeMethod;

    public string Path { get; }
    public PadKind Kind { get; }
    public bool Bluetooth { get; }
    public double ReportRateHz { get; private set; }
    public string OutputStatus { get; private set; } = "Çıkış: henüz gönderilmedi";
    public string DisplayName => Kind switch
    {
        PadKind.DualSense => "DualSense",
        PadKind.DualSenseEdge => "DualSense Edge",
        _ => "DualShock 4",
    };
    bool IsDualSense => Kind != PadKind.DualShock4;

    public PadState State { get { lock (_stateLock) return _state; } }

    /// <summary>Okuma is parcacigindan, her giris raporunda cagrilir.</summary>
    public event Action<PadDevice, PadState>? StateChanged;
    public event Action<PadDevice>? Disconnected;

    PadDevice(string path, PadKind kind, bool bluetooth, SafeFileHandle read, SafeFileHandle write, HidNative.HIDP_CAPS caps)
    {
        Path = path; Kind = kind; Bluetooth = bluetooth; _read = read; _write = write;
        _inLen = Math.Max((int)caps.InputReportByteLength, 64);
        _outLen = caps.OutputReportByteLength;
        _featLen = Math.Max((int)caps.FeatureReportByteLength, 64);
        _outBuf = new byte[bluetooth ? OutputBuilder.BluetoothLength : Math.Max(_outLen, IsDualSense ? 48 : 32)];
    }

    /// <param name="notOurs">true: bu yol desteklenen bir pad degil, tekrar denemeye gerek yok.</param>
    public static PadDevice? TryOpen(string path, out bool notOurs)
    {
        notOurs = false;
        PadKind kind;
        HidNative.HIDP_CAPS caps;
        using (var probe = HidNative.CreateFile(path, 0, HidNative.FILE_SHARE_RW, 0, HidNative.OPEN_EXISTING, 0, 0))
        {
            if (probe.IsInvalid) return null;
            notOurs = true;
            var attr = new HidNative.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HidNative.HIDD_ATTRIBUTES>() };
            if (!HidNative.HidD_GetAttributes(probe, ref attr) || attr.VendorID != SonyVid) return null;
            switch (attr.ProductID)
            {
                case 0x0CE6: kind = PadKind.DualSense; break;
                case 0x0DF2: kind = PadKind.DualSenseEdge; break;
                case 0x05C4: case 0x09CC: case 0x0BA0: kind = PadKind.DualShock4; break;
                default: return null;
            }
            if (!HidNative.HidD_GetPreparsedData(probe, out IntPtr pp)) return null;
            try { HidNative.HidP_GetCaps(pp, out caps); }
            finally { HidNative.HidD_FreePreparsedData(pp); }
            if (caps.UsagePage != 1 || caps.Usage != 5) return null;   // yalnizca oyun kumandasi koleksiyonu
        }

        notOurs = false;   // bizim pad; acilamazsa (baska uygulama kilitlemis olabilir) sonraki taramada yeniden dene
        var read = HidNative.CreateFile(path, HidNative.GENERIC_READ, HidNative.FILE_SHARE_RW, 0, HidNative.OPEN_EXISTING, 0, 0);
        var write = HidNative.CreateFile(path, HidNative.GENERIC_READ | HidNative.GENERIC_WRITE, HidNative.FILE_SHARE_RW, 0, HidNative.OPEN_EXISTING, 0, 0);
        if (read.IsInvalid || write.IsInvalid) { read.Dispose(); write.Dispose(); return null; }
        bool bluetooth = path.Contains(BluetoothHidGuid, StringComparison.OrdinalIgnoreCase);
        return new PadDevice(path, kind, bluetooth, read, write, caps);
    }

    public void Start()
    {
        try { ReadMac(); } catch { }
        if (Bluetooth)
        {
            // Kalibrasyon ozellik raporunu okumak pad'i basit moddan tam rapor moduna gecirir.
            var feat = new byte[_featLen];
            feat[0] = 0x05;
            HidNative.HidD_GetFeature(_write, feat, feat.Length);
        }
        lock (_outLock)
        {
            if (IsDualSense)
            {
                OutputBuilder.BuildDualSenseSetup(_outBuf, Bluetooth, NextSeq());
                Send();
            }
            _out = new PadOutput { R = 0, G = 64, B = 255, PlayerLeds = 0x04 };
        }
        Flush();
        _thread = new Thread(ReadLoop) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "PadPilot.Read" };
        _thread.Start();
    }

    void ReadLoop()
    {
        var buf = new byte[_inLen];
        var sw = Stopwatch.StartNew();
        int count = 0;
        PadState s = default;
        while (!_stop)
        {
            if (!HidNative.ReadFile(_read, buf, (uint)buf.Length, out uint n, 0) || n == 0) break;
            var report = buf.AsSpan(0, (int)n);
            bool ok = IsDualSense ? ReportParser.ParseDualSense(report, Bluetooth, ref s) : ReportParser.ParseDualShock4(report, Bluetooth, ref s);
            if (!ok) continue;
            lock (_stateLock) _state = s;
            count++;
            long ms = sw.ElapsedMilliseconds;
            if (ms >= 1000) { ReportRateHz = count * 1000.0 / ms; count = 0; sw.Restart(); }
            try { StateChanged?.Invoke(this, s); } catch { /* dinleyici hatasi okuma dongusunu durdurmasin */ }
        }
        if (!_stop) Disconnected?.Invoke(this);
    }

    public void SetRumble(byte strong, byte weak)
    {
        lock (_outLock) { _out.RumbleStrong = strong; _out.RumbleWeak = weak; }
        Flush();
    }

    public void SetLightbar(byte r, byte g, byte b)
    {
        lock (_outLock)
        {
            if (_out.R == r && _out.G == g && _out.B == b) return;
            _out.R = r; _out.G = g; _out.B = b;
        }
        Flush();
    }

    /// <summary>DualSense adaptif tetikleri; DualShock 4'te etkisiz.</summary>
    public void SetTriggers(byte[] right, byte[] left)
    {
        if (!IsDualSense) return;
        lock (_outLock) { _out.TriggerR = right; _out.TriggerL = left; }
        Flush();
    }

    /// <summary>DualSense'in alttaki 5 oyuncu LED'i (bit maskesi).</summary>
    public void SetPlayerLeds(byte mask)
    {
        if (!IsDualSense) return;
        lock (_outLock) { if (_out.PlayerLeds == mask) return; _out.PlayerLeds = mask; }
        Flush();
    }

    public void SetMicLed(bool on)
    {
        if (!IsDualSense) return;
        lock (_outLock) { byte v = (byte)(on ? 1 : 0); if (_out.MicLed == v) return; _out.MicLed = v; }
        Flush();
    }

    /// <summary>Son cikis durumunu yeniden gonderir. DualSense acilistaki isik animasyonu bitene kadar
    /// renk komutlarini yok sayar; ayrica Bluetooth'ta kaybolan paketlere karsi periyodik cagrilir.</summary>
    public void Resend() => Flush();

    public bool HasAdaptiveTriggers => IsDualSense;
    public bool HasPlayerLeds => IsDualSense;

    /// <summary>Bluetooth MAC adresi (yalnizca Bluetooth baglantida, bilinmiyorsa null).</summary>
    public ulong? MacAddress { get; private set; }

    /// <summary>Bluetooth pad'i kapatir (PS tusuna 10 sn basmaya gerek kalmaz).</summary>
    public bool DisconnectBluetooth() => Bluetooth && MacAddress is ulong mac && BluetoothNative.Disconnect(mac);

    void ReadMac()
    {
        if (!Bluetooth) return;
        var sb = new char[64];
        if (HidNative.HidD_GetSerialNumberString(_write, sb, sb.Length * 2))
        {
            string s = new string(sb).TrimEnd('\0').Replace(":", "").Replace("-", "");
            if (s.Length == 12 && ulong.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out ulong v)) { MacAddress = v; return; }
        }
        // Yedek: eslesme bilgisi ozellik raporu (DualSense 0x09, DS4 0x12); MAC ters sirada 1..6. baytlarda.
        var f = new byte[_featLen];
        f[0] = IsDualSense ? (byte)0x09 : (byte)0x12;
        if (HidNative.HidD_GetFeature(_write, f, f.Length))
        {
            ulong v = 0;
            for (int i = 6; i >= 1; i--) v = (v << 8) | f[i];
            if (v != 0) MacAddress = v;
        }
    }

    void Flush()
    {
        lock (_outLock)
        {
            if (IsDualSense) OutputBuilder.BuildDualSense(_outBuf, Bluetooth, NextSeq(), _out);
            else OutputBuilder.BuildDualShock4(_outBuf, Bluetooth, _out);
            Send();
        }
    }

    byte NextSeq() { byte s = _seq; _seq = (byte)((_seq + 1) & 0x0F); return s; }

    // Windows HID yiginindaki farkliliklara karsi uc yontem sirayla denenir; calisan hatirlanir.
    void Send()
    {
        if (_stop) return;
        int error = 0;
        for (int m = _writeMethod; m < 3; m++)
        {
            bool ok;
            if (m == 0) ok = HidNative.WriteFile(_write, _outBuf, (uint)_outBuf.Length, out _, 0);
            else if (m == 1)
            {
                if (_outLen <= _outBuf.Length) continue;
                _padded ??= new byte[_outLen];
                _outBuf.CopyTo(_padded, 0);
                ok = HidNative.WriteFile(_write, _padded, (uint)_padded.Length, out _, 0);
            }
            else ok = HidNative.HidD_SetOutputReport(_write, _outBuf, _outBuf.Length);

            if (ok) { _writeMethod = m; OutputStatus = $"Çıkış: çalışıyor ({WriteMethods[m]})"; return; }
            error = Marshal.GetLastPInvokeError();
        }
        OutputStatus = $"Çıkış: gönderilemedi (Windows hata kodu {error})";
    }

    public void Dispose()
    {
        if (_stop) return;
        try { SetRumble(0, 0); } catch { }
        _stop = true;
        HidNative.CancelIoEx(_read, 0);
        if (_thread != null && Thread.CurrentThread != _thread) _thread.Join(500);
        _read.Dispose();
        _write.Dispose();
    }
}
