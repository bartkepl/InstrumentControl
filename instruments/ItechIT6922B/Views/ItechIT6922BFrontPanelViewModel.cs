using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using InstrumentControl.Core.Views;

namespace ItechIT6922B.Views;

public partial class ItechIT6922BFrontPanelViewModel : ObservableObject
{
    private readonly ItechIT6922BDriver _driver;

    // ── Display readings ─────────────────────────────────────────────────────
    [ObservableProperty] private string _displayVoltage = "---";
    [ObservableProperty] private string _displayCurrent = "---";
    [ObservableProperty] private string _displayPower   = "---";
    [ObservableProperty] private string _operatingMode  = "---";

    // ── State ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private bool   _isMeasuring;
    [ObservableProperty] private bool   _isContinuous;
    [ObservableProperty] private bool   _outputEnabled;

    // ── Setpoints ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _voltageSetpoint = "5.000";
    [ObservableProperty] private string _currentLimit    = "1.000";

    // ── Protection ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _ovpEnabled  = true;
    [ObservableProperty] private string _ovpLevel    = "65.0";
    [ObservableProperty] private bool   _ocpEnabled  = false;
    [ObservableProperty] private string _ocpLevel    = "5.5";

    // ── Continuous ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedInterval = "1000";

    public List<string> Intervals { get; } =
        new() { "100", "200", "500", "1000", "2000", "5000", "10000" };

    private CancellationTokenSource? _continuousCts;
    private LiveDataWindow?           _liveWindow;
    private readonly EventHandler     _languageChangedHandler;

    public ItechIT6922BFrontPanelViewModel(ItechIT6922BDriver driver)
    {
        _driver      = driver;
        _isConnected = driver.IsConnected;
        _statusText  = FpConnected(driver.IsConnected);

        driver.MeasurementReceived += OnMeasurementReceived;
        driver.StatusChanged       += OnStatusChanged;
        driver.ErrorOccurred       += OnErrorOccurred;

        _languageChangedHandler = (_, _) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                StatusText = FpConnected(IsConnected));
        AppLocalization.LanguageChanged += _languageChangedHandler;

        // Instrument is typically already connected by the time the front panel
        // is created (connection happens in the connection dialog beforehand),
        // so pull its current setpoints/protection/output state right away.
        if (driver.IsConnected)
            _ = RefreshFullStateAsync();
    }

    // Called when the front panel is torn down (e.g. the user switches to a
    // different connected instrument) so this ViewModel stops reacting to driver
    // events and can be garbage collected instead of leaking as a "zombie"
    // listener that keeps handling every future measurement/status update.
    public void Detach()
    {
        _driver.MeasurementReceived     -= OnMeasurementReceived;
        _driver.StatusChanged           -= OnStatusChanged;
        _driver.ErrorOccurred           -= OnErrorOccurred;
        AppLocalization.LanguageChanged -= _languageChangedHandler;
    }

    // ── Event handlers ────────────────────────────────────────────────────────
    private void OnMeasurementReceived(object? sender, MeasurementResult r)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            switch (r.Function)
            {
                case "VOLT": DisplayVoltage = r.Value.ToString("F4", CultureInfo.InvariantCulture); break;
                case "CURR": DisplayCurrent = r.Value.ToString("F4", CultureInfo.InvariantCulture); break;
                case "POW":  DisplayPower   = r.Value.ToString("F3", CultureInfo.InvariantCulture); break;
            }
            StatusText = $"OK  {DateTime.Now:HH:mm:ss.fff}";
        });
    }

    private void OnStatusChanged(object? sender, string status)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            bool wasConnected = IsConnected;
            StatusText  = status;
            IsConnected = _driver.IsConnected;

            // Freshly (re)connected — pull the instrument's current setpoints
            // and protection state instead of showing stale/default values.
            if (IsConnected && !wasConnected)
                _ = RefreshFullStateAsync();
        });
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText     = T("FP_IT6922B_ErrPrefix", "ERROR: {0}", ex.Message);
            DisplayVoltage = "ERR";
        });
    }

    // ── Measure all ───────────────────────────────────────────────────────────
    private async Task UpdateReadingsAsync()
    {
        if (!_driver.IsConnected) return;
        IsMeasuring = true;
        try
        {
            var (v, i, p) = await _driver.MeasureAllAsync();
            DisplayVoltage = v.ToString("F4", CultureInfo.InvariantCulture);
            DisplayCurrent = i.ToString("F4", CultureInfo.InvariantCulture);
            DisplayPower   = p.ToString("F3", CultureInfo.InvariantCulture);
            OperatingMode  = await _driver.GetOperatingModeAsync();
            OutputEnabled  = await _driver.GetOutputEnabledAsync();
            StatusText     = $"OK  {DateTime.Now:HH:mm:ss.fff}";
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrMeasure", "Measurement error: {0}", ex.Message); }
        finally { IsMeasuring = false; }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task MeasureOnceAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        await UpdateReadingsAsync();
    }

    [RelayCommand]
    private async Task ReadSetpointsAsync() => await RefreshFullStateAsync();

    // Reads everything the instrument currently has configured — voltage/current
    // setpoints, output state, OVP/OCP levels and enable flags, operating mode
    // and a fresh measurement — so the panel reflects reality instead of the
    // ViewModel's compiled-in defaults. Called on startup (if already connected),
    // on every (re)connect, and from the "ODCZYT NASTAW" button.
    private async Task RefreshFullStateAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            var (v, i, on) = await _driver.ReadSetpointsAsync();
            VoltageSetpoint = v.ToString("F3", CultureInfo.InvariantCulture);
            CurrentLimit    = i.ToString("F3", CultureInfo.InvariantCulture);
            OutputEnabled   = on;

            OvpLevel   = (await _driver.GetOvpLevelAsync()).ToString("F2", CultureInfo.InvariantCulture);
            OvpEnabled = await _driver.GetOvpEnabledAsync();
            OcpLevel   = (await _driver.GetOcpLevelAsync()).ToString("F2", CultureInfo.InvariantCulture);
            OcpEnabled = await _driver.GetOcpEnabledAsync();

            await UpdateReadingsAsync();

            StatusText = T("FP_IT6922B_StateRead", "Instrument state read {0}", DateTime.Now.ToString("HH:mm:ss"));
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrReadState", "State read error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ApplyVoltageAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        if (!double.TryParse(VoltageSetpoint, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double v) || v < 0 || v > 60)
        {
            StatusText = T("FP_IT6922B_InvalidVoltage", "Invalid voltage (0–60 V)");
            return;
        }
        try
        {
            await _driver.SetVoltageAsync(v);
            StatusText = T("FP_IT6922B_VoltageSet", "Voltage set: {0} V", v.ToString("F3", CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrSetVoltage", "SetVoltage error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ApplyCurrentAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        if (!double.TryParse(CurrentLimit, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double i) || i < 0 || i > 5)
        {
            StatusText = T("FP_IT6922B_InvalidCurrent", "Invalid current (0–5 A)");
            return;
        }
        try
        {
            await _driver.SetCurrentLimitAsync(i);
            StatusText = T("FP_IT6922B_CurrentSet", "Current limit: {0} A", i.ToString("F3", CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrSetCurrent", "SetCurrent error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task OutputOnAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try { await _driver.SetOutputEnabledAsync(true); OutputEnabled = true; }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrOutputOn", "Output ON error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task OutputOffAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try { await _driver.SetOutputEnabledAsync(false); OutputEnabled = false; }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrOutputOff", "Output OFF error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ApplyOvpAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        if (!double.TryParse(OvpLevel, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double lv))
        {
            StatusText = T("FP_IT6922B_InvalidOvpThreshold", "Invalid OVP threshold");
            return;
        }
        try
        {
            await _driver.SetOvpLevelAsync(lv);
            await _driver.SetOvpEnabledAsync(OvpEnabled);
            StatusText = T("FP_IT6922B_OvpStatus", "OVP: {0} V  {1}",
                lv.ToString("F2", CultureInfo.InvariantCulture), OvpEnabled ? "ON" : "OFF");
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrOvp", "OVP error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ApplyOcpAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        if (!double.TryParse(OcpLevel, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double lv))
        {
            StatusText = T("FP_IT6922B_InvalidOcpThreshold", "Invalid OCP threshold");
            return;
        }
        try
        {
            await _driver.SetOcpLevelAsync(lv);
            await _driver.SetOcpEnabledAsync(OcpEnabled);
            StatusText = T("FP_IT6922B_OcpStatus", "OCP: {0} A  {1}",
                lv.ToString("F2", CultureInfo.InvariantCulture), OcpEnabled ? "ON" : "OFF");
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrOcp", "OCP error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ClearProtectionAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try { await _driver.ClearProtectionAsync(); }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrClrProt", "CLR PROT error: {0}", ex.Message); }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleContinuousAsync()
    {
        if (IsContinuous)
        {
            _continuousCts?.Cancel();
            return;
        }

        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }

        IsContinuous      = true;
        _continuousCts    = new CancellationTokenSource();
        var ct            = _continuousCts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await UpdateReadingsAsync();
                int ms = int.TryParse(SelectedInterval, out int iv) ? iv : 1000;
                try { await Task.Delay(ms, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            IsContinuous = false;
            StatusText   = T("FP_IT6922B_MeasureStopped", "Measurement stopped {0}", DateTime.Now.ToString("HH:mm:ss"));
        }
    }

    [RelayCommand]
    private void OpenLiveWindow()
    {
        if (_liveWindow != null && _liveWindow.IsLoaded)
        {
            _liveWindow.Activate();
            return;
        }
        _liveWindow = new LiveDataWindow(_driver);
        _liveWindow.Show();
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            StatusText = T("FP_IT6922B_Resetting", "Reset...");
            await _driver.ResetAsync();
            DisplayVoltage = "---";
            DisplayCurrent = "---";
            DisplayPower   = "---";
            OperatingMode  = "---";
            StatusText = T("FP_IT6922B_ResetDone", "Reset done");
        }
        catch (Exception ex) { StatusText = T("FP_IT6922B_ErrReset", "Reset error: {0}", ex.Message); }
    }

    private static string FpConnected(bool connected) => connected
        ? T("FP_Connected", "Connected")
        : T("FP_NotConnected", "Not connected");

    // Looks up a localized string from the app's merged resource dictionaries.
    // Uses TryFindResource (not a direct project reference to LocalizationService)
    // so this instrument plugin stays decoupled from InstrumentControl.App.
    private static string T(string key, string fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;

    private static string T(string key, string fallback, params object[] args) =>
        string.Format(T(key, fallback), args);
}
