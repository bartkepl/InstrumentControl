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
        // Holds the version under attempt so the failure handler can record it
        // without issuing a second (un-timed) network call at startup.
        string? targetVersion = null;
        try
        {
            var source  = new GithubSource(RepoUrl, null, false);
            var manager = new UpdateManager(source);

            if (!manager.IsInstalled)
                return;

            // CheckForUpdatesAsync() has no cancellation overload in Velopack 1.2.0,
            // so enforce the startup timeout with a bounded wait. On timeout the
            // background call is abandoned and startup proceeds without updating.
            var checkTask = manager.CheckForUpdatesAsync();
            if (!checkTask.Wait(TimeSpan.FromSeconds(10)))
            {
                LogUpdate("Update check timed out — skipping");
                return;
            }

            var updateInfo = checkTask.GetAwaiter().GetResult();
            if (updateInfo == null)
                return;

            targetVersion = updateInfo.TargetFullRelease.Version.ToString();

            var (skippedVersion, failCount) = LoadUpdateState();
            if (targetVersion == skippedVersion && failCount >= MaxUpdateAttempts)
            {
                LogUpdate($"Skipping version {targetVersion} — {failCount} previous failures");
                return;
            }

            var result = MessageBox.Show(
                $"Dostępna jest nowa wersja: {targetVersion}\n\nCzy chcesz zaktualizować teraz?\n(Aplikacja uruchomi się ponownie po aktualizacji)",
                "Aktualizacja dostępna",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
                return;

            LogUpdate($"Downloading update {targetVersion}...");
            manager.DownloadUpdatesAsync(updateInfo).GetAwaiter().GetResult();
            LogUpdate($"Applying update {targetVersion}...");
            manager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (OperationCanceledException)
        {
            LogUpdate("Update check timed out — skipping");
        }
        catch (Exception ex)
        {
            // Task.Wait wraps faults in AggregateException — unwrap for a useful message.
            var message = (ex as AggregateException)?.GetBaseException().Message ?? ex.Message;
            LogUpdate($"Update failed: {message}");
            // Only count a failure against a concrete target version (download/apply
            // stage). A failure before the version is known is a transient check error,
            // not a reason to permanently skip a version.
            if (targetVersion != null)
                RecordFailure(targetVersion);
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

    private static void RecordFailure(string version)
    {
        try
        {
            var (savedVersion, failCount) = LoadUpdateState();
            int newCount = version == savedVersion ? failCount + 1 : 1;

            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(UpdateStateFile,
                $"{{\"version\":\"{version}\",\"failCount\":{newCount}}}");
            LogUpdate($"Recorded failure {newCount}/{MaxUpdateAttempts} for version {version}");
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
