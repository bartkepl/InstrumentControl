using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.App.Services;
using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App.ViewModels;

public partial class ConnectedInstrumentVm : ObservableObject
{
    public IInstrumentDriver Driver { get; }

    [ObservableProperty] private string _statusText = LocalizationService.Get("VM_StatusConnected");
    [ObservableProperty] private bool _isConnected = true;
    [ObservableProperty] private string _lastValue = "";

    public string DisplayName => Driver.InstrumentInfo?.DisplayName ?? Driver.DriverName;
    public string ResourceName => Driver.InstrumentInfo?.ResourceName ?? "?";

    public ConnectedInstrumentVm(IInstrumentDriver driver, ILogService logService)
    {
        Driver = driver;
        Driver.StatusChanged += (_, msg) => RunOnUi(() =>
        {
            StatusText = msg;
            logService.Log(LogSource.Instrument, $"[{driver.DriverName}] {msg}");
        });
        Driver.MeasurementReceived += (_, r) => RunOnUi(() =>
        {
            LastValue = $"{r.Value:G6} {r.Unit}";
            var name = string.IsNullOrEmpty(r.InstrumentName) ? driver.DriverName : r.InstrumentName;
            var ch   = string.IsNullOrEmpty(r.ChannelId) ? "" : $"/{r.ChannelId}";
            logService.Log(LogSource.Instrument, $"[{name}{ch}] {r.Function}: {r.Value:G6} {r.Unit}");
        });
        Driver.ErrorOccurred += (_, ex) => RunOnUi(() =>
        {
            StatusText = $"{LocalizationService.Get("VM_ErrorPrefix")} {ex.Message}";
            IsConnected = false;
            logService.Log(LogSource.Instrument, $"[{driver.DriverName}] BŁĄD: {ex.Message}");
        });
    }

    private void RunOnUi(Action a) =>
        Application.Current?.Dispatcher.BeginInvoke(a);
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly VisaService _visaService;
    private readonly PluginLoader _pluginLoader;
    private readonly DataManager _dataManager;
    private readonly ILogService _logService;

    [ObservableProperty] private ObservableCollection<ConnectedInstrumentVm> _connectedInstruments = new();
    [ObservableProperty] private ConnectedInstrumentVm? _selectedInstrument;
    [ObservableProperty] private int _selectedTabIndex = 0;
    [ObservableProperty] private string _statusBarText = "Gotowy";
    [ObservableProperty] private bool _isVisaSimulated;

    public MainWindowViewModel(VisaService visaService, PluginLoader pluginLoader, DataManager dataManager, ILogService logService)
    {
        _visaService = visaService;
        _pluginLoader = pluginLoader;
        _dataManager = dataManager;
        _logService = logService;
        IsVisaSimulated = visaService.IsSimulationMode;
        StatusBarText = visaService.IsSimulationMode
            ? LocalizationService.Get("StatusBar_SimStartup")
            : LocalizationService.Get("StatusBar_VisaStartup");

        LocalizationService.LanguageChanged += (_, _) => RunOnUi(() =>
        {
            StatusBarText = IsVisaSimulated
                ? LocalizationService.Get("StatusBar_SimStartup")
                : LocalizationService.Get("StatusBar_VisaStartup");
            OnPropertyChanged(nameof(IsVisaSimulated));
        });
    }

    [RelayCommand]
    private void RemoveInstrument(ConnectedInstrumentVm? vm)
    {
        if (vm == null) return;
        _logService.Log(LogSource.Event, $"Rozłączanie instrumentu: {vm.Driver.DriverName} ({vm.ResourceName})");
        vm.Driver.DisconnectAsync().ContinueWith(_ => RunOnUi(() =>
        {
            ConnectedInstruments.Remove(vm);
            if (SelectedInstrument == vm)
                SelectedInstrument = ConnectedInstruments.FirstOrDefault();
        }));
    }

    public void AddConnectedInstrument(IInstrumentDriver driver)
    {
        _logService.Log(LogSource.Event,
            $"Instrument dodany: {driver.DriverName} @ {driver.InstrumentInfo?.ResourceName ?? "?"}");
        var vm = new ConnectedInstrumentVm(driver, _logService);
        ConnectedInstruments.Add(vm);
        SelectedInstrument = vm;
    }

    public Dictionary<string, IInstrumentDriver> GetInstrumentsDictionary()
    {
        var dict = new Dictionary<string, IInstrumentDriver>();
        foreach (var vm in ConnectedInstruments)
        {
            var key = vm.Driver.InstrumentInfo?.UserLabel is { Length: > 0 } lbl
                ? lbl
                : vm.DisplayName;
            dict[key] = vm.Driver;
        }
        return dict;
    }

    public IEnumerable<string> GetInstrumentNames() =>
        ConnectedInstruments.Select(vm =>
            vm.Driver.InstrumentInfo?.UserLabel is { Length: > 0 } lbl ? lbl : vm.DisplayName);
}
