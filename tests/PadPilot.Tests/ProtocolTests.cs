using System.Buffers.Binary;
using PadPilot.Core;
using Xunit;

namespace PadPilot.Tests;

public class ProtocolTests
{
    [Fact]
    public void Crc32_MatchesStandardCheckValue() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

    [Fact]
    public void Crc32_SeedIsEquivalentToPrefixByte()
    {
        byte[] data = [1, 2, 3, 4, 5];
        Assert.Equal(Crc32.Compute([0xA2, .. data]), Crc32.Compute(0xA2, data));
    }

    [Fact]
    public void DualSense_Usb_Parses()
    {
        var r = new byte[64];
        r[0] = 0x01; r[1] = 0x10; r[2] = 0xF0; r[5] = 0x80;
        r[8] = 0x28;    // yon tusu bos + Cross
        r[9] = 0x01;    // L1
        r[10] = 0x05;   // PS + Mute
        r[33] = 0x80; r[37] = 0x80;   // dokunma yok
        r[53] = 0x08;   // pil 8/10, sarj olmuyor
        PadState s = default;
        Assert.True(ReportParser.ParseDualSense(r, false, ref s));
        Assert.Equal(0x10, s.LX); Assert.Equal(0xF0, s.LY); Assert.Equal(0x80, s.L2);
        Assert.Equal(PadButtons.Cross | PadButtons.L1 | PadButtons.PS | PadButtons.Mute, s.Buttons);
        Assert.Equal(85, s.BatteryPercent); Assert.False(s.Charging); Assert.True(s.FullReport);
        Assert.False(s.Touch0); Assert.False(s.Touch1);
    }

    [Fact]
    public void DualSense_Bluetooth_FullReport_Parses()
    {
        var r = new byte[78];
        r[0] = 0x31; r[2] = 0x7F; r[7] = 0xFF;
        r[9] = 0x42;    // yon: sag + Circle
        r[34] = 0x01; r[35] = 0x34; r[36] = 0x12; r[37] = 0x05;   // dokunma: x=0x234, y=0x051
        r[38] = 0x80;
        r[54] = 0x13;   // pil 3/10, sarjda
        PadState s = default;
        Assert.True(ReportParser.ParseDualSense(r, true, ref s));
        Assert.Equal(0x7F, s.LX); Assert.Equal(0xFF, s.R2);
        Assert.Equal(PadButtons.Right | PadButtons.Circle, s.Buttons);
        Assert.True(s.Touch0); Assert.Equal(0x234, s.Touch0X); Assert.Equal(0x051, s.Touch0Y); Assert.False(s.Touch1);
        Assert.Equal(35, s.BatteryPercent); Assert.True(s.Charging);
    }

    [Fact]
    public void Bluetooth_SimpleReport_Parses()
    {
        byte[] r = [0x01, 0x7D, 0x7E, 0x83, 0x82, 0x08, 0x00, 0x00, 0x00, 0x00];   // nondebug/dualsense ornegi
        PadState s = default;
        Assert.True(ReportParser.ParseDualSense(r, true, ref s));
        Assert.Equal(0x7D, s.LX); Assert.Equal(PadButtons.None, s.Buttons); Assert.False(s.FullReport);
    }

    [Fact]
    public void DualShock4_Bluetooth_Parses()
    {
        var r = new byte[547];
        r[0] = 0x11; r[3] = 0x20; r[7] = 0x18;   // yon bos + Square
        r[8] = 0x20;    // Options
        r[10] = 0x40;   // L2
        r[32] = 0x15;   // kablo takili, pil 5/10
        r[37] = 0x80; r[41] = 0x80;
        PadState s = default;
        Assert.True(ReportParser.ParseDualShock4(r, true, ref s));
        Assert.Equal(0x20, s.LX); Assert.Equal(0x40, s.L2);
        Assert.Equal(PadButtons.Square | PadButtons.Options, s.Buttons);
        Assert.Equal(55, s.BatteryPercent); Assert.True(s.Charging);
    }

    [Fact]
    public void DualShock4_Usb_Parses()
    {
        var r = new byte[64];
        r[0] = 0x01; r[4] = 0x99; r[5] = 0x88;   // RY, tuslar0: yon bos + Triangle
        r[7] = 0x03;    // PS + Touchpad
        r[30] = 0x09;
        PadState s = default;
        Assert.True(ReportParser.ParseDualShock4(r, false, ref s));
        Assert.Equal(0x99, s.RY);
        Assert.Equal(PadButtons.Triangle | PadButtons.PS | PadButtons.Touchpad, s.Buttons);
        Assert.Equal(95, s.BatteryPercent); Assert.False(s.Charging);
    }

    [Fact]
    public void DualSense_Bluetooth_Output_HasValidCrc()
    {
        var buf = new byte[78];
        var o = new PadOutput { RumbleStrong = 200, RumbleWeak = 100, R = 1, G = 2, B = 3, PlayerLeds = 4 };
        Assert.Equal(78, OutputBuilder.BuildDualSense(buf, true, 5, o));
        Assert.Equal(0x31, buf[0]); Assert.Equal(0x50, buf[1]); Assert.Equal(0x10, buf[2]);
        Assert.Equal(100, buf[5]); Assert.Equal(200, buf[6]);
        Assert.Equal(4, buf[46]); Assert.Equal(1, buf[47]); Assert.Equal(2, buf[48]); Assert.Equal(3, buf[49]);
        Assert.Equal(Crc32.Compute([0xA2, .. buf[..74]]), BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(74)));
    }

    [Fact]
    public void DualSense_Usb_Output_Layout()
    {
        var buf = new byte[48];
        var o = new PadOutput { RumbleStrong = 9, RumbleWeak = 8, R = 10, G = 20, B = 30 };
        Assert.Equal(48, OutputBuilder.BuildDualSense(buf, false, 0, o));
        Assert.Equal(0x02, buf[0]); Assert.Equal(8, buf[3]); Assert.Equal(9, buf[4]);
        Assert.Equal(10, buf[45]); Assert.Equal(20, buf[46]); Assert.Equal(30, buf[47]);
    }

    [Fact]
    public void DualShock4_Output_Layout()
    {
        var o = new PadOutput { RumbleStrong = 9, RumbleWeak = 8, R = 10, G = 20, B = 30 };
        var usb = new byte[32];
        Assert.Equal(32, OutputBuilder.BuildDualShock4(usb, false, o));
        Assert.Equal(0x05, usb[0]); Assert.Equal(8, usb[4]); Assert.Equal(9, usb[5]); Assert.Equal(10, usb[6]);

        var bt = new byte[78];
        Assert.Equal(78, OutputBuilder.BuildDualShock4(bt, true, o));
        Assert.Equal(0x11, bt[0]); Assert.Equal(0xC4, bt[1]); Assert.Equal(8, bt[6]); Assert.Equal(9, bt[7]); Assert.Equal(30, bt[10]);
        Assert.Equal(Crc32.Compute([0xA2, .. bt[..74]]), BinaryPrimitives.ReadUInt32LittleEndian(bt.AsSpan(74)));
    }

    [Fact]
    public void Axis_Conversion_CoversFullRange()
    {
        Assert.Equal(short.MaxValue, VirtualPads.Axis(1f));
        Assert.Equal(-short.MaxValue, VirtualPads.Axis(-1f));
        Assert.Equal(0, VirtualPads.Axis(0f));
        Assert.Equal(255, VirtualPads.AxisByte(1f, false));
        Assert.Equal(0, VirtualPads.AxisByte(1f, true));
        Assert.Equal(-1f, StickMath.Norm(0));
        Assert.Equal(1f, StickMath.Norm(255));
    }

    [Fact]
    public void Stick_Deadzone_And_Outer()
    {
        var s = new StickSettings { Deadzone = 0.1, OuterZone = 0.9 };
        Assert.Equal((0f, 0f), StickMath.Process(0.05f, 0.05f, s));
        var (x, y) = StickMath.Process(0.95f, 0f, s);
        Assert.Equal(1f, x, 3); Assert.Equal(0f, y, 3);
        var (hx, _) = StickMath.Process(0.5f, 0f, s);
        Assert.Equal(0.5f, hx, 3);   // (0.5-0.1)/(0.9-0.1)
        s.AntiDeadzone = 0.2;
        var (ax, _) = StickMath.Process(0.11f, 0f, s);
        Assert.True(ax > 0.2f && ax < 0.22f);
    }

    [Fact]
    public void Trigger_Deadzone_And_HairTrigger()
    {
        var t = new TriggerSettings { Deadzone = 0.1, MaxZone = 0.5 };
        Assert.Equal(0, StickMath.Trigger(20, t));
        Assert.Equal(255, StickMath.Trigger(140, t));
    }

    [Fact]
    public void TriggerEffects_Encode()
    {
        var f = TriggerEffects.Feedback(5, 8);
        Assert.Equal(0x21, f[0]);
        Assert.Equal(0xE0, f[1]); Assert.Equal(0x03, f[2]);   // bolge 5..9 etkin
        var w = TriggerEffects.Weapon(2, 6, 5);
        Assert.Equal([0x25, 0x44, 0x00, 0x04], w[..4]);
        Assert.Equal(0x05, TriggerEffects.Off()[0]);
        var v = TriggerEffects.Vibration(0, 8, 30);
        Assert.Equal(0x26, v[0]); Assert.Equal(30, v[9]);
        foreach (TriggerEffectKind k in Enum.GetValues<TriggerEffectKind>())
            Assert.Equal(11, TriggerEffects.Build(new TriggerSettings { Effect = k, Start = 9, End = 0, Strength = 99 }).Length);
    }

    [Fact]
    public void DualSense_Output_IncludesTriggers()
    {
        var buf = new byte[48];
        var o = new PadOutput { TriggerR = TriggerEffects.Weapon(2, 6, 5), TriggerL = TriggerEffects.Feedback(0, 3) };
        OutputBuilder.BuildDualSense(buf, false, 0, o);
        Assert.Equal(0x0F, buf[1]);
        Assert.Equal(0x25, buf[11]); Assert.Equal(0x21, buf[22]);
    }

    [Fact]
    public void HidHide_InstanceId()
    {
        Assert.Equal(@"HID\VID_054C&PID_0CE6\7&1A2B3C&0&0000",
            HidHide.InstanceId(@"\\?\hid#vid_054c&pid_0ce6#7&1a2b3c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
    }

    [Fact]
    public void Profile_NormalizeAndExeMatch()
    {
        var p = new Profile { Exes = "eldenring; Cyberpunk2077.exe" }.Normalize();
        Assert.Equal(Targets.MappableButtons.Length, p.Buttons.Count);
        Assert.True(p.MatchesExe("EldenRing.exe"));
        Assert.True(p.MatchesExe("cyberpunk2077.exe"));
        Assert.False(p.MatchesExe("notepad.exe"));
        var c = p.Clone("Kopya");
        Assert.Equal("Kopya", c.Name); Assert.Equal("", c.Exes);
    }
}
