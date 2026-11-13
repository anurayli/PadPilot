using System.Globalization;

namespace PadPilot.Core;

/// <summary>Uygulama dili (Turkce / Ingilizce). Varsayilan: Windows dili.</summary>
public static class Lang
{
    /// <summary>true = Ingilizce.</summary>
    public static bool En { get; set; } = !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase);

    public static string T(string tr, string en) => En ? en : tr;

    /// <summary>"auto", "tr" veya "en" ayarini uygular.</summary>
    public static void Apply(string? setting) => En = setting switch
    {
        "tr" => false,
        "en" => true,
        _ => !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase),
    };
}
