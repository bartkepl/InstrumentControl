using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace InstrumentControl.App;

public static class Program
{
    private const string RepoUrl = "https://github.com/bartkepl/InstrumentControl";
    private const int MaxUpdateAttempts = 3;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InstrumentControl");

    private static readonly string UpdateStateFile = Path.Combine(SettingsDir, "update_state.json");

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        CheckAndApplyUpdateBeforeStart();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void CheckAndApplyUpdateBeforeStart()
    {
        try
        {
            var source  = new GithubSource(RepoUrl, null, false);
            var manager = new UpdateManager(source);

            if (!manager.IsInstalled)
                return;

            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            var updateInfo = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            if (updateInfo == null)
                return;

            var version = updateInfo.TargetFullRelease.Version.ToString();

            var (skippedVersion, failCount) = LoadUpdateState();
            if (version == skippedVersion && failCount >= MaxUpdateAttempts)
            {
                LogUpdate($"Skipping version {version} — {failCount} previous failures");
                return;
            }

            var result = MessageBox.Show(
                $"Dostępna jest nowa wersja: {version}\n\nCzy chcesz zaktualizować teraz?\n(Aplikacja uruchomi się ponownie po aktualizacji)",
                "Aktualizacja dostępna",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
                return;

            LogUpdate($"Downloading update {version}...");
            manager.DownloadUpdatesAsync(updateInfo).GetAwaiter().GetResult();
            LogUpdate($"Applying update {version}...");
            manager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (OperationCanceledException)
        {
            LogUpdate("Update check timed out — skipping");
        }
        catch (Exception ex)
        {
            LogUpdate($"Update failed: {ex.Message}");
            RecordFailure(ex);
        }
    }

    private static (string? version, int failCount) LoadUpdateState()
    {
        try
        {
            if (!File.Exists(UpdateStateFile)) return (null, 0);
            var json = File.ReadAllText(UpdateStateFile);
            var versionMatch = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            var countMatch = Regex.Match(json, "\"failCount\"\\s*:\\s*(\\d+)");
            var version = versionMatch.Success ? versionMatch.Groups[1].Value : null;
            var count = countMatch.Success ? int.Parse(countMatch.Groups[1].Value) : 0;
            return (version, count);
        }
        catch { return (null, 0); }
    }

    private static void RecordFailure(Exception ex)
    {
        try
        {
            var source = new GithubSource(RepoUrl, null, false);
            var manager = new UpdateManager(source);
            var info = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            if (info == null) return;

            var version = info.TargetFullRelease.Version.ToString();
            var (savedVersion, failCount) = LoadUpdateState();
            int newCount = version == savedVersion ? failCount + 1 : 1;

            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(UpdateStateFile,
                $"{{\"version\":\"{version}\",\"failCount\":{newCount}}}");
        }
        catch { }
    }

    private static void LogUpdate(string message)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var logFile = Path.Combine(SettingsDir, "update.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
