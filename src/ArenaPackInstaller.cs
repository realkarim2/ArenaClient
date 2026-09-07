using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ArenaClient;

public sealed class ArenaPackInstaller
{
    public const string DefaultManifestUrl = "https://csarena.pages.dev/client/manifest.json";

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public async Task<InstallResult> InstallAsync(string gameDirectory, string manifestUrl, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(gameDirectory))
            return InstallResult.Fail("Game directory does not exist.");

        var cstrike = Path.Combine(gameDirectory, "cstrike");
        if (!Directory.Exists(cstrike))
            return InstallResult.Fail("cstrike folder was not found. Select the CS 1.6 root folder.");

        var manifest = await DownloadManifestAsync(manifestUrl, cancellationToken);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.PackageUrl))
            return InstallResult.Fail("Arena package manifest is unavailable.");

        var tempRoot = Path.Combine(Path.GetTempPath(), "CSArena", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, "arena-pack.zip");
        var backupRoot = Path.Combine(gameDirectory, ".csarena_backup", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        try
        {
            await DownloadFileAsync(manifest.PackageUrl, zipPath, progress, cancellationToken);

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var actualHash = await ComputeSha256Async(zipPath, cancellationToken);
                if (!actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    return InstallResult.Fail("Arena package integrity check failed.");
            }

            var extractRoot = Path.Combine(tempRoot, "package");
            ZipFile.ExtractToDirectory(zipPath, extractRoot);

            var files = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                return InstallResult.Fail("Arena package is empty.");

            foreach (var source in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(extractRoot, source);
                var destination = Path.GetFullPath(Path.Combine(cstrike, relative));
                var root = Path.GetFullPath(cstrike) + Path.DirectorySeparatorChar;

                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return InstallResult.Fail("Package contains an unsafe path.");

                if (File.Exists(destination))
                {
                    var backup = Path.Combine(backupRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }

            var installedVersion = manifest.Version ?? "unknown";
            File.WriteAllText(Path.Combine(gameDirectory, ".csarena-version"), installedVersion);
            return InstallResult.Success(installedVersion, files.Length, backupRoot);
        }
        catch (OperationCanceledException)
        {
            return InstallResult.Fail("Installation cancelled.");
        }
        catch (Exception ex)
        {
            return InstallResult.Fail(ex.Message);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private async Task<Manifest?> DownloadManifestAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<Manifest>(stream, cancellationToken: cancellationToken);
    }

    private async Task DownloadFileAsync(string url, string destination, IProgress<int>? progress, CancellationToken cancellationToken)
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
            if (total.HasValue && total.Value > 0)
                progress?.Report((int)Math.Clamp(read * 100L / total.Value, 0, 100));
        }
        progress?.Report(100);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
    public static InstallResult Fail(string message) => new(false, message);
}
