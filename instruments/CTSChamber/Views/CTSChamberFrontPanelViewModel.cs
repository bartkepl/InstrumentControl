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
    [ObservableProperty] private string _stateLabel = "---";
    [ObservableProperty] private Brush  _stateBrush = Brushes.Gray;

    // ── UI state ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private bool   _isMeasuring;
    [ObservableProperty] private bool   _isContinuous;
    [ObservableProperty] private string _statusText = "Not connected";

    // ── Setpoint inputs ───────────────────────────────────────────────────────
    [ObservableProperty] private string _temperatureSetpoint = "25.0";
    [ObservableProperty] private string _rampUpInput         = "5.0";
    [ObservableProperty] private string _rampDownInput       = "5.0";

    // ── Continuous poll ───────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedInterval = "5000";

    public List<string> Intervals { get; } =
        new() { "1000", "2000", "5000", "10000", "30000", "60000" };

    private CancellationTokenSource? _continuousCts;
    private readonly EventHandler _languageChangedHandler;

    public CTSChamberFrontPanelViewModel(CTSChamberDriver driver)
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
            StatusText    = T("FP_ErrGeneric", "Error: {0}", ex.Message);
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
        catch (Exception ex) { StatusText = T("FP_CTS_ErrRead", "Read error: {0}", ex.Message); }
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
        catch (Exception ex) { StatusText = T("FP_CTS_ErrReadState", "State read error: {0}", ex.Message); }
    }

    private void UpdateStateIndicator(bool running, bool error, bool paused)
    {
        if (error)
        {
            StateLabel = T("FP_CTS_StateError", "ERROR");
            StateBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
        }
        else if (paused)
        {
            StateLabel = T("FP_CTS_StatePaused", "PAUSED");
            StateBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));
        }
        else if (running)
        {
            StateLabel = T("FP_CTS_StateRunning", "RUNNING");
            StateBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44));
        }
        else
        {
            StateLabel = T("FP_CTS_StateStopped", "STOP");
            StateBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ReadTemperatureAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        await RefreshTemperatureAsync();
        await RefreshStateAsync();
    }

    [RelayCommand]
    private async Task ChamberStartAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            await _driver.ChamberStartAsync();
            IsRunning  = true;
            IsPaused   = false;
            StateLabel = T("FP_CTS_StateRunning", "RUNNING");
            StateBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44));
        }
        catch (Exception ex) { StatusText = T("FP_CTS_ErrStart", "Start error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ChamberStopAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            await _driver.ChamberStopAsync();
            IsRunning  = false;
            IsPaused   = false;
            StateLabel = T("FP_CTS_StateStopped", "STOP");
            StateBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
        catch (Exception ex) { StatusText = T("FP_CTS_ErrStop", "Stop error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ChamberPauseAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            await _driver.ChamberPauseAsync();
            IsPaused   = true;
            StateLabel = T("FP_CTS_StatePaused", "PAUSED");
            StateBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));
        }
        catch (Exception ex) { StatusText = T("FP_CTS_ErrPause", "Pause error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ChamberResumeAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            await _driver.ChamberResumeAsync();
            IsPaused   = false;
            IsRunning  = true;
            StateLabel = T("FP_CTS_StateRunning", "RUNNING");
            StateBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44));
        }
        catch (Exception ex) { StatusText = T("FP_CTS_ErrResume", "Resume error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ApplyTemperatureAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        if (!double.TryParse(TemperatureSetpoint.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
            || t < -75 || t > 185)
        {
            StatusText = T("FP_CTS_InvalidTemp", "Invalid temperature (−75 … 185 °C)");
            return;
        }
        try
        {
            await _driver.SetTemperatureAsync(t);
            DisplaySetpoint = t.ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            StatusText = T("FP_CTS_TempSet", "Setpoint temperature: {0} °C", t.ToString("F1", CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { StatusText = T("FP_CTS_ErrSetTemp", "SetTemp error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ApplyRampAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        if (!double.TryParse(RampUpInput.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double up) || up < 0.01)
        {
            StatusText = T("FP_CTS_InvalidRampUp", "Invalid ramp-up gradient (≥ 0.01 K/min)");
            return;
        }
        if (!double.TryParse(RampDownInput.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double down) || down < 0.01)
        {
            StatusText = T("FP_CTS_InvalidRampDown", "Invalid ramp-down gradient (≥ 0.01 K/min)");
            return;
        }
        try
        {
            await _driver.SetRampUpAsync(up);
            await _driver.SetRampDownAsync(down);
            StatusText = T("FP_CTS_RampSet", "Gradients: up={0} K/min, down={1} K/min",
                up.ToString("F1", CultureInfo.InvariantCulture), down.ToString("F1", CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { StatusText = T("FP_CTS_ErrSetRamp", "SetRamp error: {0}", ex.Message); }
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
            StatusText   = T("FP_CTS_AutoMeasureStopped", "Auto-measure stopped {0}", DateTime.Now.ToString("HH:mm:ss"));
        }
    }

    private static string FpConnected(bool connected) => connected
        ? T("FP_Connected", "Connected")
        : T("FP_NotConnected", "Not connected");

    private static string T(string key, string fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;

    private static string T(string key, string fallback, params object[] args) =>
        string.Format(T(key, fallback), args);
}
