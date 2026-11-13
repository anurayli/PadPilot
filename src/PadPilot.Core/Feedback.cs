using System.Net.Http.Json;
using System.Text.Json;

namespace PadPilot.Core;

/// <summary>Geri bildirimi FormSubmit uzerinden gelistiricinin e-postasina gonderir (sunucu veya sifre gerekmez).</summary>
public static class Feedback
{
    const string Endpoint = "https://formsubmit.co/ajax/alinuraylii@gmail.com";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <returns>(basarili, hata metni)</returns>
    public static async Task<(bool Ok, string? Error)> SendAsync(string message, string? replyEmail, string platform, string version)
    {
        var body = new Dictionary<string, string>
        {
            ["_subject"] = $"PadPilot geri bildirim ({platform} {version})",
            ["_template"] = "table",
            ["_captcha"] = "false",
            ["Mesaj"] = message,
            ["Platform"] = platform,
            ["Surum"] = version,
            ["Dil"] = Lang.En ? "en" : "tr",
            ["Sistem"] = Environment.OSVersion.VersionString,
        };
        if (!string.IsNullOrWhiteSpace(replyEmail)) { body["email"] = replyEmail.Trim(); body["_replyto"] = replyEmail.Trim(); }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
            req.Headers.Add("Accept", "application/json");
            req.Headers.Referrer = new Uri("https://github.com/anurayli/PadPilot");
            using var res = await Http.SendAsync(req);
            string text = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            bool ok = root.TryGetProperty("success", out var s) && (s.ValueKind == JsonValueKind.True || (s.ValueKind == JsonValueKind.String && s.GetString() == "true"));
            return ok ? (true, null) : (false, root.TryGetProperty("message", out var m) ? m.GetString() : text);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
