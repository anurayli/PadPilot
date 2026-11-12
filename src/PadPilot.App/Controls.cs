using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PadPilot.Core;

namespace PadPilot.App;

/// <summary>Solda baslik/aciklama, sagda kontrol olan ayar satiri.</summary>
public class SettingRow : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRow));
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}

/// <summary>Baslik + kaydirici + deger metni.</summary>
public class SliderRow : Control
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SliderRow));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(SliderRow));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderRow), new(0.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderRow), new(1.0));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(SliderRow), new(0.01));
    public static readonly DependencyProperty SnapProperty = DependencyProperty.Register(nameof(Snap), typeof(bool), typeof(SliderRow), new(false));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(SliderRow), new("0.00", (d, _) => ((SliderRow)d).UpdateText()));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderRow),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SliderRow)d).UpdateText()));
    static readonly DependencyPropertyKey DisplayKey = DependencyProperty.RegisterReadOnly(nameof(DisplayValue), typeof(string), typeof(SliderRow), new(""));
    public static readonly DependencyProperty DisplayValueProperty = DisplayKey.DependencyProperty;

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public bool Snap { get => (bool)GetValue(SnapProperty); set => SetValue(SnapProperty, value); }
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string DisplayValue => (string)GetValue(DisplayValueProperty);

    void UpdateText()
    {
        string f = Format;
        string text = f == "%" ? (Value * 100).ToString("0", CultureInfo.CurrentCulture) + "%"
            : f == "x" ? Value.ToString("0.00", CultureInfo.CurrentCulture) + "\u00D7"
            : Value.ToString(f, CultureInfo.CurrentCulture);
        SetValue(DisplayKey, text);
    }
}

/// <summary>Pad'in canli cizimi: basilan tuslar, analoglar, tetikler, dokunmatik.</summary>
public class PadVisual : FrameworkElement
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(PadState), typeof(PadVisual),
        new FrameworkPropertyMetadata(default(PadState), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsDualSenseProperty = DependencyProperty.Register(nameof(IsDualSense), typeof(bool), typeof(PadVisual),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LightProperty = DependencyProperty.Register(nameof(Light), typeof(Color), typeof(PadVisual),
        new FrameworkPropertyMetadata(Color.FromRgb(0x2F, 0x6B, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

    public PadState State { get => (PadState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool IsDualSense { get => (bool)GetValue(IsDualSenseProperty); set => SetValue(IsDualSenseProperty, value); }
    public Color Light { get => (Color)GetValue(LightProperty); set => SetValue(LightProperty, value); }

    static readonly Brush Body = Frz(new SolidColorBrush(Color.FromRgb(0x24, 0x28, 0x31)));
    static readonly Brush Dark = Frz(new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x1F)));
    static readonly Brush Idle = Frz(new SolidColorBrush(Color.FromRgb(0x32, 0x37, 0x42)));
    static readonly Brush On = Frz(new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF)));
    static readonly Brush Label = Frz(new SolidColorBrush(Color.FromRgb(0xC9, 0xCE, 0xD8)));
    static readonly Pen Edge = Frz(new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x40, 0x4C)), 1.5));
    static T Frz<T>(T f) where T : Freezable { f.Freeze(); return f; }

    const double W = 460, H = 300;

    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? W : a.Width, double.IsInfinity(a.Height) ? H : Math.Min(a.Height, a.Width * H / W));

    protected override void OnRender(DrawingContext dc)
    {
        double k = Math.Min(ActualWidth / W, ActualHeight / H);
        dc.PushTransform(new TranslateTransform((ActualWidth - W * k) / 2, (ActualHeight - H * k) / 2));
        dc.PushTransform(new ScaleTransform(k, k));
        var s = State;
        bool B(PadButtons b) => (s.Buttons & b) != 0;

        // Tetikler (doluluk = basma miktari)
        Trigger(dc, new Rect(78, 4, 84, 20), s.L2 / 255.0, "L2");
        Trigger(dc, new Rect(298, 4, 84, 20), s.R2 / 255.0, "R2");
        dc.DrawRoundedRectangle(B(PadButtons.L1) ? On : Idle, null, new Rect(76, 30, 88, 14), 7, 7);
        dc.DrawRoundedRectangle(B(PadButtons.R1) ? On : Idle, null, new Rect(296, 30, 88, 14), 7, 7);

        // Govde
        var body = new GeometryGroup { FillRule = FillRule.Nonzero };
        body.Children.Add(new RectangleGeometry(new Rect(50, 44, 360, 150), 70, 70));
        body.Children.Add(new EllipseGeometry(new Point(108, 205), 62, 84));
        body.Children.Add(new EllipseGeometry(new Point(352, 205), 62, 84));
        dc.DrawGeometry(Body, null, body);
        dc.DrawGeometry(null, Edge, Geometry.Combine(Geometry.Combine(new RectangleGeometry(new Rect(50, 44, 360, 150), 70, 70),
            new EllipseGeometry(new Point(108, 205), 62, 84), GeometryCombineMode.Union, null), new EllipseGeometry(new Point(352, 205), 62, 84), GeometryCombineMode.Union, null));

        // Touchpad + isik
        var tp = new Rect(160, 50, 140, 78);
        dc.DrawRoundedRectangle(B(PadButtons.Touchpad) ? On : Dark, Edge, tp, 10, 10);
        var light = new SolidColorBrush(Light); light.Freeze();
        if (IsDualSense) { dc.DrawRoundedRectangle(light, null, new Rect(150, 56, 5, 64), 2, 2); dc.DrawRoundedRectangle(light, null, new Rect(305, 56, 5, 64), 2, 2); }
        else dc.DrawRoundedRectangle(light, null, new Rect(185, 44, 90, 4), 2, 2);
        double tpH = IsDualSense ? 1080 : 942;
        if (s.Touch0) dc.DrawEllipse(Brushes.White, null, new Point(tp.X + s.Touch0X / 1920.0 * tp.Width, tp.Y + s.Touch0Y / tpH * tp.Height), 5, 5);
        if (s.Touch1) dc.DrawEllipse(Brushes.White, null, new Point(tp.X + s.Touch1X / 1920.0 * tp.Width, tp.Y + s.Touch1Y / tpH * tp.Height), 5, 5);

        // Share / Options / PS / Mute
        dc.DrawRoundedRectangle(B(PadButtons.Share) ? On : Idle, null, new Rect(136, 66, 10, 22), 5, 5);
        dc.DrawRoundedRectangle(B(PadButtons.Options) ? On : Idle, null, new Rect(314, 66, 10, 22), 5, 5);
        dc.DrawEllipse(B(PadButtons.PS) ? On : Idle, null, new Point(230, 160), 11, 11);
        if (IsDualSense) dc.DrawRoundedRectangle(B(PadButtons.Mute) ? Brushes.OrangeRed : Idle, null, new Rect(222, 178, 16, 6), 3, 3);

        // Yon tuslari
        var c = new Point(110, 108);
        DPad(dc, new Rect(c.X - 8, c.Y - 30, 16, 22), B(PadButtons.Up));
        DPad(dc, new Rect(c.X - 8, c.Y + 8, 16, 22), B(PadButtons.Down));
        DPad(dc, new Rect(c.X - 30, c.Y - 8, 22, 16), B(PadButtons.Left));
        DPad(dc, new Rect(c.X + 8, c.Y - 8, 22, 16), B(PadButtons.Right));

        // Yuz tuslari (PlayStation renkleri)
        var f = new Point(350, 108);
        Face(dc, new Point(f.X, f.Y - 24), "\u25B3", Color.FromRgb(0x3E, 0xC7, 0xA5), B(PadButtons.Triangle));
        Face(dc, new Point(f.X, f.Y + 24), "\u2715", Color.FromRgb(0x7A, 0xA5, 0xFF), B(PadButtons.Cross));
        Face(dc, new Point(f.X - 24, f.Y), "\u25A1", Color.FromRgb(0xF5, 0x9A, 0xD7), B(PadButtons.Square));
        Face(dc, new Point(f.X + 24, f.Y), "\u25CB", Color.FromRgb(0xFF, 0x66, 0x70), B(PadButtons.Circle));

        // Analoglar
        Stick(dc, new Point(170, 190), StickMath.Norm(s.LX), StickMath.Norm(s.LY), B(PadButtons.L3));
        Stick(dc, new Point(290, 190), StickMath.Norm(s.RX), StickMath.Norm(s.RY), B(PadButtons.R3));
        dc.Pop(); dc.Pop();
    }

    static void Trigger(DrawingContext dc, Rect r, double v, string text)
    {
        dc.DrawRoundedRectangle(Idle, null, r, 6, 6);
        if (v > 0.01) dc.DrawRoundedRectangle(On, null, new Rect(r.X, r.Y, Math.Max(12, r.Width * v), r.Height), 6, 6);
        Text(dc, text, new Point(r.X + r.Width / 2, r.Y + r.Height / 2), 11, Label);
    }

    static void DPad(DrawingContext dc, Rect r, bool on) => dc.DrawRoundedRectangle(on ? On : Idle, null, r, 4, 4);

    static void Face(DrawingContext dc, Point p, string glyph, Color color, bool on)
    {
        var b = new SolidColorBrush(color); b.Freeze();
        dc.DrawEllipse(on ? b : Dark, null, p, 13, 13);
        Text(dc, glyph, p, 14, on ? Brushes.Black : b);
    }

    static void Stick(DrawingContext dc, Point c, double x, double y, bool pressed)
    {
        dc.DrawEllipse(Dark, Edge, c, 30, 30);
        var cap = new Point(c.X + x * 12, c.Y + y * 12);
        dc.DrawEllipse(pressed ? On : Idle, null, cap, 21, 21);
        dc.DrawEllipse(null, new Pen(pressed ? Brushes.White : Edge.Brush, 1), cap, 15, 15);
    }

    static void Text(DrawingContext dc, string t, Point center, double size, Brush brush)
    {
        var ft = new FormattedText(t, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Symbol"), size, brush, 1.25);
        dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }
}

/// <summary>Analog onizleme: olu bolge halkasi, ham konum (gri) ve islenmis konum (mavi).</summary>
public class StickPreview : FrameworkElement
{
    static FrameworkPropertyMetadata Meta() => new(0.0, FrameworkPropertyMetadataOptions.AffectsRender);
    public static readonly DependencyProperty RawXProperty = DependencyProperty.Register(nameof(RawX), typeof(double), typeof(StickPreview), Meta());
    public static readonly DependencyProperty RawYProperty = DependencyProperty.Register(nameof(RawY), typeof(double), typeof(StickPreview), Meta());
    public static readonly DependencyProperty OutXProperty = DependencyProperty.Register(nameof(OutX), typeof(double), typeof(StickPreview), Meta());
    public static readonly DependencyProperty OutYProperty = DependencyProperty.Register(nameof(OutY), typeof(double), typeof(StickPreview), Meta());
    public static readonly DependencyProperty DeadzoneProperty = DependencyProperty.Register(nameof(Deadzone), typeof(double), typeof(StickPreview), Meta());
    public static readonly DependencyProperty OuterProperty = DependencyProperty.Register(nameof(Outer), typeof(double), typeof(StickPreview), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double RawX { get => (double)GetValue(RawXProperty); set => SetValue(RawXProperty, value); }
    public double RawY { get => (double)GetValue(RawYProperty); set => SetValue(RawYProperty, value); }
    public double OutX { get => (double)GetValue(OutXProperty); set => SetValue(OutXProperty, value); }
    public double OutY { get => (double)GetValue(OutYProperty); set => SetValue(OutYProperty, value); }
    public double Deadzone { get => (double)GetValue(DeadzoneProperty); set => SetValue(DeadzoneProperty, value); }
    public double Outer { get => (double)GetValue(OuterProperty); set => SetValue(OuterProperty, value); }

    static readonly Pen Ring = new(new SolidColorBrush(Color.FromRgb(0x3A, 0x40, 0x4C)), 1);
    static readonly Pen Dash = new(new SolidColorBrush(Color.FromRgb(0x5A, 0x62, 0x70)), 1) { DashStyle = DashStyles.Dash };

    protected override Size MeasureOverride(Size a) => new(150, 150);

    protected override void OnRender(DrawingContext dc)
    {
        double r = Math.Min(ActualWidth, ActualHeight) / 2 - 4;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1B)), Ring, c, r, r);
        dc.DrawLine(Ring, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
        dc.DrawLine(Ring, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x5D, 0x6C)), null, c, r * Deadzone, r * Deadzone);
        dc.DrawEllipse(null, Dash, c, r * Outer, r * Outer);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x8C, 0x94, 0xA3)), null, new Point(c.X + RawX * r, c.Y - RawY * r), 4, 4);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF)), new Pen(Brushes.White, 1.5), new Point(c.X + OutX * r, c.Y - OutY * r), 6, 6);
    }
}

/// <summary>Tetik onizlemesi: 10 bolge, etkin efekt araligi ve anlik basma konumu.</summary>
public class TriggerPreview : FrameworkElement
{
    static FrameworkPropertyMetadata Meta(object d) => new(d, FrameworkPropertyMetadataOptions.AffectsRender);
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(TriggerPreview), Meta(0.0));
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(nameof(Start), typeof(int), typeof(TriggerPreview), Meta(0));
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(int), typeof(TriggerPreview), Meta(9));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(TriggerEffectKind), typeof(TriggerPreview), Meta(TriggerEffectKind.Off));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Start { get => (int)GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public int End { get => (int)GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public TriggerEffectKind Kind { get => (TriggerEffectKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 300 : a.Width, 34);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth / 10, h = 22;
        bool ranged = Kind is TriggerEffectKind.Weapon or TriggerEffectKind.Bow or TriggerEffectKind.Galloping or TriggerEffectKind.Machine or TriggerEffectKind.Slope;
        for (int i = 0; i < 10; i++)
        {
            bool active = Kind != TriggerEffectKind.Off && i >= Start && (!ranged || i <= End);
            var b = new SolidColorBrush(active ? Color.FromRgb(0x1F, 0x3A, 0x6E) : Color.FromRgb(0x24, 0x28, 0x31));
            dc.DrawRoundedRectangle(b, null, new Rect(i * w + 1, 0, w - 2, h), 4, 4);
        }
        double x = Math.Clamp(Value, 0, 1) * ActualWidth;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF)), null, new Rect(0, h + 6, x, 4));
        dc.DrawRectangle(Brushes.White, null, new Rect(Math.Max(0, x - 1), 0, 2, h));
    }
}
