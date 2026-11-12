using System.Buffers.Binary;

namespace PadPilot.Core;

[Flags]
public enum PadButtons : uint
{
    None = 0, Cross = 1, Circle = 2, Square = 4, Triangle = 8,
    L1 = 16, R1 = 32, L2 = 64, R2 = 128, Share = 256, Options = 512, L3 = 1024, R3 = 2048,
    PS = 4096, Touchpad = 8192, Mute = 16384,
    Up = 1 << 15, Down = 1 << 16, Left = 1 << 17, Right = 1 << 18,
}

/// <summary>Iki pad turu icin ortak durum modeli.</summary>
public struct PadState
{
    public byte LX, LY, RX, RY, L2, R2;
    public PadButtons Buttons;
    public byte BatteryPercent;
    public bool Charging;
    /// <summary>false ise pad Bluetooth "basit mod"da: pil, jiroskop ve dokunmatik verisi yok.</summary>
    public bool FullReport;
    public short GyroX, GyroY, GyroZ, AccelX, AccelY, AccelZ;
    public bool Touch0, Touch1;
    public short Touch0X, Touch0Y, Touch1X, Touch1Y;
}

public struct PadOutput
{
    public byte RumbleStrong, RumbleWeak, R, G, B, PlayerLeds, MicLed;
}

public static class Crc32
{
    static readonly uint[] Table = Build();

    static uint[] Build()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return ~c;
    }

    /// <summary>Bluetooth raporlari: once tohum bayti (0xA1 giris, 0xA2 cikis, 0xA3 ozellik), sonra veri.</summary>
    public static uint Compute(byte seed, ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFF;
        c = Table[(c ^ seed) & 0xFF] ^ (c >> 8);
        foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return ~c;
    }
}

/// <summary>Giris raporu ayristirma. Rapor duzeni: Linux hid-playstation.c.</summary>
public static class ReportParser
{
    public static bool ParseDualSense(ReadOnlySpan<byte> r, bool bluetooth, ref PadState s)
    {
        int o;
        if (!bluetooth) { if (r.Length < 54 || r[0] != 0x01) return false; o = 1; }
        else if (r.Length >= 55 && r[0] == 0x31) o = 2;
        else if (r.Length >= 10 && r[0] == 0x01) return ParseSimple(r, ref s);
        else return false;

        s.LX = r[o]; s.LY = r[o + 1]; s.RX = r[o + 2]; s.RY = r[o + 3]; s.L2 = r[o + 4]; s.R2 = r[o + 5];
        s.Buttons = Face(r[o + 7]) | Shoulder(r[o + 8]) | Extra(r[o + 9], true);
        ReadMotion(r.Slice(o + 15, 12), ref s);
        ReadTouch(r.Slice(o + 32, 8), ref s);
        int cap = r[o + 52] & 0x0F, chg = r[o + 52] >> 4;
        s.BatteryPercent = chg == 2 ? (byte)100 : (byte)Math.Min(cap * 10 + 5, 100);
        s.Charging = chg == 1;
        s.FullReport = true;
        return true;
    }

    public static bool ParseDualShock4(ReadOnlySpan<byte> r, bool bluetooth, ref PadState s)
    {
        int o;
        if (r.Length >= 45 && r[0] == 0x11 && bluetooth) o = 3;
        else if (r.Length >= 43 && r[0] == 0x01 && !bluetooth) o = 1;
        else if (r.Length >= 10 && r[0] == 0x01) return ParseSimple(r, ref s);
        else return false;

        s.LX = r[o]; s.LY = r[o + 1]; s.RX = r[o + 2]; s.RY = r[o + 3];
        s.Buttons = Face(r[o + 4]) | Shoulder(r[o + 5]) | Extra(r[o + 6], false);
        s.L2 = r[o + 7]; s.R2 = r[o + 8];
        ReadMotion(r.Slice(o + 12, 12), ref s);
        ReadTouch(r.Slice(o + 34, 8), ref s);
        int cap = r[o + 29] & 0x0F;
        bool cable = (r[o + 29] & 0x10) != 0;
        s.BatteryPercent = cap < 10 ? (byte)(cap * 10 + 5) : (byte)100;
        s.Charging = cable && cap < 10;
        s.FullReport = true;
        return true;
    }

    // Bluetooth basit mod (rapor 0x01, 10 bayt): yalnizca analoglar, tuslar ve tetikler.
    static bool ParseSimple(ReadOnlySpan<byte> r, ref PadState s)
    {
        s.LX = r[1]; s.LY = r[2]; s.RX = r[3]; s.RY = r[4];
        s.Buttons = Face(r[5]) | Shoulder(r[6]) | Extra(r[7], false);
        s.L2 = r[8]; s.R2 = r[9];
        s.FullReport = false;
        return true;
    }

    static PadButtons Face(byte b)
    {
        PadButtons p = (b & 0x0F) switch
        {
            0 => PadButtons.Up, 1 => PadButtons.Up | PadButtons.Right, 2 => PadButtons.Right,
            3 => PadButtons.Down | PadButtons.Right, 4 => PadButtons.Down, 5 => PadButtons.Down | PadButtons.Left,
            6 => PadButtons.Left, 7 => PadButtons.Up | PadButtons.Left, _ => PadButtons.None,
        };
        if ((b & 0x10) != 0) p |= PadButtons.Square;
        if ((b & 0x20) != 0) p |= PadButtons.Cross;
        if ((b & 0x40) != 0) p |= PadButtons.Circle;
        if ((b & 0x80) != 0) p |= PadButtons.Triangle;
        return p;
    }

    static PadButtons Shoulder(byte b)
    {
        PadButtons p = 0;
        if ((b & 0x01) != 0) p |= PadButtons.L1;
        if ((b & 0x02) != 0) p |= PadButtons.R1;
        if ((b & 0x04) != 0) p |= PadButtons.L2;
        if ((b & 0x08) != 0) p |= PadButtons.R2;
        if ((b & 0x10) != 0) p |= PadButtons.Share;
        if ((b & 0x20) != 0) p |= PadButtons.Options;
        if ((b & 0x40) != 0) p |= PadButtons.L3;
        if ((b & 0x80) != 0) p |= PadButtons.R3;
        return p;
    }

    static PadButtons Extra(byte b, bool dualSense)
    {
        PadButtons p = 0;
        if ((b & 0x01) != 0) p |= PadButtons.PS;
        if ((b & 0x02) != 0) p |= PadButtons.Touchpad;
        if (dualSense && (b & 0x04) != 0) p |= PadButtons.Mute;
        return p;
    }

    static void ReadMotion(ReadOnlySpan<byte> m, ref PadState s)
    {
        s.GyroX = BinaryPrimitives.ReadInt16LittleEndian(m);
        s.GyroY = BinaryPrimitives.ReadInt16LittleEndian(m[2..]);
        s.GyroZ = BinaryPrimitives.ReadInt16LittleEndian(m[4..]);
        s.AccelX = BinaryPrimitives.ReadInt16LittleEndian(m[6..]);
        s.AccelY = BinaryPrimitives.ReadInt16LittleEndian(m[8..]);
        s.AccelZ = BinaryPrimitives.ReadInt16LittleEndian(m[10..]);
    }

    // Her nokta 4 bayt: temas (bit7 = dokunmuyor), x alt, x ust 4 bit | y alt 4 bit, y ust.
    static void ReadTouch(ReadOnlySpan<byte> t, ref PadState s)
    {
        s.Touch0 = (t[0] & 0x80) == 0;
        s.Touch0X = (short)(((t[2] & 0x0F) << 8) | t[1]);
        s.Touch0Y = (short)((t[3] << 4) | (t[2] >> 4));
        s.Touch1 = (t[4] & 0x80) == 0;
        s.Touch1X = (short)(((t[6] & 0x0F) << 8) | t[5]);
        s.Touch1Y = (short)((t[7] << 4) | (t[6] >> 4));
    }
}

/// <summary>Cikis raporu olusturma (titresim, isik). Adaptif tetikler Asama 2'de eklenecek.</summary>
public static class OutputBuilder
{
    public const int BluetoothLength = 78;

    /// <summary>DualSense acilisindaki mavi isik animasyonunu kapatan tek seferlik paket.</summary>
    public static int BuildDualSenseSetup(Span<byte> buf, bool bluetooth, byte seq)
    {
        int c = DualSenseHeader(buf, bluetooth, seq);
        buf[c + 38] = 0x01;   // valid_flag2: isik cubugu kurulumu
        buf[c + 41] = 0x02;   // lightbar_setup: sondur
        return DualSenseFinish(buf, bluetooth, c);
    }

    public static int BuildDualSense(Span<byte> buf, bool bluetooth, byte seq, in PadOutput o)
    {
        int c = DualSenseHeader(buf, bluetooth, seq);
        buf[c] = 0x03;                    // valid_flag0: titresim + haptik secimi
        buf[c + 1] = 0x01 | 0x04 | 0x10;  // valid_flag1: mikrofon LED, isik cubugu, oyuncu LED
        buf[c + 2] = o.RumbleWeak;        // sag (hafif) motor
        buf[c + 3] = o.RumbleStrong;      // sol (agir) motor
        buf[c + 8] = o.MicLed;
        buf[c + 43] = o.PlayerLeds;
        buf[c + 44] = o.R; buf[c + 45] = o.G; buf[c + 46] = o.B;
        return DualSenseFinish(buf, bluetooth, c);
    }

    static int DualSenseHeader(Span<byte> buf, bool bluetooth, byte seq)
    {
        buf.Clear();
        if (!bluetooth) { buf[0] = 0x02; return 1; }
        buf[0] = 0x31; buf[1] = (byte)(seq << 4); buf[2] = 0x10;
        return 3;
    }

    static int DualSenseFinish(Span<byte> buf, bool bluetooth, int c)
    {
        if (!bluetooth) return c + 47;
        WriteCrc(buf);
        return BluetoothLength;
    }

    public static int BuildDualShock4(Span<byte> buf, bool bluetooth, in PadOutput o)
    {
        buf.Clear();
        int c;
        if (bluetooth) { buf[0] = 0x11; buf[1] = 0xC0 | 0x04; c = 3; }   // HID + CRC + 4 ms yoklama
        else { buf[0] = 0x05; c = 1; }
        buf[c] = 0x03;                    // valid_flag0: motor + isik
        buf[c + 3] = o.RumbleWeak;
        buf[c + 4] = o.RumbleStrong;
        buf[c + 5] = o.R; buf[c + 6] = o.G; buf[c + 7] = o.B;
        if (!bluetooth) return 32;
        WriteCrc(buf);
        return BluetoothLength;
    }

    static void WriteCrc(Span<byte> buf) =>
        BinaryPrimitives.WriteUInt32LittleEndian(buf[74..], Crc32.Compute(0xA2, buf[..74]));
}
