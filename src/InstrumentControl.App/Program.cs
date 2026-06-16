using System;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace InstrumentControl.App;

public static class Program
{
    private const string RepoUrl = "https://github.com/bartkepl/InstrumentControl";

    [STAThread]
    public static void Main(string[] args)
    {
        // MUSI byÄ‡ pierwsza linia â€” obsĹ‚uguje install/uninstall/update hooks
        // i koĹ„czy proces natychmiast gdy wywoĹ‚any przez instalator
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

            // false gdy uruchomione poza instalacjÄ… Velopack (np. F5 w IDE)
            if (!manager.IsInstalled)
                return;

            var updateInfo = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            if (updateInfo == null)
                return;

            var version = updateInfo.TargetFullRelease.Version;

            // System.Windows.MessageBox dziaĹ‚a na wÄ…tku STA bez uruchomionej App
            var result = MessageBox.Show(
                $"DostÄ™pna jest nowa wersja: {version}\n\nCzy chcesz zaktualizowaÄ‡ teraz?\n(Aplikacja uruchomi siÄ™ ponownie po aktualizacji)",
                "Aktualizacja dostÄ™pna",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
                return;

            manager.DownloadUpdatesAsync(updateInfo).GetAwaiter().GetResult();
            manager.ApplyUpdatesAndRestart(updateInfo);
            // ApplyUpdatesAndRestart koĹ„czy ten proces â€” kod poniĹĽej nie wykona siÄ™
        }
        catch
        {
            // Cicha poraĹĽka â€” bĹ‚Ä…d sieci/API nie moĹĽe blokowaÄ‡ startu aplikacji
        }
    }
}

