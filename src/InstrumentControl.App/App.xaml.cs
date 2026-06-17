using System.IO;
using System.Windows;
using InstrumentControl.App.Services;
using InstrumentControl.App.Views;
using InstrumentControl.Core.Blocks;
using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App;

public partial class App : Application
{
    public static VisaService VisaService { get; } = new();
    public static PluginLoader PluginLoader { get; } = new();
    public static DataManager DataManager { get; } = new();
    public static LogService LogService { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LocalizationService.Initialize();

        // Global unhandled exception handlers — show copyable error window
        DispatcherUnhandledException += (_, args) =>
        {
            LogService.Log(LogSource.Debug, $"KRYTYCZNY BŁĄD UI: {args.Exception}");
            ErrorWindow.ShowException("Błąd UI (nieobsłużony)", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception
                     ?? new Exception(args.ExceptionObject?.ToString() ?? "Nieznany błąd");
            LogService.Log(LogSource.Debug, $"KRYTYCZNY BŁĄD APLIKACJI: {ex}");
            ErrorWindow.ShowException("Błąd aplikacji (krytyczny)", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogService.Log(LogSource.Debug, $"BŁĄD ZADANIA ASYNC: {args.Exception}");
            ErrorWindow.ShowException("Błąd zadania asynchronicznego", args.Exception);
            args.SetObserved();
        };

        // Initialize VISA (falls back to simulation if NI-VISA not found)
        VisaService.Initialize();
        LogService.Log(LogSource.System, VisaService.IsSimulationMode
            ? "VISA: tryb symulacji (NI-VISA niedostępna)"
            : "VISA: NI-VISA zainicjalizowana");

        // Wire VISA/Serial communication logging
        VisaService.VisaLog  = msg => LogService.Log(LogSource.Visa, msg);
        VisaService.SerialLog = msg => LogService.Log(LogSource.Serial, msg);

        // Register built-in sequence blocks (static constructors trigger registration)
        _ = new StartBlock();
        _ = new EndBlock();
        _ = new WaitBlock();
        _ = new SaveCsvBlock();
        _ = new AddToChartBlock();
        _ = new MathBlock();
        _ = new LoopBlock();
        _ = new LogMessageBlock();
        _ = new SetVariableBlock();
        _ = new ConditionBlock();

        // Load instrument plugins from "instruments" folder next to EXE
        var instrumentsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "instruments");
        if (Directory.Exists(instrumentsDir))
        {
            PluginLoader.LoadFromDirectory(instrumentsDir);
            LogService.Log(LogSource.System, $"Pluginy załadowane z: {instrumentsDir}");
        }

        LogService.Log(LogSource.System, "Aplikacja uruchomiona");
    }
}
