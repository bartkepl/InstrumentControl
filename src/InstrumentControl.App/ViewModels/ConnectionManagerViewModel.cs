using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.App.Services;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App.ViewModels;

public partial class DriverInfoVm : ObservableObject
{
    public IInstrumentDriver Driver { get; }
    public string Name => $"{Driver.Manufacturer} {Driver.Model}";
    public string Description => LocalizationService.TryGet($"DriverDesc_{Driver.DriverName}") ?? Driver.Description;

    [ObservableProperty] private bool _isDetected;
    [ObservableProperty] private bool _isGrayedOut;

    public DriverInfoVm(IInstrumentDriver driver)
    {
        Driver = driver;
        LocalizationService.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Description));
    }
}

public partial class ResourceItemVm : ObservableObject
{
    public string Address { get; }

    /// <summary>Zasób jest już zajęty przez połączony przyrząd — nie można go wybrać.</summary>
    [ObservableProperty] private bool _isInUse;

    public ResourceItemVm(string address, bool isInUse)
    {
        Address = address;
        IsInUse = isInUse;
    }
}

public partial class ConnectionManagerViewModel : ViewModelBase
{
    private readonly VisaService _visaService;
    private readonly PluginLoader _pluginLoader;
    private readonly MainWindowViewModel? _mainVm;
    private CancellationTokenSource? _detectCts;

    [ObservableProperty] private ObservableCollection<ResourceItemVm> _availableResources = new();
    [ObservableProperty] private ObservableCollection<string> _availableComPorts = new();
    [ObservableProperty] private ObservableCollection<DriverInfoVm> _availableDrivers = new();
    [ObservableProperty] private ResourceItemVm? _selectedResourceItem;
    [ObservableProperty] private string _selectedResource = string.Empty;
    [ObservableProperty] private string _manualResourceString = string.Empty;
    [ObservableProperty] private DriverInfoVm? _selectedDriver;
    [ObservableProperty] private string _instrumentLabel = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _useSimulation;
    [ObservableProperty] private int _comBaudRate = 9600;
    [ObservableProperty] private string _detectedIdnText = string.Empty;
    [ObservableProperty] private bool _isAutoDetecting;

    public bool ConnectionSuccessful { get; private set; }
    public IInstrumentDriver? ConnectedDriver { get; private set; }

    public ConnectionManagerViewModel(VisaService visaService, PluginLoader pluginLoader, MainWindowViewModel? mainVm = null)
    {
        _visaService = visaService;
        _pluginLoader = pluginLoader;
        _mainVm = mainVm;
        UseSimulation = visaService.IsSimulationMode;
        StatusText = LocalizationService.Get("ConnMgr_InitStatus");
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

    partial void OnSelectedDriverChanged(DriverInfoVm? value)
    {
        if (value == null) return;
        var driverName = value.Driver.DriverName;
        if (_mainVm == null)
        {
            InstrumentLabel = driverName;
            return;
        }
        var existing = _mainVm.GetInstrumentNames()
            .Where(n => n.StartsWith(driverName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (existing.Count == 0)
            InstrumentLabel = driverName;
        else
            InstrumentLabel = $"{driverName} No.{existing.Count + 1}";
    }

    // ── Auto-detection ───────────────────────────────────────────────────────

    partial void OnSelectedResourceItemChanged(ResourceItemVm? value)
    {
        // In-use resources are not selectable in the list, but guard anyway.
        SelectedResource = value is { IsInUse: false } ? value.Address : string.Empty;
    }

    partial void OnSelectedResourceChanged(string value)
    {
        bool canAutoDetect = !string.IsNullOrEmpty(value)
            && !value.StartsWith("ASRL", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("SIM::", StringComparison.OrdinalIgnoreCase)
            && !UseSimulation;

        if (canAutoDetect)
            TriggerAutoDetect(value);
        else
            ClearDetection();
    }

    private void TriggerAutoDetect(string resource)
    {
        _detectCts?.Cancel();
        _detectCts = new CancellationTokenSource();
        _ = AutoDetectAsync(resource, _detectCts.Token);
    }

    private async Task AutoDetectAsync(string resource, CancellationToken ct)
    {
        IsAutoDetecting = true;
        DetectedIdnText = string.Empty;
        ClearDetectionMarkers();

        // Debounce — rapid list navigation shouldn't spam VISA
        try { await Task.Delay(350, ct); }
        catch (OperationCanceledException) { IsAutoDetecting = false; return; }

        if (ct.IsCancellationRequested) { IsAutoDetecting = false; return; }

        string? idn = null;
        try
        {
            idn = await Task.Run(async () =>
            {
                var conn = _visaService.OpenVisaSession(resource);
                await conn.OpenAsync();
                try
                {
                    return await conn.QueryAsync("*IDN?", 2000);
                }
                finally
                {
                    try { await conn.CloseAsync(); } catch { }
                    conn.Dispose();
                }
            }, ct);
        }
        catch (OperationCanceledException) { IsAutoDetecting = false; return; }
        catch { /* device may not support *IDN? or be on a different bus */ }

        if (ct.IsCancellationRequested) { IsAutoDetecting = false; return; }

        if (!string.IsNullOrWhiteSpace(idn))
        {
            var matched = MatchDriver(idn);
            DetectedIdnText = $"IDN: {idn.Trim()}";
            foreach (var dvm in AvailableDrivers)
            {
                dvm.IsDetected = dvm == matched;
                dvm.IsGrayedOut = matched != null && dvm != matched;
            }
            if (matched != null)
                SelectedDriver = matched;
        }

        IsAutoDetecting = false;
    }

    private DriverInfoVm? MatchDriver(string idn)
    {
        string upper = idn.ToUpperInvariant();
        return AvailableDrivers.FirstOrDefault(d =>
            !string.IsNullOrEmpty(d.Driver.Model) &&
            upper.Contains(d.Driver.Model.ToUpperInvariant()));
    }

    private void ClearDetectionMarkers()
    {
        foreach (var dvm in AvailableDrivers)
        {
            dvm.IsDetected = false;
            dvm.IsGrayedOut = false;
        }
    }

    private void ClearDetection()
    {
        _detectCts?.Cancel();
        DetectedIdnText = string.Empty;
        IsAutoDetecting = false;
        ClearDetectionMarkers();
    }

    public void CancelPending() => _detectCts?.Cancel();

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshResources()
    {
        IsBusy = true;
        ClearDetection();
        StatusText = LocalizationService.Get("VM_ScanningVisa");
        await Task.Run(() =>
        {
            var resources = _visaService.FindResources();
            var ports = _visaService.GetComPorts();
            RunOnUi(() =>
            {
                AvailableResources.Clear();
                foreach (var r in resources)
                    AvailableResources.Add(new ResourceItemVm(r, IsResourceBusy(r)));
                AvailableComPorts.Clear();
                foreach (var p in ports) AvailableComPorts.Add(p);
                StatusText = string.Format(LocalizationService.Get("VM_FoundResources"), resources.Length, ports.Length);
                // Preselect the first resource that is not already in use.
                SelectedResourceItem = AvailableResources.FirstOrDefault(r => !r.IsInUse);
            });
        });
        IsBusy = false;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedDriver == null) return;
        var resource = GetResourceString();
        if (IsResourceBusy(resource))
        {
            TestResult = string.Format(LocalizationService.Get("VM_ResourceInUse"), resource);
            return;
        }
        IsBusy = true; TestResult = LocalizationService.Get("VM_Testing");
        try
        {
            var driver = _pluginLoader.CreateDriver(SelectedDriver.Driver.DriverName)!;
            IConnectionProvider conn = UseSimulation
                ? _visaService.OpenSimulated(resource)
                : _visaService.OpenVisaSession(resource);
            await driver.ConnectAsync(conn);
            var idn = await driver.GetIdentificationAsync();
            TestResult = $"{LocalizationService.Get("VM_TestOk")} {idn}";
            await driver.DisconnectAsync();
            driver.Dispose();
        }
        catch (Exception ex)
        {
            TestResult = $"{LocalizationService.Get("VM_ErrorPrefix")} {ex.Message}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task Connect()
    {
        if (SelectedDriver == null) { StatusText = LocalizationService.Get("VM_SelectDriver"); return; }
        var resource = GetResourceString();
        if (IsResourceBusy(resource))
        {
            StatusText = string.Format(LocalizationService.Get("VM_ResourceInUse"), resource);
            return;
        }
        IsBusy = true; StatusText = LocalizationService.Get("VM_Connecting");
        try
        {
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
            StatusText = $"{LocalizationService.Get("VM_Connected")} {driver.InstrumentInfo?.DisplayName}";

            if (Application.Current.Windows.OfType<Views.ConnectionManagerWindow>().FirstOrDefault() is { } wnd)
                wnd.DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText = $"{LocalizationService.Get("VM_ConnectionError")} {ex.Message}";
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

    /// <summary>
    /// Czy adres jest już zajęty przez połączony przyrząd. Zasoby symulowane
    /// (SIM::) mogą być współdzielone, więc dla nich blokada nie obowiązuje.
    /// </summary>
    private bool IsResourceBusy(string resource) =>
        !UseSimulation
        && !resource.StartsWith("SIM::", StringComparison.OrdinalIgnoreCase)
        && _mainVm?.IsResourceInUse(resource) == true;
}
