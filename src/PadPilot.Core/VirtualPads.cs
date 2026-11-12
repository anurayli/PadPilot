using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace PadPilot.Core;

/// <summary>Sanal pad'e gidecek islenmis durum. Analoglar -1..1 (Y yukari pozitif), tetikler 0..255.</summary>
public struct VState
{
    public VBtn Buttons;
    public float LX, LY, RX, RY;
    public byte LT, RT;
}

public interface IVirtualTarget : IDisposable
{
    void Submit(in VState s);
}

/// <summary>ViGEmBus uzerinden sanal Xbox 360 / DualShock 4 kumandalari.</summary>
public sealed class VirtualPads : IDisposable
{
    readonly ViGEmClient? _client;
    public string? Error { get; }
    public bool Available => _client != null;

    public VirtualPads()
    {
        try { _client = new ViGEmClient(); }
        catch (Exception ex) { Error = ex.Message; }
    }

    /// <param name="rumble">Oyundan gelen titresim (agir, hafif).</param>
    public IVirtualTarget? Create(OutputKind kind, Action<byte, byte> rumble)
    {
        if (_client == null || kind == OutputKind.None) return null;
        return kind == OutputKind.DualShock4 ? new Ds4Target(_client, rumble) : new X360Target(_client, rumble);
    }

    public static short Axis(float v) => (short)Math.Clamp(MathF.Round(v * 32767f), -32768f, 32767f);
    public static byte AxisByte(float v, bool invert) => (byte)Math.Clamp(MathF.Round(128f + (invert ? -v : v) * 127.5f), 0f, 255f);

    public void Dispose() => _client?.Dispose();

    sealed class X360Target : IVirtualTarget
    {
        readonly IXbox360Controller _x;
        static readonly (VBtn, Xbox360Button)[] Map =
        [
            (VBtn.A, Xbox360Button.A), (VBtn.B, Xbox360Button.B), (VBtn.X, Xbox360Button.X), (VBtn.Y, Xbox360Button.Y),
            (VBtn.LB, Xbox360Button.LeftShoulder), (VBtn.RB, Xbox360Button.RightShoulder),
            (VBtn.Back, Xbox360Button.Back), (VBtn.Start, Xbox360Button.Start), (VBtn.Guide, Xbox360Button.Guide),
            (VBtn.LS, Xbox360Button.LeftThumb), (VBtn.RS, Xbox360Button.RightThumb),
            (VBtn.Up, Xbox360Button.Up), (VBtn.Down, Xbox360Button.Down), (VBtn.Left, Xbox360Button.Left), (VBtn.Right, Xbox360Button.Right),
        ];

        public X360Target(ViGEmClient c, Action<byte, byte> rumble)
        {
            _x = c.CreateXbox360Controller();
            _x.AutoSubmitReport = false;
            _x.FeedbackReceived += (_, e) => rumble(e.LargeMotor, e.SmallMotor);
            _x.Connect();
        }

        public void Submit(in VState s)
        {
            foreach (var (v, x) in Map) _x.SetButtonState(x, (s.Buttons & v) != 0);
            _x.SetAxisValue(Xbox360Axis.LeftThumbX, Axis(s.LX));
            _x.SetAxisValue(Xbox360Axis.LeftThumbY, Axis(s.LY));
            _x.SetAxisValue(Xbox360Axis.RightThumbX, Axis(s.RX));
            _x.SetAxisValue(Xbox360Axis.RightThumbY, Axis(s.RY));
            _x.SetSliderValue(Xbox360Slider.LeftTrigger, (s.Buttons & VBtn.LT) != 0 ? (byte)255 : s.LT);
            _x.SetSliderValue(Xbox360Slider.RightTrigger, (s.Buttons & VBtn.RT) != 0 ? (byte)255 : s.RT);
            _x.SubmitReport();
        }

        public void Dispose() { try { _x.Disconnect(); } catch { } }
    }

    sealed class Ds4Target : IVirtualTarget
    {
        readonly IDualShock4Controller _d;
        static readonly (VBtn, DualShock4Button)[] Map =
        [
            (VBtn.A, DualShock4Button.Cross), (VBtn.B, DualShock4Button.Circle), (VBtn.X, DualShock4Button.Square),
            (VBtn.Y, DualShock4Button.Triangle), (VBtn.LB, DualShock4Button.ShoulderLeft), (VBtn.RB, DualShock4Button.ShoulderRight),
            (VBtn.Back, DualShock4Button.Share), (VBtn.Start, DualShock4Button.Options),
            (VBtn.LS, DualShock4Button.ThumbLeft), (VBtn.RS, DualShock4Button.ThumbRight),
            (VBtn.Guide, DualShock4SpecialButton.Ps), (VBtn.Touchpad, DualShock4SpecialButton.Touchpad),
        ];

        public Ds4Target(ViGEmClient c, Action<byte, byte> rumble)
        {
            _d = c.CreateDualShock4Controller();
            _d.AutoSubmitReport = false;
            _d.FeedbackReceived += (_, e) => rumble(e.LargeMotor, e.SmallMotor);
            _d.Connect();
        }

        public void Submit(in VState s)
        {
            foreach (var (v, b) in Map) _d.SetButtonState(b, (s.Buttons & v) != 0);
            bool u = (s.Buttons & VBtn.Up) != 0, d = (s.Buttons & VBtn.Down) != 0, l = (s.Buttons & VBtn.Left) != 0, r = (s.Buttons & VBtn.Right) != 0;
            _d.SetDPadDirection(
                u && r ? DualShock4DPadDirection.Northeast : u && l ? DualShock4DPadDirection.Northwest :
                d && r ? DualShock4DPadDirection.Southeast : d && l ? DualShock4DPadDirection.Southwest :
                u ? DualShock4DPadDirection.North : d ? DualShock4DPadDirection.South :
                l ? DualShock4DPadDirection.West : r ? DualShock4DPadDirection.East : DualShock4DPadDirection.None);
            _d.SetAxisValue(DualShock4Axis.LeftThumbX, AxisByte(s.LX, false));
            _d.SetAxisValue(DualShock4Axis.LeftThumbY, AxisByte(s.LY, true));
            _d.SetAxisValue(DualShock4Axis.RightThumbX, AxisByte(s.RX, false));
            _d.SetAxisValue(DualShock4Axis.RightThumbY, AxisByte(s.RY, true));
            byte lt = (s.Buttons & VBtn.LT) != 0 ? (byte)255 : s.LT, rt = (s.Buttons & VBtn.RT) != 0 ? (byte)255 : s.RT;
            _d.SetSliderValue(DualShock4Slider.LeftTrigger, lt);
            _d.SetSliderValue(DualShock4Slider.RightTrigger, rt);
            _d.SetButtonState(DualShock4Button.TriggerLeft, lt > 0);
            _d.SetButtonState(DualShock4Button.TriggerRight, rt > 0);
            _d.SubmitReport();
        }

        public void Dispose() { try { _d.Disconnect(); } catch { } }
    }
}
