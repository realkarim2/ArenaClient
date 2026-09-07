using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ArenaClient;

public sealed class ArenaPackInstaller
{
    public const string DefaultManifestUrl = "https://csarena.pages.dev/client/manifest.json";
    private const long MaxPackageBytes = 512L * 1024 * 1024;

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public async Task<InstallResult> InstallAsync(string gameDirectory, string manifestUrl, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(gameDirectory)) return InstallResult.Fail("Game directory does not exist.");
        var cstrike = Path.Combine(gameDirectory, "cstrike");
        if (!Directory.Exists(cstrike)) return InstallResult.Fail("cstrike folder was not found.");

        Manifest? manifest;
        try { manifest = await DownloadManifestAsync(manifestUrl, cancellationToken); }
        catch (Exception ex) { return InstallResult.Fail($"Manifest download failed: {ex.Message}"); }
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.PackageUrl) || string.IsNullOrWhiteSpace(manifest.Version))
            return InstallResult.Fail("Arena package manifest is invalid.");
        if (!Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out var packageUri) || packageUri.Scheme != Uri.UriSchemeHttps)
            return InstallResult.Fail("Arena package URL must use HTTPS.");

        var installedVersionPath = Path.Combine(gameDirectory, ".csarena-version");
        var currentVersion = File.Exists(installedVersionPath) ? File.ReadAllText(installedVersionPath).Trim() : null;
        if (string.Equals(currentVersion, manifest.Version, StringComparison.OrdinalIgnoreCase))
            return InstallResult.AlreadyCurrent(manifest.Version);

        var tempRoot = Path.Combine(Path.GetTempPath(), "CSArena", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, "arena-pack.zip");
        var backupRoot = Path.Combine(gameDirectory, ".csarena_backup", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        var copied = new List<(string Destination, string? Backup)>();

        try
        {
            await DownloadFileAsync(packageUri, zipPath, progress, cancellationToken);
            var length = new FileInfo(zipPath).Length;
            if (length <= 0 || length > MaxPackageBytes) return InstallResult.Fail("Arena package size is invalid.");

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var actualHash = await ComputeSha256Async(zipPath, cancellationToken);
                if (!actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    return InstallResult.Fail("Arena package integrity check failed.");
            }

            var extractRoot = Path.Combine(tempRoot, "package");
            Directory.CreateDirectory(extractRoot);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    var full = Path.GetFullPath(Path.Combine(extractRoot, entry.FullName));
                    var root = Path.GetFullPath(extractRoot) + Path.DirectorySeparatorChar;
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        return InstallResult.Fail("Package contains an unsafe path.");
                }
                ZipFile.ExtractToDirectory(zipPath, extractRoot, true);
            }

            var files = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories);
            if (files.Length == 0) return InstallResult.Fail("Arena package is empty.");
            var cstrikeRoot = Path.GetFullPath(cstrike) + Path.DirectorySeparatorChar;

            foreach (var source in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(extractRoot, source);
                var destination = Path.GetFullPath(Path.Combine(cstrike, relative));
                if (!destination.StartsWith(cstrikeRoot, StringComparison.OrdinalIgnoreCase))
                    return InstallResult.Fail("Package contains an unsafe destination.");

                string? backup = null;
                if (File.Exists(destination))
                {
                    backup = Path.Combine(backupRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, true);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
                copied.Add((destination, backup));
            }

            File.WriteAllText(installedVersionPath, manifest.Version);
            return InstallResult.Success(manifest.Version, files.Length, backupRoot);
        }
        catch (OperationCanceledException) { Rollback(copied); return InstallResult.Fail("Installation cancelled."); }
        catch (Exception ex) { Rollback(copied); return InstallResult.Fail(ex.Message); }
        finally { try { Directory.Delete(tempRoot, true); } catch { } }
    }

    private static void Rollback(List<(string Destination, string? Backup)> copied)
    {
        foreach (var item in copied.AsEnumerable().Reverse())
        {
            try
            {
                if (item.Backup is not null && File.Exists(item.Backup)) File.Copy(item.Backup, item.Destination, true);
                else if (File.Exists(item.Destination)) File.Delete(item.Destination);
            }
            catch { }
        }
    }

    private async Task<Manifest?> DownloadManifestAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<Manifest>(stream, cancellationToken: cancellationToken);
    }

    private async Task DownloadFileAsync(Uri url, string destination, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destination);
        var buffer = new byte[1024 * 1024];
        long read = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            read += count;
            if (total is > 0) progress?.Report((int)Math.Clamp(read * 100L / total.Value, 0, 100));
        }
        progress?.Report(100);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private sealed class Manifest
    {
        public string? Version { get; set; }
        public string? PackageUrl { get; set; }
        public string? Sha256 { get; set; }
    }
}

public sealed record InstallResult(bool Installed, string Message, string? Version = null, int Files = 0, string? BackupPath = null)
{
    public static InstallResult Success(string version, int files, string backupPath) => new(true, "Arena files installed successfully.", version, files, backupPath);
    public static InstallResult AlreadyCurrent(string version) => new(true, "Arena files are already up to date.", version, 0, null);
    public static InstallResult Fail(string message) => new(false, message);
}
