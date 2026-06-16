using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace CTSChamber.Views;

public partial class CTSChamberFrontPanelViewModel : ObservableObject
{
    private readonly CTSChamberDriver _driver;

    // ── Temperature display ───────────────────────────────────────────────────
    [ObservableProperty] private string _displayActual   = "---.-";
    [ObservableProperty] private string _displaySetpoint = "---.-";

    // ── Chamber state ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isRunning;
    [ObservableProperty] private bool   _isError;
    [ObservableProperty] private bool   _isPaused;
    [ObservableProperty] private string _stateLabel = "STOP";
    [ObservableProperty] private Brush  _stateBrush = Brushes.Gray;

    // ── UI state ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private bool   _isMeasuring;
    [ObservableProperty] private bool   _isContinuous;
    [ObservableProperty] private string _statusText = "Brak połączenia";

    // ── Setpoint inputs ───────────────────────────────────────────────────────
    [ObservableProperty] private string _temperatureSetpoint = "25.0";
    [ObservableProperty] private string _rampUpInput         = "5.0";
    [ObservableProperty] private string _rampDownInput       = "5.0";

    // ── Continuous poll ───────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedInterval = "5000";

    public List<string> Intervals { get; } =
        new() { "1000", "2000", "5000", "10000", "30000", "60000" };

    private CancellationTokenSource? _continuousCts;

    public CTSChamberFrontPanelViewModel(CTSChamberDriver driver)
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
            if (r.Function == "TEMP")
                DisplayActual = r.Value.ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
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
            StatusText    = $"BŁĄD: {ex.Message}";
            DisplayActual = "ERR";
        });
    }

    // ── Update helpers ────────────────────────────────────────────────────────

    private async Task RefreshTemperatureAsync()
    {
        if (!_driver.IsConnected) return;
        IsMeasuring = true;
        try
        {
            var (actual, setpoint) = await _driver.ReadTemperatureAsync();
            DisplayActual   = actual  .ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            DisplaySetpoint = setpoint.ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            StatusText = $"OK  {DateTime.Now:HH:mm:ss.fff}";
        }
        catch (Exception ex) { StatusText = $"Błąd odczytu: {ex.Message}"; }
        finally { IsMeasuring = false; }
    }

    private async Task RefreshStateAsync()
    {
        if (!_driver.IsConnected) return;
        try
        {
            var (running, error, paused) = await _driver.ReadChamberStateAsync();
            IsRunning = running;
            IsError   = error;
            IsPaused  = paused;
            UpdateStateIndicator(running, error, paused);
        }
        catch (Exception ex) { StatusText = $"Błąd odczytu stanu: {ex.Message}"; }
    }

    private void UpdateStateIndicator(bool running, bool error, bool paused)
    {
        if (error)
        {
            StateLabel = "BŁĄD";
            StateBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
        }
        else if (paused)
        {
            StateLabel = "PAUZA";
            StateBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));
        }
        else if (running)
        {
            StateLabel = "PRACA";
            StateBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44));
        }
        else
        {
            StateLabel = "STOP";
            StateBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ReadTemperatureAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        await RefreshTemperatureAsync();
        await RefreshStateAsync();
    }

    [RelayCommand]
    private async Task ChamberStartAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            await _driver.ChamberStartAsync();
            IsRunning  = true;
            IsPaused   = false;
            StateLabel = "PRACA";
            StateBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44));
        }
        catch (Exception ex) { StatusText = $"Błąd Start: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ChamberStopAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            await _driver.ChamberStopAsync();
            IsRunning  = false;
            IsPaused   = false;
            StateLabel = "STOP";
            StateBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
        catch (Exception ex) { StatusText = $"Błąd Stop: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ChamberPauseAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            await _driver.ChamberPauseAsync();
            IsPaused   = true;
            StateLabel = "PAUZA";
            StateBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));
        }
        catch (Exception ex) { StatusText = $"Błąd Pauza: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ChamberResumeAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            await _driver.ChamberResumeAsync();
            IsPaused   = false;
            IsRunning  = true;
            StateLabel = "PRACA";
            StateBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44));
        }
        catch (Exception ex) { StatusText = $"Błąd Wznów: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyTemperatureAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(TemperatureSetpoint.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
            || t < -75 || t > 185)
        {
            StatusText = "Nieprawidłowa temperatura (−75 … 185 °C)";
            return;
        }
        try
        {
            await _driver.SetTemperatureAsync(t);
            DisplaySetpoint = t.ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            StatusText = $"Temperatura zadana: {t:F1} °C";
        }
        catch (Exception ex) { StatusText = $"Błąd SetTemp: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyRampAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(RampUpInput.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double up) || up < 0.01)
        {
            StatusText = "Nieprawidłowy gradient wzrostu (≥ 0.01 K/min)";
            return;
        }
        if (!double.TryParse(RampDownInput.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double down) || down < 0.01)
        {
            StatusText = "Nieprawidłowy gradient spadku (≥ 0.01 K/min)";
            return;
        }
        try
        {
            await _driver.SetRampUpAsync(up);
            await _driver.SetRampDownAsync(down);
            StatusText = $"Gradienty: wzrost={up:F1} K/min, spadek={down:F1} K/min";
        }
        catch (Exception ex) { StatusText = $"Błąd SetRamp: {ex.Message}"; }
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

        IsContinuous   = true;
        _continuousCts = new CancellationTokenSource();
        var ct         = _continuousCts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshTemperatureAsync();
                await RefreshStateAsync();
                int ms = int.TryParse(SelectedInterval, out int iv) ? iv : 5000;
                try { await Task.Delay(ms, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            IsContinuous = false;
            StatusText   = $"Auto-pomiar zatrzymany  {DateTime.Now:HH:mm:ss}";
        }
    }

    private static string FpConnected(bool connected) => connected
        ? System.Windows.Application.Current?.TryFindResource("FP_Connected") as string ?? "Connected"
        : System.Windows.Application.Current?.TryFindResource("FP_NotConnected") as string ?? "Not connected";
}
