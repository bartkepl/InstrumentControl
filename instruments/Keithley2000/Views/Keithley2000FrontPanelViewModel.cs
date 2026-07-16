using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using InstrumentControl.Core.Views;

namespace Keithley2000.Views;

public partial class Keithley2000FrontPanelViewModel : ObservableObject
{
    private readonly Keithley2000Driver _driver;

    // ── Display ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _displayValue = "---";
    [ObservableProperty] private string _displayUnit = "V";
    [ObservableProperty] private string _displayFunction = "DCV";

    // ── State ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private bool _isMeasuring;
    [ObservableProperty] private bool _isContinuous;

    // ── MATH ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedMathMode = "OFF";
    [ObservableProperty] private bool _isMathActive = false;

    // ── LCD control ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _displayMessage = "";
    [ObservableProperty] private bool _isDisplayEnabled = true;

    // ── Auto-zero ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isAutoZeroEnabled = true;

    // ── Burst measurement ────────────────────────────────────────────────────
    [ObservableProperty] private int _burstCount = 10;
    [ObservableProperty] private string _burstResultsText = "---";

    // ── LIMIT test ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _limitLow = "0.0";
    [ObservableProperty] private string _limitHigh = "10.0";
    [ObservableProperty] private bool _isLimitEnabled = false;
    [ObservableProperty] private string _limitStatus = "---";
    [ObservableProperty] private bool _limitPass = false;

    // ── Selection ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedFunction = "DCV";
    [ObservableProperty] private string _selectedRange = "AUTO";
    [ObservableProperty] private string _selectedNplc = "1";
    [ObservableProperty] private string _selectedInterval = "1000";
    [ObservableProperty] private string _selectedTcType = "K";

    // ── Statistics ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _minValue = "---";
    [ObservableProperty] private string _maxValue = "---";
    [ObservableProperty] private string _avgValue = "---";
    [ObservableProperty] private int _measureCount = 0;

    private readonly List<double> _statsBuffer = new();
    private CancellationTokenSource? _continuousCts;
    private LiveDataWindow? _liveWindow;
    private readonly EventHandler _languageChangedHandler;

    // ── List sources ─────────────────────────────────────────────────────────
    public ObservableCollection<string> Functions { get; } = new()
    {
        "DCV", "ACV", "DCI", "ACI", "RES2W", "RES4W", "FREQ", "PERIOD", "DIODE", "CONT", "TEMP"
    };

    public ObservableCollection<string> Ranges { get; } = new()
    {
        "AUTO", "100m", "1", "10", "100", "1000"
    };

    public ObservableCollection<string> NplcValues { get; } = new()
    {
        "0.02", "0.2", "1", "10", "100"
    };

    public List<string> Intervals { get; } = new()
    {
        "100", "200", "500", "1000", "2000", "5000", "10000"
    };

    public ObservableCollection<string> TcTypes { get; } = new()
    {
        "J", "K", "T", "E", "N", "R", "S", "B"
    };

    // ── Constructor ──────────────────────────────────────────────────────────
    public Keithley2000FrontPanelViewModel(Keithley2000Driver driver)
    {
        _driver = driver;
        _isConnected = driver.IsConnected;
        _statusText = FpConnected(driver.IsConnected);

        driver.MeasurementReceived += OnMeasurementReceived;
        driver.StatusChanged += OnStatusChanged;
        driver.ErrorOccurred += OnErrorOccurred;

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

    // ── Event handlers ───────────────────────────────────────────────────────
    private void OnMeasurementReceived(object? sender, MeasurementResult result)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            DisplayValue = FormatValue(result.Value);
            DisplayUnit = result.Unit;
            DisplayFunction = result.Function;
            StatusText = $"OK  {DateTime.Now:HH:mm:ss.fff}";
        });
    }

    private void OnStatusChanged(object? sender, string status)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = status;
            IsConnected = _driver.IsConnected;
        });
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = T("FP_ErrGeneric", "Error: {0}", ex.Message);
            DisplayValue = "ERR";
        });
    }

    // ── Core measurement ─────────────────────────────────────────────────────
    private async Task<double?> PerformMeasurementAsync()
    {
        if (!_driver.IsConnected)
        {
            StatusText = T("FP_NotConnected", "Not connected");
            return null;
        }

        IsMeasuring = true;
        StatusText = T("FP_DMM_Measuring", "Measuring...");

        try
        {
            double nplc = double.TryParse(SelectedNplc,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;
            string range = MapRangeLabel(SelectedRange);

            double value = SelectedFunction switch
            {
                "DCV"    => await _driver.MeasureDCV(range, nplc),
                "ACV"    => await _driver.MeasureACV(range, nplc),
                "DCI"    => await _driver.MeasureDCI(range, nplc),
                "ACI"    => await _driver.MeasureACI(range, nplc),
                "RES2W"  => await _driver.MeasureResistance2W(range, nplc),
                "RES4W"  => await _driver.MeasureResistance4W(range, nplc),
                "FREQ"   => await _driver.MeasureFrequency(range, nplc),
                "PERIOD" => await _driver.MeasurePeriod(range, nplc),
                "DIODE"  => await _driver.MeasureDiode(),
                "CONT"   => await _driver.MeasureContinuity(),
                "TEMP"   => await _driver.MeasureTemperature(SelectedTcType, nplc),
                _        => await _driver.MeasureDCV(range, nplc),
            };

            DisplayValue = FormatValue(value);
            DisplayFunction = SelectedFunction;
            DisplayUnit = GetUnit(SelectedFunction);

            _statsBuffer.Add(value);
            MeasureCount = _statsBuffer.Count;
            MinValue = FormatValue(_statsBuffer.Min());
            MaxValue = FormatValue(_statsBuffer.Max());
            AvgValue = FormatValue(_statsBuffer.Average());

            // LIMIT check
            if (IsLimitEnabled && _driver.IsConnected)
            {
                try
                {
                    bool fail = await _driver.GetLimitFailAsync();
                    LimitStatus = fail ? "FAIL" : "PASS";
                    LimitPass   = !fail;
                }
                catch { /* ignore limit query errors during measurement */ }
            }

            StatusText = $"OK  {DateTime.Now:HH:mm:ss.fff}  [{MeasureCount}]";
            return value;
        }
        catch (Exception ex)
        {
            DisplayValue = "ERR";
            StatusText = T("FP_ErrGeneric", "Error: {0}", ex.Message);
            return null;
        }
        finally
        {
            IsMeasuring = false;
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task MeasureAsync() => await PerformMeasurementAsync();

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleContinuousAsync()
    {
        if (IsContinuous)
        {
            _continuousCts?.Cancel();
            return;
        }

        IsContinuous = true;
        _continuousCts = new CancellationTokenSource();
        var ct = _continuousCts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await PerformMeasurementAsync();
                int intervalMs = int.TryParse(SelectedInterval, out int iv) ? iv : 1000;
                try { await Task.Delay(intervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            IsContinuous = false;
            StatusText = T("FP_DMM_MeasureStopped", "Measurement stopped  [{0} samples]", MeasureCount);
        }
    }

    [RelayCommand]
    private async Task SetMathModeAsync(string mode)
    {
        SelectedMathMode = mode;
        IsMathActive = mode != "OFF";
        if (_driver.IsConnected)
        {
            try { await _driver.SetMathMode(mode); }
            catch (Exception ex) { StatusText = T("FP_DMM_ErrMath", "MATH error: {0}", ex.Message); }
        }
    }

    [RelayCommand]
    private async Task MathOffAsync()
    {
        SelectedMathMode = "OFF";
        IsMathActive = false;
        if (_driver.IsConnected)
        {
            try { await _driver.SetMathMode("OFF"); }
            catch (Exception ex) { StatusText = T("FP_DMM_ErrMath", "MATH error: {0}", ex.Message); }
        }
    }

    [RelayCommand]
    private async Task SendDisplayTextAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try { await _driver.DisplayText(DisplayMessage); }
        catch (Exception ex) { StatusText = T("FP_DMM_ErrDisp", "DISP error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task ClearDisplayAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            await _driver.ClearDisplay();
            DisplayMessage = "";
        }
        catch (Exception ex) { StatusText = T("FP_DMM_ErrDisp", "DISP error: {0}", ex.Message); }
    }

    // ── LCD on/off ────────────────────────────────────────────────────────────
    partial void OnIsDisplayEnabledChanged(bool value)
    {
        if (_driver.IsConnected)
            _ = ApplyDisplayEnabledAsync(value);
    }

    private async Task ApplyDisplayEnabledAsync(bool on)
    {
        try { await _driver.SetDisplayEnabled(on); }
        catch (Exception ex) { StatusText = T("FP_DMM_ErrDisp", "DISP error: {0}", ex.Message); }
    }

    // ── Auto-zero ─────────────────────────────────────────────────────────────
    partial void OnIsAutoZeroEnabledChanged(bool value)
    {
        if (_driver.IsConnected)
            _ = ApplyAutoZeroAsync(value);
    }

    private async Task ApplyAutoZeroAsync(bool on)
    {
        try { await _driver.SetAutoZero(on); }
        catch (Exception ex) { StatusText = T("FP_DMM_ErrZeroAuto", "ZERO:AUTO error: {0}", ex.Message); }
    }

    // ── Burst measurement ─────────────────────────────────────────────────────
    [RelayCommand]
    private async Task BurstMeasureAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        IsMeasuring = true;
        StatusText = T("FP_DMM_BurstRunning", "Burst {0} samples...", BurstCount);
        try
        {
            double[] values = await _driver.BurstMeasureAsync(BurstCount);
            BurstResultsText = string.Join(",  ", values.Select(v => FormatValue(v)));
            foreach (double v in values)
                _statsBuffer.Add(v);
            MeasureCount = _statsBuffer.Count;
            MinValue = FormatValue(_statsBuffer.Min());
            MaxValue = FormatValue(_statsBuffer.Max());
            AvgValue = FormatValue(_statsBuffer.Average());
            StatusText = T("FP_DMM_BurstOk", "Burst OK — {0} samples  {1}", values.Length, DateTime.Now.ToString("HH:mm:ss"));
        }
        catch (Exception ex) { StatusText = T("FP_DMM_ErrBurst", "Burst error: {0}", ex.Message); BurstResultsText = "ERR"; }
        finally { IsMeasuring = false; }
    }

    // ── LIMIT test ────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ToggleLimitTestAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            if (!IsLimitEnabled)
            {
                if (double.TryParse(LimitLow, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double low) &&
                    double.TryParse(LimitHigh, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double high))
                {
                    await _driver.SetLimitTestAsync(low, high);
                    IsLimitEnabled = true;
                    LimitStatus = "---";
                    StatusText = T("FP_DMM_LimitOnStatus", "LIMIT ON  [{0} … {1}]", low, high);
                }
                else StatusText = T("FP_DMM_InvalidLimits", "Invalid LIMIT thresholds");
            }
            else
            {
                await _driver.DisableLimitTestAsync();
                IsLimitEnabled = false;
                LimitStatus = "---";
                StatusText = T("FP_DMM_LimitOffStatus", "LIMIT OFF");
            }
        }
        catch (Exception ex) { StatusText = T("FP_DMM_ErrLimit", "LIMIT error: {0}", ex.Message); }
    }

    // ── Live window ───────────────────────────────────────────────────────────
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
    private void ClearStats()
    {
        _statsBuffer.Clear();
        MeasureCount = 0;
        MinValue = "---";
        MaxValue = "---";
        AvgValue = "---";
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_NotConnected", "Not connected"); return; }
        try
        {
            StatusText = T("FP_Resetting", "Reset...");
            await _driver.ResetAsync();
            DisplayValue = "---";
            DisplayUnit = "V";
            DisplayFunction = "DCV";
            SelectedFunction = "DCV";
            SelectedRange = "AUTO";
            SelectedNplc = "1";
            StatusText = T("FP_ResetDone", "Reset done");
        }
        catch (Exception ex) { StatusText = T("FP_ErrReset", "Reset error: {0}", ex.Message); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static string FormatValue(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return "OL";
        return Math.Abs(value) switch
        {
            >= 1e9  => $"{value / 1e9:F4} G",
            >= 1e6  => $"{value / 1e6:F4} M",
            >= 1e3  => $"{value / 1e3:F4} k",
            >= 1    => $"{value:F6}",
            >= 1e-3 => $"{value * 1e3:F4} m",
            >= 1e-6 => $"{value * 1e6:F4} µ",
            >= 1e-9 => $"{value * 1e9:F4} n",
            _       => $"{value:G6}",
        };
    }

    private static string MapRangeLabel(string label) => label switch
    {
        "100m" => "0.1", "1" => "1", "10" => "10", "100" => "100", "1000" => "1000", _ => "AUTO"
    };

    private static string GetUnit(string function) => function switch
    {
        "DCV"    => "V",
        "ACV"    => "V AC",
        "DCI"    => "A",
        "ACI"    => "A AC",
        "RES2W"  => "Ω",
        "RES4W"  => "Ω",
        "FREQ"   => "Hz",
        "PERIOD" => "s",
        "DIODE"  => "V",
        "CONT"   => "Ω",
        "TEMP"   => "°C",
        _        => "",
    };

    private static string FpConnected(bool connected) => connected
        ? T("FP_Connected", "Connected")
        : T("FP_NotConnected", "Not connected");

    private static string T(string key, string fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;

    private static string T(string key, string fallback, params object[] args) =>
        string.Format(T(key, fallback), args);
}
