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
    [ObservableProperty] private string _statusText = "Brak połączenia";
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

    public ItechIT6922BFrontPanelViewModel(ItechIT6922BDriver driver)
    {
        _driver      = driver;
        _isConnected = driver.IsConnected;
        _statusText  = FpConnected(driver.IsConnected);

        driver.MeasurementReceived += OnMeasurementReceived;
        driver.StatusChanged       += OnStatusChanged;
        driver.ErrorOccurred       += OnErrorOccurred;

        AppLocalization.LanguageChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                StatusText = FpConnected(IsConnected));
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
            StatusText   = status;
            IsConnected  = _driver.IsConnected;
        });
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText     = $"BŁĄD: {ex.Message}";
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
        catch (Exception ex) { StatusText = $"Błąd pomiaru: {ex.Message}"; }
        finally { IsMeasuring = false; }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task MeasureOnceAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        await UpdateReadingsAsync();
    }

    [RelayCommand]
    private async Task ReadSetpointsAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            var (v, i, on) = await _driver.ReadSetpointsAsync();
            VoltageSetpoint = v.ToString("F3", CultureInfo.InvariantCulture);
            CurrentLimit    = i.ToString("F3", CultureInfo.InvariantCulture);
            OutputEnabled   = on;
            StatusText = $"Odczytano nastawy  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { StatusText = $"Błąd odczytu nastaw: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyVoltageAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(VoltageSetpoint, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double v) || v < 0 || v > 60)
        {
            StatusText = "Nieprawidłowe napięcie (0–60 V)";
            return;
        }
        try
        {
            await _driver.SetVoltageAsync(v);
            StatusText = $"Napięcie ustawione: {v:F3} V";
        }
        catch (Exception ex) { StatusText = $"Błąd SetVoltage: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyCurrentAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(CurrentLimit, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double i) || i < 0 || i > 5)
        {
            StatusText = "Nieprawidłowy prąd (0–5 A)";
            return;
        }
        try
        {
            await _driver.SetCurrentLimitAsync(i);
            StatusText = $"Limit prądu: {i:F3} A";
        }
        catch (Exception ex) { StatusText = $"Błąd SetCurrent: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task OutputOnAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try { await _driver.SetOutputEnabledAsync(true); OutputEnabled = true; }
        catch (Exception ex) { StatusText = $"Błąd Output ON: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task OutputOffAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try { await _driver.SetOutputEnabledAsync(false); OutputEnabled = false; }
        catch (Exception ex) { StatusText = $"Błąd Output OFF: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyOvpAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(OvpLevel, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double lv))
        {
            StatusText = "Nieprawidłowy próg OVP";
            return;
        }
        try
        {
            await _driver.SetOvpLevelAsync(lv);
            await _driver.SetOvpEnabledAsync(OvpEnabled);
            StatusText = $"OVP: {lv:F2} V  {(OvpEnabled ? "ON" : "OFF")}";
        }
        catch (Exception ex) { StatusText = $"Błąd OVP: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyOcpAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(OcpLevel, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double lv))
        {
            StatusText = "Nieprawidłowy próg OCP";
            return;
        }
        try
        {
            await _driver.SetOcpLevelAsync(lv);
            await _driver.SetOcpEnabledAsync(OcpEnabled);
            StatusText = $"OCP: {lv:F2} A  {(OcpEnabled ? "ON" : "OFF")}";
        }
        catch (Exception ex) { StatusText = $"Błąd OCP: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ClearProtectionAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try { await _driver.ClearProtectionAsync(); }
        catch (Exception ex) { StatusText = $"Błąd CLR PROT: {ex.Message}"; }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleContinuousAsync()
    {
        if (IsContinuous)
        {
            _continuousCts?.Cancel();
            return;
        }

        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }

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
            StatusText   = $"Pomiar zatrzymany  {DateTime.Now:HH:mm:ss}";
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
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            StatusText = "Reset...";
            await _driver.ResetAsync();
            DisplayVoltage = "---";
            DisplayCurrent = "---";
            DisplayPower   = "---";
            OperatingMode  = "---";
            StatusText = "Reset wykonany";
        }
        catch (Exception ex) { StatusText = $"Błąd resetu: {ex.Message}"; }
    }

    private static string FpConnected(bool connected) => connected
        ? System.Windows.Application.Current?.TryFindResource("FP_Connected") as string ?? "Connected"
        : System.Windows.Application.Current?.TryFindResource("FP_NotConnected") as string ?? "Not connected";
}
