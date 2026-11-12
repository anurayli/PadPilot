using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PadPilot.Core;

/// <summary>Bluetooth cihazinin baglantisini keser (DS4Windows'un kullandigi yontem).</summary>
public static class BluetoothNative
{
    [StructLayout(LayoutKind.Sequential)]
    struct BLUETOOTH_FIND_RADIO_PARAMS { public int dwSize; }

    [DllImport("bthprops.cpl", SetLastError = true)]
    static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS p, out IntPtr radio);
    [DllImport("bthprops.cpl", SetLastError = true)]
    static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);
    [DllImport("bthprops.cpl", SetLastError = true)]
    static extern bool BluetoothFindRadioClose(IntPtr find);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr h, uint code, ref ulong inBuf, int inSize, IntPtr outBuf, int outSize, out int returned, IntPtr ov);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    const uint IOCTL_BTH_DISCONNECT_DEVICE = 0x41000C;

    public static bool Disconnect(ulong mac)
    {
        var p = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>() };
        IntPtr find = BluetoothFindFirstRadio(ref p, out IntPtr radio);
        if (find == IntPtr.Zero) return false;
        bool ok = false;
        try
        {
            do
            {
                ulong addr = mac;
                ok |= DeviceIoControl(radio, IOCTL_BTH_DISCONNECT_DEVICE, ref addr, 8, 0, 0, out _, 0);
                CloseHandle(radio);
            } while (!ok && BluetoothFindNextRadio(find, out radio));
        }
        finally { BluetoothFindRadioClose(find); }
        return ok;
    }
}

/// <summary>Klavye ve fare olaylari (SendInput).</summary>
public static class InputSim
{
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public int type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public int mouseData; public uint dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);

    const uint MOVE = 1, LDOWN = 2, LUP = 4, RDOWN = 8, RUP = 0x10, MDOWN = 0x20, MUP = 0x40, XDOWN = 0x80, XUP = 0x100, WHEEL = 0x800, HWHEEL = 0x1000;
    const uint KEYUP = 2, EXTENDED = 1;

    static void Send(INPUT i) => SendInput(1, [i], Marshal.SizeOf<INPUT>());
    static void Mouse(uint flags, int dx = 0, int dy = 0, int data = 0) =>
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags } } });

    public static void MouseMove(int dx, int dy) { if (dx != 0 || dy != 0) Mouse(MOVE, dx, dy); }
    public static void Wheel(int delta) { if (delta != 0) Mouse(WHEEL, data: delta); }
    public static void HWheel(int delta) { if (delta != 0) Mouse(HWHEEL, data: delta); }

    public static void MouseButton(string b, bool down)
    {
        switch (b)
        {
            case "L": Mouse(down ? LDOWN : LUP); break;
            case "R": Mouse(down ? RDOWN : RUP); break;
            case "M": Mouse(down ? MDOWN : MUP); break;
            case "X1": Mouse(down ? XDOWN : XUP, data: 1); break;
            case "X2": Mouse(down ? XDOWN : XUP, data: 2); break;
            case "WU": if (down) Wheel(120); break;
            case "WD": if (down) Wheel(-120); break;
        }
    }

    static readonly HashSet<int> Extended = [0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x5B, 0x2C,
        0xAD, 0xAE, 0xAF, 0xB0, 0xB1, 0xB3];

    public static void Key(int vk, bool down)
    {
        uint flags = (down ? 0 : KEYUP) | (Extended.Contains(vk) ? EXTENDED : 0);
        Send(new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)MapVirtualKey((uint)vk, 0), dwFlags = flags } } });
    }

    public static void Chord(params int[] vks)
    {
        foreach (int k in vks) Key(k, true);
        for (int i = vks.Length - 1; i >= 0; i--) Key(vks[i], false);
    }
}

/// <summary>Windows ile butunlesme: ondeki uygulama, baslangicta calistirma.</summary>
public static class SystemIntegration
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    /// <summary>On plandaki pencerenin exe adi (ornek "eldenring.exe").</summary>
    public static string? ForegroundExe()
    {
        try
        {
            IntPtr h = GetForegroundWindow();
            if (h == 0) return null;
            GetWindowThreadProcessId(h, out uint pid);
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName + ".exe";
        }
        catch { return null; }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetStartup(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("PadPilot", $"\"{Environment.ProcessPath}\" --tepsi");
        else k.DeleteValue("PadPilot", false);
    }
}

/// <summary>
/// HidHide: gercek pad'leri oyunlardan gizler (cift giris olmasin). Ayarlar kalicidir; yonetici izni ister,
/// bu yuzden HidHideCLI tek seferlik UAC onayi ile calistirilir.
/// </summary>
public static class HidHide
{
    static readonly string[] CliPaths =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
        @"C:\Program Files\Nefarius Software Solutions e.U\HidHide\x64\HidHideCLI.exe",
    ];

    public static string? CliPath => CliPaths.FirstOrDefault(File.Exists);
    public static bool Installed => CliPath != null;

    /// <summary>HID arayuz yolundan aygit ornek kimligi: \\?\hid#vid_054c&amp;pid_0ce6#7&amp;..#{guid} -> HID\VID_054C&amp;PID_0CE6\7&amp;..</summary>
    public static string InstanceId(string path)
    {
        string s = path;
        if (s.StartsWith(@"\\?\")) s = s[4..];
        int g = s.LastIndexOf("#{", StringComparison.Ordinal);
        if (g > 0) s = s[..g];
        return s.Replace('#', '\\').ToUpperInvariant();
    }

    /// <summary>Uygulamayi beyaz listeye ekler, verilen aygitlari gizler (veya gostermeye dondurur), gizlemeyi acar.</summary>
    public static bool Apply(IEnumerable<string> instanceIds, bool hide)
    {
        string? cli = CliPath;
        if (cli == null) return false;
        var args = new List<string> { "--app-reg", Quote(Environment.ProcessPath!) };
        foreach (var id in instanceIds) { args.Add(hide ? "--dev-hide" : "--dev-unhide"); args.Add(Quote(id)); }
        args.Add(hide ? "--cloak-on" : "--cloak-off");
        try
        {
            using var p = Process.Start(new ProcessStartInfo(cli, string.Join(' ', args))
            { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            p?.WaitForExit(15000);
            return p?.ExitCode == 0;
        }
        catch { return false; }   // UAC reddedildi
    }

    static string Quote(string s) => "\"" + s + "\"";
}
