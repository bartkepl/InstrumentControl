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
        // MUSI być pierwsza linia — obsługuje install/uninstall/update hooks
        // i kończy proces natychmiast gdy wywołany przez instalator
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

            // false gdy uruchomione poza instalacją Velopack (np. F5 w IDE)
            if (!manager.IsInstalled)
                return;

            var updateInfo = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            if (updateInfo == null)
                return;

            var version = updateInfo.TargetFullRelease.Version;

            // System.Windows.MessageBox działa na wątku STA bez uruchomionej App
            var result = MessageBox.Show(
                $"Dostępna jest nowa wersja: {version}\n\nCzy chcesz zaktualizować teraz?\n(Aplikacja uruchomi się ponownie po aktualizacji)",
                "Aktualizacja dostępna",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
                return;

            manager.DownloadUpdatesAsync(updateInfo).GetAwaiter().GetResult();
            manager.ApplyUpdatesAndRestart(updateInfo);
            // ApplyUpdatesAndRestart kończy ten proces — kod poniżej nie wykona się
        }
        catch
        {
            // Cicha porażka — błąd sieci/API nie może blokować startu aplikacji
        }
    }
}
