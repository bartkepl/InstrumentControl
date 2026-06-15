using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App.ViewModels;

public partial class DriverInfoVm : ObservableObject
{
    public IInstrumentDriver Driver { get; }
    public string Name => $"{Driver.Manufacturer} {Driver.Model}";
    public string Description => Driver.Description;

    public DriverInfoVm(IInstrumentDriver driver) => Driver = driver;
}

public partial class ConnectionManagerViewModel : ViewModelBase
{
    private readonly VisaService _visaService;
    private readonly PluginLoader _pluginLoader;

    [ObservableProperty] private ObservableCollection<string> _availableResources = new();
    [ObservableProperty] private ObservableCollection<string> _availableComPorts = new();
    [ObservableProperty] private ObservableCollection<DriverInfoVm> _availableDrivers = new();
    [ObservableProperty] private string _selectedResource = string.Empty;
    [ObservableProperty] private string _manualResourceString = string.Empty;
    [ObservableProperty] private DriverInfoVm? _selectedDriver;
    [ObservableProperty] private string _instrumentLabel = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Kliknij 'Odśwież' aby znaleźć zasoby VISA";
    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _useSimulation;
    [ObservableProperty] private int _comBaudRate = 9600;

    public bool ConnectionSuccessful { get; private set; }
    public IInstrumentDriver? ConnectedDriver { get; private set; }

    public ConnectionManagerViewModel(VisaService visaService, PluginLoader pluginLoader)
    {
        _visaService = visaService;
        _pluginLoader = pluginLoader;
        UseSimulation = visaService.IsSimulationMode;
        LoadDrivers();
    }

    private void LoadDrivers()
    {
        AvailableDrivers.Clear();
        foreach (var (name, _, _, _) in _pluginLoader.GetAvailableDrivers())
        {
            var driver = _pluginLoader.CreateDriver(name);
            if (driver != null) AvailableDrivers.Add(new DriverInfoVm(driver));
        }
        SelectedDriver = AvailableDrivers.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RefreshResources()
    {
        IsBusy = true;
        StatusText = "Skanowanie zasobów VISA...";
        await Task.Run(() =>
        {
            var resources = _visaService.FindResources();
            var ports = _visaService.GetComPorts();
            RunOnUi(() =>
            {
                AvailableResources.Clear();
                foreach (var r in resources) AvailableResources.Add(r);
                AvailableComPorts.Clear();
                foreach (var p in ports) AvailableComPorts.Add(p);
                StatusText = $"Znaleziono {resources.Length} zasobów VISA, {ports.Length} portów COM";
                if (resources.Length > 0) SelectedResource = resources[0];
            });
        });
        IsBusy = false;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedDriver == null) return;
        IsBusy = true; TestResult = "Testowanie...";
        try
        {
            var resource = GetResourceString();
            var driver = _pluginLoader.CreateDriver(SelectedDriver.Driver.DriverName)!;
            IConnectionProvider conn = UseSimulation
                ? _visaService.OpenSimulated(resource)
                : _visaService.OpenVisaSession(resource);
            await driver.ConnectAsync(conn);
            var idn = await driver.GetIdentificationAsync();
            TestResult = $"OK: {idn}";
            await driver.DisconnectAsync();
            driver.Dispose();
        }
        catch (Exception ex)
        {
            TestResult = $"Błąd: {ex.Message}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task Connect()
    {
        if (SelectedDriver == null) { StatusText = "Wybierz driver"; return; }
        IsBusy = true; StatusText = "Łączenie...";
        try
        {
            var resource = GetResourceString();
            var driver = _pluginLoader.CreateDriver(SelectedDriver.Driver.DriverName)!;
            IConnectionProvider conn;
            if (UseSimulation)
                conn = _visaService.OpenSimulated(resource);
            else if (resource.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                conn = _visaService.OpenComSession(resource, ComBaudRate);
            else
                conn = _visaService.OpenVisaSession(resource);

            await driver.ConnectAsync(conn);
            if (!string.IsNullOrEmpty(InstrumentLabel) && driver.InstrumentInfo != null)
                driver.InstrumentInfo.UserLabel = InstrumentLabel;

            ConnectedDriver = driver;
            ConnectionSuccessful = true;
            StatusText = $"Połączono: {driver.InstrumentInfo?.DisplayName}";

            // Close the dialog window
            if (Application.Current.Windows.OfType<Views.ConnectionManagerWindow>().FirstOrDefault() is { } wnd)
                wnd.DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd połączenia: {ex.Message}";
            ConnectionSuccessful = false;
        }
        IsBusy = false;
    }

    private string GetResourceString()
    {
        if (!string.IsNullOrWhiteSpace(ManualResourceString)) return ManualResourceString.Trim();
        if (!string.IsNullOrWhiteSpace(SelectedResource)) return SelectedResource.Trim();
        return "SIM::INSTR";
    }
}
