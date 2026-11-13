namespace PadPilot.Core;

/// <summary>Sanal pad tuslari (Xbox 360 ve DualShock 4 ciktisi icin ortak).</summary>
[Flags]
public enum VBtn : uint
{
    None = 0, A = 1, B = 2, X = 4, Y = 8, LB = 16, RB = 32, Back = 64, Start = 128,
    LS = 256, RS = 512, Guide = 1024, Up = 2048, Down = 4096, Left = 8192, Right = 16384,
    LT = 1 << 15, RT = 1 << 16, Touchpad = 1 << 17,
}

public sealed record TargetInfo(string Code, string Label, string Group);

/// <summary>Tus esleme hedefleri. Kod bicimi: "" varsayilan, "none", "x:A", "k:65", "m:L", "a:NextProfile".</summary>
public static class Targets
{
    public static readonly PadButtons[] MappableButtons =
    [
        PadButtons.Cross, PadButtons.Circle, PadButtons.Square, PadButtons.Triangle,
        PadButtons.L1, PadButtons.R1, PadButtons.L2, PadButtons.R2, PadButtons.L3, PadButtons.R3,
        PadButtons.Up, PadButtons.Down, PadButtons.Left, PadButtons.Right,
        PadButtons.Share, PadButtons.Options, PadButtons.PS, PadButtons.Touchpad, PadButtons.Mute,
    ];

    static string T(string tr, string en) => Lang.T(tr, en);

    public static string ButtonLabel(PadButtons b) => b switch
    {
        PadButtons.Cross => T("✕  Çarpı", "✕  Cross"), PadButtons.Circle => T("○  Daire", "○  Circle"),
        PadButtons.Square => T("□  Kare", "□  Square"), PadButtons.Triangle => T("△  Üçgen", "△  Triangle"),
        PadButtons.L1 => "L1", PadButtons.R1 => "R1",
        PadButtons.L2 => T("L2 (tetik)", "L2 (trigger)"), PadButtons.R2 => T("R2 (tetik)", "R2 (trigger)"),
        PadButtons.L3 => T("L3 (sol analog bas)", "L3 (left stick click)"), PadButtons.R3 => T("R3 (sağ analog bas)", "R3 (right stick click)"),
        PadButtons.Up => T("Yön ↑", "D-pad ↑"), PadButtons.Down => T("Yön ↓", "D-pad ↓"),
        PadButtons.Left => T("Yön ←", "D-pad ←"), PadButtons.Right => T("Yön →", "D-pad →"),
        PadButtons.Share => "Share / Create", PadButtons.Options => "Options", PadButtons.PS => T("PS tuşu", "PS button"),
        PadButtons.Touchpad => T("Touchpad tıklama", "Touchpad click"), PadButtons.Mute => T("Mikrofon tuşu (PS5)", "Mic button (PS5)"),
        _ => b.ToString(),
    };

    /// <summary>"" (varsayilan) kodunun karsiligi.</summary>
    public static VBtn DefaultButton(PadButtons b) => b switch
    {
        PadButtons.Cross => VBtn.A, PadButtons.Circle => VBtn.B, PadButtons.Square => VBtn.X, PadButtons.Triangle => VBtn.Y,
        PadButtons.L1 => VBtn.LB, PadButtons.R1 => VBtn.RB, PadButtons.L3 => VBtn.LS, PadButtons.R3 => VBtn.RS,
        PadButtons.Up => VBtn.Up, PadButtons.Down => VBtn.Down, PadButtons.Left => VBtn.Left, PadButtons.Right => VBtn.Right,
        PadButtons.Share => VBtn.Back, PadButtons.Options => VBtn.Start, PadButtons.PS => VBtn.Guide,
        PadButtons.Touchpad => VBtn.Touchpad, _ => VBtn.None,
    };

    static List<TargetInfo>? _tr, _en;
    /// <summary>Gecerli dildeki tum hedefler.</summary>
    public static IReadOnlyList<TargetInfo> All => Lang.En ? (_en ??= Build()) : (_tr ??= Build());
    public static IReadOnlyList<TargetInfo> HoldTargets => [new("", T("— Yok —", "— None —"), ""), .. All.Skip(1)];

    static List<TargetInfo> Build()
    {
        var l = new List<TargetInfo>
        {
            new("", T("Varsayılan", "Default"), ""),
            new("none", T("Kapalı (hiçbir şey)", "Disabled (nothing)"), ""),
        };
        void X(string c, string n) => l.Add(new("x:" + c, n, "Pad"));
        X("A", "A"); X("B", "B"); X("X", "X"); X("Y", "Y"); X("LB", "LB"); X("RB", "RB");
        X("LT", T("LT (tam bas)", "LT (full press)")); X("RT", T("RT (tam bas)", "RT (full press)"));
        X("LS", T("Sol analog bas", "Left stick click")); X("RS", T("Sağ analog bas", "Right stick click"));
        X("Back", "Back / Share"); X("Start", "Start / Options"); X("Guide", "Guide / PS");
        X("Up", T("Yön ↑", "D-pad ↑")); X("Down", T("Yön ↓", "D-pad ↓")); X("Left", T("Yön ←", "D-pad ←")); X("Right", T("Yön →", "D-pad →"));
        X("Touchpad", T("Touchpad (DS4 çıkışında)", "Touchpad (DS4 output)"));

        void M(string c, string n) => l.Add(new("m:" + c, n, "Mouse"));
        M("L", T("Fare sol tık", "Mouse left click")); M("R", T("Fare sağ tık", "Mouse right click"));
        M("M", T("Fare orta tık", "Mouse middle click")); M("X1", T("Fare geri", "Mouse back")); M("X2", T("Fare ileri", "Mouse forward"));
        M("WU", T("Tekerlek yukarı", "Wheel up")); M("WD", T("Tekerlek aşağı", "Wheel down"));

        void A(string c, string n) => l.Add(new("a:" + c, n, "Action"));
        A("NextProfile", T("Sonraki profil", "Next profile")); A("PrevProfile", T("Önceki profil", "Previous profile"));
        A("GyroToggle", T("Gyro aç/kapat", "Toggle gyro")); A("Snip", T("Ekran alıntısı (Win+Shift+S)", "Screen snip (Win+Shift+S)"));
        A("Osk", T("Ekran klavyesi", "On-screen keyboard")); A("Desktop", T("Masaüstünü göster", "Show desktop"));
        A("Disconnect", T("Pad'i kapat (Bluetooth)", "Turn off controller (Bluetooth)"));

        foreach (var (vk, n) in BuildKeys()) l.Add(new("k:" + vk, n, "Keyboard"));
        return l;
    }

    static (int, string)[] BuildKeys()
    {
        var k = new List<(int, string)>
        {
            (0x20, T("Boşluk", "Space")), (0x0D, "Enter"), (0x1B, "Esc"), (0x09, "Tab"), (0x08, T("Geri sil", "Backspace")),
            (0xA0, "Shift"), (0xA2, "Ctrl"), (0xA4, "Alt"), (0x5B, "Windows"),
            (0x26, T("Ok ↑", "Arrow ↑")), (0x28, T("Ok ↓", "Arrow ↓")), (0x25, T("Ok ←", "Arrow ←")), (0x27, T("Ok →", "Arrow →")),
            (0x24, "Home"), (0x23, "End"), (0x21, "Page Up"), (0x22, "Page Down"), (0x2D, "Insert"), (0x2E, "Delete"),
            (0x2C, "Print Screen"), (0xAF, T("Ses +", "Volume +")), (0xAE, T("Ses −", "Volume −")), (0xAD, T("Sesi kapat", "Mute")),
            (0xB3, T("Oynat / duraklat", "Play / pause")), (0xB0, T("Sonraki parça", "Next track")), (0xB1, T("Önceki parça", "Previous track")),
        };
        for (int c = 'A'; c <= 'Z'; c++) k.Add((c, T("Tuş ", "Key ") + (char)c));
        for (int c = '0'; c <= '9'; c++) k.Add((c, T("Tuş ", "Key ") + (char)c));
        for (int i = 1; i <= 12; i++) k.Add((0x6F + i, "F" + i));
        return [.. k];
    }

    public static string Label(string code) => All.FirstOrDefault(t => t.Code == code)?.Label ?? code;

    public static VBtn ParsePad(string code) =>
        code.StartsWith("x:") && Enum.TryParse<VBtn>(code[2..], out var v) ? v : VBtn.None;
}
