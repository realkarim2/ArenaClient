using System.Diagnostics;
using Microsoft.Win32;

namespace ArenaClient;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private const string PlaceholderGamePath = "Select your CS 1.6 root folder...";
    private readonly Label steamStatus = new();
    private readonly Label gameStatus = new();
    private readonly Label accountStatus = new();
    private readonly ProgressBar progress = new();
    private readonly TextBox gamePath = new();
    private readonly Button installButton = new();
    private readonly Button launchButton = new();
    private SteamIdentity? steamIdentity;

    public MainForm()
    {
        Text = "CS Arena Client";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 600);
        BackColor = Color.FromArgb(14, 15, 18);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        Controls.Add(new Label
        {
            Text = "CS ARENA",
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(42, 30)
        });

        Controls.Add(new Label
        {
            Text = "Competitive Counter-Strike 1.6 Client",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(160, 165, 175),
            AutoSize = true,
            Location = new Point(46, 78)
        });

        var card = new Panel
        {
            Location = new Point(42, 125),
            Size = new Size(816, 330),
            BackColor = Color.FromArgb(24, 26, 31)
        };
        Controls.Add(card);

        steamStatus.Text = "Steam: checking...";
        steamStatus.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        steamStatus.Location = new Point(28, 24);
        steamStatus.AutoSize = true;
        card.Controls.Add(steamStatus);

        accountStatus.Text = "Account: waiting for Steam";
        accountStatus.ForeColor = Color.FromArgb(170, 175, 185);
        accountStatus.Location = new Point(28, 60);
        accountStatus.AutoSize = true;
        card.Controls.Add(accountStatus);

        gameStatus.Text = "Game: CS 1.6 not selected";
        gameStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        gameStatus.Location = new Point(28, 100);
        gameStatus.AutoSize = true;
        card.Controls.Add(gameStatus);

        gamePath.Text = PlaceholderGamePath;
        gamePath.ReadOnly = true;
        gamePath.ForeColor = Color.FromArgb(190, 195, 205);
        gamePath.BackColor = Color.FromArgb(35, 38, 45);
        gamePath.Location = new Point(28, 135);
        gamePath.Size = new Size(600, 32);
        card.Controls.Add(gamePath);

        var browse = new Button
        {
            Text = "BROWSE",
            Size = new Size(120, 32),
            Location = new Point(644, 134),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 48, 55),
            ForeColor = Color.White
        };
        browse.Click += (_, _) => BrowseGameFolder();
        card.Controls.Add(browse);

        progress.Minimum = 0;
        progress.Maximum = 100;
        progress.Value = 0;
        progress.Location = new Point(28, 185);
        progress.Size = new Size(736, 24);
        card.Controls.Add(progress);

        installButton.Text = "INSTALL / UPDATE ARENA FILES";
        installButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        installButton.Size = new Size(300, 52);
        installButton.Location = new Point(28, 235);
        installButton.FlatStyle = FlatStyle.Flat;
        installButton.BackColor = Color.FromArgb(245, 125, 35);
        installButton.ForeColor = Color.White;
        installButton.Click += async (_, _) => await InstallArenaAsync();
        card.Controls.Add(installButton);

        launchButton.Text = "LAUNCH CS ARENA";
        launchButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        launchButton.Size = new Size(210, 52);
        launchButton.Location = new Point(346, 235);
        launchButton.FlatStyle = FlatStyle.Flat;
        launchButton.BackColor = Color.FromArgb(45, 48, 55);
        launchButton.ForeColor = Color.White;
        launchButton.Click += (_, _) => LaunchGame();
        card.Controls.Add(launchButton);

        Controls.Add(new Label
        {
            Text = "Steam identity is read locally. ArenaClient does not ask for or store your Steam password.",
            ForeColor = Color.FromArgb(125, 130, 140),
            AutoSize = true,
            Location = new Point(46, 490)
        });

        RefreshSteamState();
    }

    private void RefreshSteamState()
    {
        var steamRunning = Process.GetProcessesByName("steam").Length > 0;
        steamIdentity = steamRunning ? SteamIdentityReader.TryGetCurrentIdentity() : null;

        steamStatus.Text = steamRunning ? "Steam: ONLINE" : "Steam: NOT RUNNING";
        steamStatus.ForeColor = steamRunning ? Color.LightGreen : Color.OrangeRed;

        if (steamIdentity is not null)
        {
            accountStatus.Text = $"Steam: {steamIdentity.PersonaName} | SteamID64: {steamIdentity.SteamId64}";
            accountStatus.ForeColor = Color.LightGreen;
        }
        else
        {
            accountStatus.Text = steamRunning
                ? "Account: Steam is running, but no active identity was found"
                : "Account: waiting for Steam";
            accountStatus.ForeColor = Color.FromArgb(170, 175, 185);
        }

        launchButton.Enabled = steamRunning && steamIdentity is not null && gamePath.Text != PlaceholderGamePath;
    }

    private void BrowseGameFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the CS 1.6 root folder containing cstrike"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        if (!Directory.Exists(Path.Combine(dialog.SelectedPath, "cstrike")))
        {
            MessageBox.Show("This folder does not contain cstrike.", "CS Arena");
            return;
        }

        gamePath.Text = dialog.SelectedPath;
        gameStatus.Text = "Game: CS 1.6 detected";
        RefreshSteamState();
    }

    private async Task InstallArenaAsync()
    {
        if (gamePath.Text == PlaceholderGamePath || !Directory.Exists(gamePath.Text))
        {
            BrowseGameFolder();
            if (gamePath.Text == PlaceholderGamePath || !Directory.Exists(gamePath.Text))
                return;
        }

        RefreshSteamState();
        if (steamIdentity is null)
        {
            MessageBox.Show("Start Steam and sign in before installing CS Arena.", "CS Arena");
            return;
        }

        SetBusy(true);
        progress.Value = 0;
        gameStatus.Text = "Game: downloading Arena package...";

        try
        {
            var installer = new ArenaPackInstaller();
            var result = await installer.InstallAsync(
                gamePath.Text,
                ArenaPackInstaller.DefaultManifestUrl,
                new Progress<int>(value => progress.Value = value));

            if (result.Installed)
            {
                gameStatus.Text = $"Game: CS Arena ready - version {result.Version}";
                MessageBox.Show(
                    $"Arena files installed: {result.Files}\nBackup: {result.BackupPath}\nSteam: {steamIdentity.PersonaName}\nSteamID64: {steamIdentity.SteamId64}",
                    "CS Arena");
            }
            else
            {
                gameStatus.Text = "Game: install failed";
                MessageBox.Show(result.Message, "CS Arena");
            }
        }
        finally
        {
            SetBusy(false);
            RefreshSteamState();
        }
    }

    private void SetBusy(bool busy)
    {
        installButton.Enabled = !busy;
        launchButton.Enabled = !busy && steamIdentity is not null && gamePath.Text != PlaceholderGamePath;
    }

    private void LaunchGame()
    {
        RefreshSteamState();

        if (steamIdentity is null)
        {
            MessageBox.Show("Steam must be running and signed in.", "CS Arena");
            return;
        }

        if (gamePath.Text == PlaceholderGamePath || !Directory.Exists(gamePath.Text))
        {
            MessageBox.Show("Select your CS 1.6 folder first.", "CS Arena");
            return;
        }

        var exe = Path.Combine(gamePath.Text, "hl.exe");
        var launchArguments = $"-game cstrike +name \"{EscapeLaunchArgument(steamIdentity.PersonaName)}\"";

        if (File.Exists(exe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = gamePath.Text,
                UseShellExecute = true,
                Arguments = launchArguments
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "steam://run/10",
            UseShellExecute = true
        });
    }

    private static string EscapeLaunchArgument(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? FindSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
