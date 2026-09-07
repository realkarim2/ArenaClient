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
    private readonly Label steamStatus = new();
    private readonly Label accountStatus = new();
    private readonly Button launchButton = new();
    private readonly Button settingsButton = new();

    public MainForm()
    {
        Text = "CS Arena Client";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 560);
        BackColor = Color.FromArgb(14, 15, 18);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var title = new Label
        {
            Text = "CS ARENA",
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(42, 35)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Competitive Counter-Strike 1.6 Client",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(160, 165, 175),
            AutoSize = true,
            Location = new Point(46, 82)
        };
        Controls.Add(subtitle);

        var card = new Panel
        {
            Location = new Point(42, 135),
            Size = new Size(816, 270),
            BackColor = Color.FromArgb(24, 26, 31)
        };
        Controls.Add(card);

        steamStatus.Text = "Steam: checking...";
        steamStatus.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        steamStatus.Location = new Point(28, 28);
        steamStatus.AutoSize = true;
        card.Controls.Add(steamStatus);

        accountStatus.Text = "Account: waiting for Steam";
        accountStatus.Font = new Font("Segoe UI", 11);
        accountStatus.ForeColor = Color.FromArgb(170, 175, 185);
        accountStatus.Location = new Point(28, 70);
        accountStatus.AutoSize = true;
        card.Controls.Add(accountStatus);

        launchButton.Text = "LAUNCH CS 1.6";
        launchButton.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        launchButton.Size = new Size(220, 52);
        launchButton.Location = new Point(28, 145);
        launchButton.FlatStyle = FlatStyle.Flat;
        launchButton.BackColor = Color.FromArgb(245, 125, 35);
        launchButton.ForeColor = Color.White;
        launchButton.Click += (_, _) => LaunchGame();
        card.Controls.Add(launchButton);

        settingsButton.Text = "SETTINGS";
        settingsButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        settingsButton.Size = new Size(140, 52);
        settingsButton.Location = new Point(264, 145);
        settingsButton.FlatStyle = FlatStyle.Flat;
        settingsButton.BackColor = Color.FromArgb(45, 48, 55);
        settingsButton.ForeColor = Color.White;
        settingsButton.Click += (_, _) => MessageBox.Show("Steam integration and game settings will be added here.", "CS Arena");
        card.Controls.Add(settingsButton);

        var footer = new Label
        {
            Text = "Steam must be running. Steam credentials are never requested by CS Arena.",
            ForeColor = Color.FromArgb(125, 130, 140),
            AutoSize = true,
            Location = new Point(46, 455)
        };
        Controls.Add(footer);

        RefreshSteamState();
    }

    private void RefreshSteamState()
    {
        var steam = Process.GetProcessesByName("steam").Length > 0;
        steamStatus.Text = steam ? "Steam: ONLINE" : "Steam: NOT RUNNING";
        steamStatus.ForeColor = steam ? Color.LightGreen : Color.OrangeRed;
        launchButton.Enabled = steam;

        if (steam)
        {
            accountStatus.Text = "Account: Steam detected - identity sync ready for next stage";
        }
    }

    private void LaunchGame()
    {
        var installPath = FindSteamInstallPath();
        if (installPath is null)
        {
            MessageBox.Show("Steam installation was not found.", "CS Arena");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "steam://run/10",
            UseShellExecute = true
        });
    }

    private static string? FindSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
