using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App.ViewModels;

public partial class ConnectedInstrumentVm : ObservableObject
{
    public IInstrumentDriver Driver { get; }

    [ObservableProperty] private string _statusText = "Połączony";
    [ObservableProperty] private bool _isConnected = true;
    [ObservableProperty] private string _lastValue = "";

    public string DisplayName => Driver.InstrumentInfo?.DisplayName ?? Driver.DriverName;
    public string ResourceName => Driver.InstrumentInfo?.ResourceName ?? "?";

    public ConnectedInstrumentVm(IInstrumentDriver driver)
    {
        Driver = driver;
        Driver.StatusChanged += (_, msg) => RunOnUi(() => StatusText = msg);
        Driver.MeasurementReceived += (_, r) => RunOnUi(() => LastValue = $"{r.Value:G6} {r.Unit}");
        Driver.ErrorOccurred += (_, ex) => RunOnUi(() => { StatusText = $"Błąd: {ex.Message}"; IsConnected = false; });
    }

    private void RunOnUi(Action a) =>
        Application.Current?.Dispatcher.BeginInvoke(a);
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly VisaService _visaService;
    private readonly PluginLoader _pluginLoader;
    private readonly DataManager _dataManager;

    [ObservableProperty] private ObservableCollection<ConnectedInstrumentVm> _connectedInstruments = new();
    [ObservableProperty] private ConnectedInstrumentVm? _selectedInstrument;
    [ObservableProperty] private int _selectedTabIndex = 0;
    [ObservableProperty] private string _statusBarText = "Gotowy";
    [ObservableProperty] private bool _isVisaSimulated;

    public MainWindowViewModel(VisaService visaService, PluginLoader pluginLoader, DataManager dataManager)
    {
        _visaService = visaService;
        _pluginLoader = pluginLoader;
        _dataManager = dataManager;
        IsVisaSimulated = visaService.IsSimulationMode;
        StatusBarText = visaService.IsSimulationMode
            ? "Tryb symulacji (NI-VISA nie znaleziono)"
            : "NI-VISA aktywne";
    }

    [RelayCommand]
    private void RemoveInstrument(ConnectedInstrumentVm? vm)
    {
        if (vm == null) return;
        vm.Driver.DisconnectAsync().ContinueWith(_ => RunOnUi(() =>
        {
            ConnectedInstruments.Remove(vm);
            if (SelectedInstrument == vm)
                SelectedInstrument = ConnectedInstruments.FirstOrDefault();
        }));
    }

    public void AddConnectedInstrument(IInstrumentDriver driver)
    {
        var vm = new ConnectedInstrumentVm(driver);
        ConnectedInstruments.Add(vm);
        SelectedInstrument = vm;

        // Forward all measurements from front panel to DataManager (→ chart/data view)
        driver.MeasurementReceived += (_, r) => _dataManager.AddResult(r);
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
