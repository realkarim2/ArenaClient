using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ArenaClient;

public sealed record SteamIdentity(string SteamId64, string PersonaName, string? AccountName);

public static class SteamIdentityReader
{
    public static SteamIdentity? TryGetCurrentIdentity()
    {
        var steamPath = FindSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamPath))
            return null;

        var loginUsers = Path.Combine(steamPath, "config", "loginusers.vdf");
        if (!File.Exists(loginUsers))
            return null;

        var text = File.ReadAllText(loginUsers);
        var blocks = Regex.Matches(
            text,
            "\\\"(?<id>\\d{17})\\\"\\s*\\{(?<body>.*?)\\n\\s*\\}",
            RegexOptions.Singleline);

        SteamIdentity? fallback = null;
        foreach (Match block in blocks)
        {
            var id = block.Groups["id"].Value;
            var body = block.Groups["body"].Value;
            var persona = ReadVdfValue(body, "PersonaName");
            var account = ReadVdfValue(body, "AccountName");
            var mostRecent = ReadVdfValue(body, "MostRecent");

            if (string.IsNullOrWhiteSpace(persona))
                persona = account;
            if (string.IsNullOrWhiteSpace(persona))
                continue;

            var identity = new SteamIdentity(id, Unescape(persona), account is null ? null : Unescape(account));
            fallback ??= identity;

            if (mostRecent == "1")
                return identity;
        }

        return fallback;
    }

    private static string? ReadVdfValue(string body, string key)
    {
        var match = Regex.Match(
            body,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"",
            RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string Unescape(string value) => value
        .Replace("\\\\", "\\")
        .Replace("\\\"", "\"");

    private static string? FindSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\\Valve\\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
