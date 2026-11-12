using System.Collections.Concurrent;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace PadPilot.Core;

/// <summary>Pad durumunu ViGEmBus uzerinden sanal Xbox 360 kumandasina aktarir; oyunun titresimini pad'e geri yazar.</summary>
public sealed class XboxBridge : IDisposable
{
    readonly ViGEmClient _client = new();   // ViGEmBus kurulu degilse burada hata firlatir
    readonly ConcurrentDictionary<PadDevice, IXbox360Controller> _targets = new();

    public void Attach(PadDevice pad)
    {
        if (_targets.ContainsKey(pad)) return;
        var x = _client.CreateXbox360Controller();
        x.AutoSubmitReport = false;
        x.FeedbackReceived += (_, e) => pad.SetRumble(e.LargeMotor, e.SmallMotor);
        x.Connect();
        _targets[pad] = x;
        pad.StateChanged += OnState;
    }

    public void Detach(PadDevice pad)
    {
        pad.StateChanged -= OnState;
        if (_targets.TryRemove(pad, out var x))
        {
            try { x.Disconnect(); } catch { }
        }
    }

    void OnState(PadDevice pad, PadState s)
    {
        if (!_targets.TryGetValue(pad, out var x)) return;
        var b = s.Buttons;
        x.SetButtonState(Xbox360Button.A, b.HasFlag(PadButtons.Cross));
        x.SetButtonState(Xbox360Button.B, b.HasFlag(PadButtons.Circle));
        x.SetButtonState(Xbox360Button.X, b.HasFlag(PadButtons.Square));
        x.SetButtonState(Xbox360Button.Y, b.HasFlag(PadButtons.Triangle));
        x.SetButtonState(Xbox360Button.LeftShoulder, b.HasFlag(PadButtons.L1));
        x.SetButtonState(Xbox360Button.RightShoulder, b.HasFlag(PadButtons.R1));
        x.SetButtonState(Xbox360Button.Back, b.HasFlag(PadButtons.Share));
        x.SetButtonState(Xbox360Button.Start, b.HasFlag(PadButtons.Options));
        x.SetButtonState(Xbox360Button.LeftThumb, b.HasFlag(PadButtons.L3));
        x.SetButtonState(Xbox360Button.RightThumb, b.HasFlag(PadButtons.R3));
        x.SetButtonState(Xbox360Button.Guide, b.HasFlag(PadButtons.PS));
        x.SetButtonState(Xbox360Button.Up, b.HasFlag(PadButtons.Up));
        x.SetButtonState(Xbox360Button.Down, b.HasFlag(PadButtons.Down));
        x.SetButtonState(Xbox360Button.Left, b.HasFlag(PadButtons.Left));
        x.SetButtonState(Xbox360Button.Right, b.HasFlag(PadButtons.Right));
        x.SetAxisValue(Xbox360Axis.LeftThumbX, Axis(s.LX));
        x.SetAxisValue(Xbox360Axis.LeftThumbY, AxisInverted(s.LY));
        x.SetAxisValue(Xbox360Axis.RightThumbX, Axis(s.RX));
        x.SetAxisValue(Xbox360Axis.RightThumbY, AxisInverted(s.RY));
        x.SetSliderValue(Xbox360Slider.LeftTrigger, s.L2);
        x.SetSliderValue(Xbox360Slider.RightTrigger, s.R2);
        x.SubmitReport();
    }

    public static short Axis(byte v) => (short)(v * 257 - 32768);
    public static short AxisInverted(byte v) => (short)(32767 - v * 257);   // PlayStation'da Y asagi dogru artar

    public void Dispose()
    {
        foreach (var pad in _targets.Keys) Detach(pad);
        _client.Dispose();
    }
}
