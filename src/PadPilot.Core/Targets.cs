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

    public static string ButtonLabel(PadButtons b) => b switch
    {
        PadButtons.Cross => "✕  Çarpı", PadButtons.Circle => "○  Daire", PadButtons.Square => "□  Kare",
        PadButtons.Triangle => "△  Üçgen", PadButtons.L1 => "L1", PadButtons.R1 => "R1",
        PadButtons.L2 => "L2 (tetik)", PadButtons.R2 => "R2 (tetik)", PadButtons.L3 => "L3 (sol analog bas)",
        PadButtons.R3 => "R3 (sağ analog bas)", PadButtons.Up => "Yön ↑", PadButtons.Down => "Yön ↓",
        PadButtons.Left => "Yön ←", PadButtons.Right => "Yön →", PadButtons.Share => "Share / Create",
        PadButtons.Options => "Options", PadButtons.PS => "PS tuşu", PadButtons.Touchpad => "Touchpad tıklama",
        PadButtons.Mute => "Mikrofon tuşu (PS5)", _ => b.ToString(),
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

    public static readonly IReadOnlyList<TargetInfo> All = Build();
    public static readonly IReadOnlyList<TargetInfo> HoldTargets = [new("", "— Yok —", ""), .. All.Skip(1)];

    static List<TargetInfo> Build()
    {
        var l = new List<TargetInfo>
        {
            new("", "Varsayılan", ""),
            new("none", "Kapalı (hiçbir şey)", ""),
        };
        void X(string c, string n) => l.Add(new("x:" + c, n, "Pad"));
        X("A", "A"); X("B", "B"); X("X", "X"); X("Y", "Y"); X("LB", "LB"); X("RB", "RB");
        X("LT", "LT (tam bas)"); X("RT", "RT (tam bas)"); X("LS", "Sol analog bas"); X("RS", "Sağ analog bas");
        X("Back", "Back / Share"); X("Start", "Start / Options"); X("Guide", "Guide / PS");
        X("Up", "Yön ↑"); X("Down", "Yön ↓"); X("Left", "Yön ←"); X("Right", "Yön →"); X("Touchpad", "Touchpad (DS4 çıkışında)");

        void M(string c, string n) => l.Add(new("m:" + c, n, "Fare"));
        M("L", "Fare sol tık"); M("R", "Fare sağ tık"); M("M", "Fare orta tık"); M("X1", "Fare geri"); M("X2", "Fare ileri");
        M("WU", "Tekerlek yukarı"); M("WD", "Tekerlek aşağı");

        void A(string c, string n) => l.Add(new("a:" + c, n, "Eylem"));
        A("NextProfile", "Sonraki profil"); A("PrevProfile", "Önceki profil"); A("GyroToggle", "Gyro aç/kapat");
        A("Snip", "Ekran alıntısı (Win+Shift+S)"); A("Osk", "Ekran klavyesi"); A("Desktop", "Masaüstünü göster");
        A("Disconnect", "Pad'i kapat (Bluetooth)");

        foreach (var (vk, n) in BuildKeys()) l.Add(new("k:" + vk, n, "Klavye"));
        return l;
    }

    public static readonly (int Vk, string Name)[] Keys = BuildKeys();

    static (int, string)[] BuildKeys()
    {
        var k = new List<(int, string)>
        {
            (0x20, "Boşluk"), (0x0D, "Enter"), (0x1B, "Esc"), (0x09, "Tab"), (0x08, "Geri sil"),
            (0xA0, "Shift"), (0xA2, "Ctrl"), (0xA4, "Alt"), (0x5B, "Windows"),
            (0x26, "Ok ↑"), (0x28, "Ok ↓"), (0x25, "Ok ←"), (0x27, "Ok →"),
            (0x24, "Home"), (0x23, "End"), (0x21, "Page Up"), (0x22, "Page Down"), (0x2D, "Insert"), (0x2E, "Delete"),
            (0x2C, "Print Screen"), (0xAF, "Ses +"), (0xAE, "Ses −"), (0xAD, "Sesi kapat"),
            (0xB3, "Oynat / duraklat"), (0xB0, "Sonraki parça"), (0xB1, "Önceki parça"),
        };
        for (int c = 'A'; c <= 'Z'; c++) k.Add((c, "Tuş " + (char)c));
        for (int c = '0'; c <= '9'; c++) k.Add((c, "Tuş " + (char)c));
        for (int i = 1; i <= 12; i++) k.Add((0x6F + i, "F" + i));
        return [.. k];
    }

    public static string Label(string code) => All.FirstOrDefault(t => t.Code == code)?.Label ?? code;

    public static VBtn ParsePad(string code) =>
        code.StartsWith("x:") && Enum.TryParse<VBtn>(code[2..], out var v) ? v : VBtn.None;
}
