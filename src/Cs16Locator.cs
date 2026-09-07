using Microsoft.Win32;

namespace ArenaClient;

public sealed record Cs16Install(string RootPath, string ExecutablePath);

public static class Cs16Locator
{
    public static Cs16Install? Find()
    {
        foreach (var path in CandidatePaths())
        {
            var install = Validate(path);
            if (install is not null)
                return install;
        }

        return null;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var steamPath = ReadSteamPath();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            foreach (var library in ReadSteamLibraries(steamPath))
            {
                var candidate = Path.Combine(library, "steamapps", "common", "Half-Life");
                if (seen.Add(candidate))
                    yield return candidate;
            }
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(local))
        {
            var candidate = Path.Combine(local, "Steam", "steamapps", "common", "Half-Life");
            if (seen.Add(candidate))
                yield return candidate;
        }

        var local64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(local64))
        {
            var candidate = Path.Combine(local64, "Steam", "steamapps", "common", "Half-Life");
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    private static Cs16Install? Validate(string root)
    {
        var exe = Path.Combine(root, "hl.exe");
        var cstrike = Path.Combine(root, "cstrike");
        return File.Exists(exe) && Directory.Exists(cstrike)
            ? new Cs16Install(root, exe)
            : null;
    }

    private static string? ReadSteamPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }

    private static IEnumerable<string> ReadSteamLibraries(string steamPath)
    {
        yield return steamPath;

        var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFile))
            yield break;

        var text = File.ReadAllText(libraryFile);
        foreach (var match in System.Text.RegularExpressions.Regex.Matches(
                     text,
                     "\\\"path\\\"\\s*\\\"(?<path>(?:\\\\.|[^\\\"])*)\\\"",
                     System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            var path = match.Groups["path"].Value
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"");

            if (!string.IsNullOrWhiteSpace(path))
                yield return path;
        }
    }
}
