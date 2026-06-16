using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Views;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace RTB2004.Views;

// ─── Preset model ────────────────────────────────────────────────────────────

public class RTB2004ChannelPreset
{
    public bool   Enabled  { get; set; }
    public string Scale    { get; set; } = "1";
    public string Offset   { get; set; } = "0.0";
    public string Coupling { get; set; } = "DC";
    public string Probe    { get; set; } = "10";
}

public class RTB2004MeasPreset
{
    public string Source { get; set; } = "CH1";
    public string Type   { get; set; } = "FREQ";
}

public class RTB2004Preset
{
    public RTB2004ChannelPreset[] Channels      { get; set; } = new RTB2004ChannelPreset[4];
    public string                 Timebase      { get; set; } = "0.001";
    public string                 TriggerSource { get; set; } = "CH1";
    public string                 TriggerLevel  { get; set; } = "0.0";
    public string                 TriggerSlope  { get; set; } = "POS";
    public string                 TriggerMode   { get; set; } = "AUTO";
    public RTB2004MeasPreset[]    Measurements  { get; set; } = new RTB2004MeasPreset[4];
}

// ─── ViewModel ───────────────────────────────────────────────────────────────

public partial class RTB2004FrontPanelViewModel : ObservableObject
{
    private readonly RTB2004Driver _driver;
    private          PlotModel     _scopeModel = null!;
    public           PlotModel     ScopeModel  => _scopeModel;

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _statusText       = "Brak połączenia";
    [ObservableProperty] private string _acquisitionState = "STOP";
    [ObservableProperty] private bool   _isCapturing;

    // ── Status bar display ─────────────────────────────────────────────────
    [ObservableProperty] private string _ch1Label        = "CH1 ■";
    [ObservableProperty] private string _ch2Label        = "CH2 □";
    [ObservableProperty] private string _ch3Label        = "CH3 □";
    [ObservableProperty] private string _ch4Label        = "CH4 □";
    [ObservableProperty] private string _timescaleDisplay = "1ms/dz";

    // ── Channel settings ───────────────────────────────────────────────────
    [ObservableProperty] private bool   _ch1Enabled  = true;
    [ObservableProperty] private string _ch1Scale    = "1";
    [ObservableProperty] private string _ch1Offset   = "0.0";
    [ObservableProperty] private string _ch1Coupling = "DC";
    [ObservableProperty] private string _ch1Probe    = "10";

    [ObservableProperty] private bool   _ch2Enabled  = false;
    [ObservableProperty] private string _ch2Scale    = "1";
    [ObservableProperty] private string _ch2Offset   = "0.0";
    [ObservableProperty] private string _ch2Coupling = "DC";
    [ObservableProperty] private string _ch2Probe    = "10";

    [ObservableProperty] private bool   _ch3Enabled  = false;
    [ObservableProperty] private string _ch3Scale    = "1";
    [ObservableProperty] private string _ch3Offset   = "0.0";
    [ObservableProperty] private string _ch3Coupling = "DC";
    [ObservableProperty] private string _ch3Probe    = "10";

    [ObservableProperty] private bool   _ch4Enabled  = false;
    [ObservableProperty] private string _ch4Scale    = "1";
    [ObservableProperty] private string _ch4Offset   = "0.0";
    [ObservableProperty] private string _ch4Coupling = "DC";
    [ObservableProperty] private string _ch4Probe    = "10";

    // ── Timebase ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedTimescale = "0.001";

    // ── Trigger ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _triggerSource = "CH1";
    [ObservableProperty] private string _triggerLevel  = "0.0";
    [ObservableProperty] private string _triggerSlope  = "POS";
    [ObservableProperty] private string _triggerMode   = "AUTO";

    // ── Measurement slots ──────────────────────────────────────────────────
    [ObservableProperty] private string _m1Source = "CH1";
    [ObservableProperty] private string _m1Type   = "FREQ";
    [ObservableProperty] private string _m1Result = "---";
    [ObservableProperty] private string _m1Unit   = "Hz";

    [ObservableProperty] private string _m2Source = "CH1";
    [ObservableProperty] private string _m2Type   = "AMPL";
    [ObservableProperty] private string _m2Result = "---";
    [ObservableProperty] private string _m2Unit   = "V";

    [ObservableProperty] private string _m3Source = "CH1";
    [ObservableProperty] private string _m3Type   = "RMS";
    [ObservableProperty] private string _m3Result = "---";
    [ObservableProperty] private string _m3Unit   = "V";

    [ObservableProperty] private string _m4Source = "CH1";
    [ObservableProperty] private string _m4Type   = "MEAN";
    [ObservableProperty] private string _m4Result = "---";
    [ObservableProperty] private string _m4Unit   = "V";

    // ── Option lists ───────────────────────────────────────────────────────
    public List<string> ScaleOptions { get; } = new()
    {
        "0.001","0.002","0.005","0.01","0.02","0.05",
        "0.1","0.2","0.5","1","2","5","10"
    };

    public List<string> CouplingOptions { get; } = new() { "DC", "AC", "GND" };
    public List<string> ProbeOptions    { get; } = new() { "1", "10", "100" };

    public List<string> TimebaseOptions { get; } = new()
    {
        "1E-9","2E-9","5E-9","1E-8","2E-8","5E-8",
        "1E-7","2E-7","5E-7","1E-6","2E-6","5E-6",
        "1E-5","2E-5","5E-5","0.0001","0.0002","0.0005",
        "0.001","0.002","0.005","0.01","0.02","0.05",
        "0.1","0.2","0.5","1","2","5","10","20","50"
    };

    public List<string> TriggerSources { get; } = new() { "CH1","CH2","CH3","CH4","EXT" };
    public List<string> TriggerSlopes  { get; } = new() { "POS","NEG","EITH" };
    public List<string> TriggerModes   { get; } = new() { "AUTO","NORM" };
    public List<string> ChannelSources { get; } = new() { "CH1","CH2","CH3","CH4" };

    public List<string> MeasTypes { get; } = new()
    {
        "FREQ","PERI","AMPL","RMS","MEAN","PK2PK",
        "PHAS","DEL","CRIS","FFAL","PWID","NWID","DCYC"
    };

    private LiveDataWindow? _liveWindow;

    public RTB2004FrontPanelViewModel(RTB2004Driver driver)
    {
        _driver      = driver;
        _isConnected = driver.IsConnected;
        _statusText  = driver.IsConnected ? "Połączono" : "Brak połączenia";

        driver.StatusChanged += OnStatusChanged;
        driver.ErrorOccurred += OnErrorOccurred;

        InitializeScopeModel();
    }

    // ── Oscilloscope display model ─────────────────────────────────────────
    private void InitializeScopeModel()
    {
        _scopeModel = new PlotModel
        {
            Background              = OxyColors.Transparent,
            PlotAreaBackground      = OxyColor.FromRgb(4, 7, 18),
            PlotAreaBorderColor     = OxyColor.FromRgb(38, 58, 100),
            PlotAreaBorderThickness = new OxyThickness(1),
            Padding                 = new OxyThickness(4, 4, 4, 4),
            IsLegendVisible         = false,
        };

        _scopeModel.Axes.Add(new LinearAxis
        {
            Position           = AxisPosition.Bottom,
            AxislineStyle      = LineStyle.Solid,
            AxislineColor      = OxyColor.FromRgb(48, 70, 115),
            TicklineColor      = OxyColor.FromRgb(48, 70, 115),
            TextColor          = OxyColor.FromRgb(120, 155, 205),
            FontSize           = 8,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(26, 40, 68),
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.FromRgb(16, 24, 42),
            MajorStep          = 0.5,
            MinorStep          = 0.1,
            Minimum            = -2.5,
            Maximum            =  2.5,
            StringFormat       = "0.##",
        });

        _scopeModel.Axes.Add(new LinearAxis
        {
            Position           = AxisPosition.Left,
            AxislineStyle      = LineStyle.Solid,
            AxislineColor      = OxyColor.FromRgb(48, 70, 115),
            TicklineColor      = OxyColor.FromRgb(48, 70, 115),
            TextColor          = OxyColor.FromRgb(120, 155, 205),
            FontSize           = 8,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(26, 40, 68),
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.FromRgb(16, 24, 42),
            MajorStep          = 1.0,
            MinorStep          = 0.2,
            Minimum            = -4.0,
            Maximum            =  4.0,
            StringFormat       = "0.##",
        });

        OxyColor[] chColors =
        {
            OxyColor.FromRgb(255, 255,   0),  // CH1 yellow
            OxyColor.FromRgb(  0, 255, 255),  // CH2 cyan
            OxyColor.FromRgb(255, 102, 255),  // CH3 magenta
            OxyColor.FromRgb(102, 255, 102),  // CH4 green
        };

        for (int i = 0; i < 4; i++)
            _scopeModel.Series.Add(new LineSeries
            {
                Color           = chColors[i],
                StrokeThickness = 1.2,
                IsVisible       = false,
                Title           = $"CH{i + 1}",
            });

        OnPropertyChanged(nameof(ScopeModel));
    }

    // ── Event handlers ─────────────────────────────────────────────────────
    private void OnStatusChanged(object? sender, string status)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText  = status;
            IsConnected = _driver.IsConnected;
        });
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = $"BŁĄD: {ex.Message}";
        });
    }

    // ── Property change reactions ──────────────────────────────────────────
    partial void OnSelectedTimescaleChanged(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
            TimescaleDisplay = FormatTime(s) + "/dz";
    }

    partial void OnCh1EnabledChanged(bool value) => Ch1Label = value ? "CH1 ■" : "CH1 □";
    partial void OnCh2EnabledChanged(bool value) => Ch2Label = value ? "CH2 ■" : "CH2 □";
    partial void OnCh3EnabledChanged(bool value) => Ch3Label = value ? "CH3 ■" : "CH3 □";
    partial void OnCh4EnabledChanged(bool value) => Ch4Label = value ? "CH4 ■" : "CH4 □";

    partial void OnM1TypeChanged(string value) => M1Unit = GetUnit(value);
    partial void OnM2TypeChanged(string value) => M2Unit = GetUnit(value);
    partial void OnM3TypeChanged(string value) => M3Unit = GetUnit(value);
    partial void OnM4TypeChanged(string value) => M4Unit = GetUnit(value);

    // ── Commands ───────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RunAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try { await _driver.RunAsync(); AcquisitionState = "RUN"; }
        catch (Exception ex) { StatusText = $"Błąd RUN: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try { await _driver.StopAsync(); AcquisitionState = "STOP"; }
        catch (Exception ex) { StatusText = $"Błąd STOP: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SingleAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            AcquisitionState = "SINGLE";
            await _driver.SingleAsync();
            StatusText = "Single akwizycja — oczekiwanie na wyzwalanie";
        }
        catch (Exception ex) { StatusText = $"Błąd SINGLE: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task AutoscaleAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            StatusText = "Autoskalowanie...";
            await _driver.AutoscaleAsync();
            StatusText = $"Autoskalowanie OK  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { StatusText = $"Błąd AUTOSCALE: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyChannelsAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            await ApplyChannelAsync(1, Ch1Enabled, Ch1Scale, Ch1Offset, Ch1Coupling, Ch1Probe);
            await ApplyChannelAsync(2, Ch2Enabled, Ch2Scale, Ch2Offset, Ch2Coupling, Ch2Probe);
            await ApplyChannelAsync(3, Ch3Enabled, Ch3Scale, Ch3Offset, Ch3Coupling, Ch3Probe);
            await ApplyChannelAsync(4, Ch4Enabled, Ch4Scale, Ch4Offset, Ch4Coupling, Ch4Probe);
            StatusText = $"Kanały zastosowane  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { StatusText = $"Błąd ApplyChannels: {ex.Message}"; }
    }

    private async Task ApplyChannelAsync(int ch, bool enabled, string scale, string offset,
                                          string coupling, string probe)
    {
        double sc  = double.TryParse(scale,  NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : 1.0;
        double off = double.TryParse(offset, NumberStyles.Float, CultureInfo.InvariantCulture, out double o) ? o : 0.0;
        double pr  = double.TryParse(probe,  NumberStyles.Float, CultureInfo.InvariantCulture, out double p) ? p : 10.0;

        await _driver.SetChannelEnabledAsync(ch, enabled);
        await _driver.SetChannelScaleAsync(ch, sc);
        await _driver.SetChannelOffsetAsync(ch, off);
        await _driver.SetChannelCouplingAsync(ch, coupling);
        await _driver.SetChannelProbeAsync(ch, pr);
    }

    [RelayCommand]
    private async Task ApplyTimescaleAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(SelectedTimescale, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double secs))
        {
            StatusText = "Nieprawidłowa podstawa czasu";
            return;
        }
        try
        {
            await _driver.SetTimescaleAsync(secs);
            TimescaleDisplay = FormatTime(secs) + "/dz";
            StatusText = $"Podstawa czasu: {FormatTime(secs)}/dz";
        }
        catch (Exception ex) { StatusText = $"Błąd Timescale: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyTriggerAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        if (!double.TryParse(TriggerLevel, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double level))
        {
            StatusText = "Nieprawidłowy poziom wyzwalania";
            return;
        }
        int chNum = TriggerSource.StartsWith("CH") && int.TryParse(TriggerSource[2..], out int n) ? n : 1;
        try
        {
            await _driver.SetTriggerSourceAsync(TriggerSource);
            await _driver.SetTriggerLevelAsync(chNum, level);
            await _driver.SetTriggerSlopeAsync(TriggerSlope);
            await _driver.SetTriggerModeAsync(TriggerMode);
            StatusText = $"Wyzwalanie: {TriggerSource}  {level:F3}V  {TriggerSlope}  {TriggerMode}";
        }
        catch (Exception ex) { StatusText = $"Błąd Trigger: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task UpdateMeasurementsAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        try
        {
            (M1Result, M1Unit) = await ReadMeasAsync(1, M1Source, M1Type);
            (M2Result, M2Unit) = await ReadMeasAsync(2, M2Source, M2Type);
            (M3Result, M3Unit) = await ReadMeasAsync(3, M3Source, M3Type);
            (M4Result, M4Unit) = await ReadMeasAsync(4, M4Source, M4Type);
            StatusText = $"Pomiary zaktualizowane  {DateTime.Now:HH:mm:ss.fff}";
        }
        catch (Exception ex) { StatusText = $"Błąd pomiarów: {ex.Message}"; }
    }

    private async Task<(string Result, string Unit)> ReadMeasAsync(int slot, string source, string type)
    {
        try
        {
            int chNum = source.StartsWith("CH") && int.TryParse(source[2..], out int n) ? n : 1;
            await _driver.SetMeasurementAsync(slot, chNum, type);
            await Task.Delay(80);
            double val = await _driver.GetMeasurementResultAsync(slot);
            return (FormatValue(val, type), GetUnit(type));
        }
        catch { return ("ERR", ""); }
    }

    [RelayCommand]
    private async Task CaptureWaveformAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        IsCapturing = true;
        try
        {
            StatusText = "Odczyt przebiegów...";
            bool[] enabled = { Ch1Enabled, Ch2Enabled, Ch3Enabled, Ch4Enabled };
            bool anyRead = false;

            for (int i = 0; i < 4; i++)
            {
                var series = (LineSeries)_scopeModel.Series[i];
                if (!enabled[i]) { series.IsVisible = false; continue; }

                var (voltages, xStart, xInc) = await _driver.ReadWaveformAsync(i + 1);
                series.Points.Clear();
                int stride = Math.Max(1, voltages.Length / 5000);
                for (int j = 0; j < voltages.Length; j += stride)
                    series.Points.Add(new DataPoint(xStart + j * xInc, voltages[j]));
                series.IsVisible = true;
                anyRead = true;
            }

            if (anyRead) _scopeModel.ResetAllAxes();
            _scopeModel.InvalidatePlot(true);
            StatusText = $"Przebiegi wczytane  {DateTime.Now:HH:mm:ss.fff}";
        }
        catch (Exception ex) { StatusText = $"Błąd odczytu przebiegu: {ex.Message}"; }
        finally { IsCapturing = false; }
    }

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Nie połączono"; return; }
        var dlg = new SaveFileDialog
        {
            Title      = "Zapisz screenshot oscyloskopu",
            Filter     = "PNG (*.png)|*.png|BMP (*.bmp)|*.bmp|Wszystkie pliki (*.*)|*.*",
            DefaultExt = ".png",
            FileName   = $"RTB2004_{DateTime.Now:yyyyMMdd_HHmmss}",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            StatusText = "Pobieranie screenshota...";
            byte[] data = await _driver.TakeScreenshotAsync();
            if (data.Length == 0) { StatusText = "Screenshot: brak danych (tryb symulacji?)"; return; }
            await File.WriteAllBytesAsync(dlg.FileName, data);
            StatusText = $"Screenshot zapisany: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex) { StatusText = $"Błąd screenshot: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Zapisz preset oscyloskopu",
            Filter     = "Preset JSON (*.json)|*.json",
            DefaultExt = ".json",
            FileName   = $"RTB2004_preset_{DateTime.Now:yyyyMMdd_HHmmss}",
        };
        if (dlg.ShowDialog() != true) return;
        var preset = BuildPreset();
        string json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dlg.FileName, json);
        StatusText = $"Preset zapisany: {Path.GetFileName(dlg.FileName)}";
    }

    [RelayCommand]
    private async Task LoadPresetAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Wczytaj preset oscyloskopu",
            Filter = "Preset JSON (*.json)|*.json|Wszystkie pliki (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            string json = await File.ReadAllTextAsync(dlg.FileName);
            var preset  = JsonSerializer.Deserialize<RTB2004Preset>(json);
            if (preset == null) { StatusText = "Błąd: nieprawidłowy plik presetu"; return; }
            ApplyPreset(preset);
            StatusText = $"Preset wczytany: {Path.GetFileName(dlg.FileName)}";
            if (_driver.IsConnected)
            {
                await ApplyChannelsAsync();
                await ApplyTimescaleAsync();
                await ApplyTriggerAsync();
            }
        }
        catch (Exception ex) { StatusText = $"Błąd wczytywania presetu: {ex.Message}"; }
    }

    private RTB2004Preset BuildPreset() => new()
    {
        Channels = new[]
        {
            new RTB2004ChannelPreset { Enabled = Ch1Enabled, Scale = Ch1Scale, Offset = Ch1Offset, Coupling = Ch1Coupling, Probe = Ch1Probe },
            new RTB2004ChannelPreset { Enabled = Ch2Enabled, Scale = Ch2Scale, Offset = Ch2Offset, Coupling = Ch2Coupling, Probe = Ch2Probe },
            new RTB2004ChannelPreset { Enabled = Ch3Enabled, Scale = Ch3Scale, Offset = Ch3Offset, Coupling = Ch3Coupling, Probe = Ch3Probe },
            new RTB2004ChannelPreset { Enabled = Ch4Enabled, Scale = Ch4Scale, Offset = Ch4Offset, Coupling = Ch4Coupling, Probe = Ch4Probe },
        },
        Timebase      = SelectedTimescale,
        TriggerSource = TriggerSource,
        TriggerLevel  = TriggerLevel,
        TriggerSlope  = TriggerSlope,
        TriggerMode   = TriggerMode,
        Measurements  = new[]
        {
            new RTB2004MeasPreset { Source = M1Source, Type = M1Type },
            new RTB2004MeasPreset { Source = M2Source, Type = M2Type },
            new RTB2004MeasPreset { Source = M3Source, Type = M3Type },
            new RTB2004MeasPreset { Source = M4Source, Type = M4Type },
        },
    };

    private void ApplyPreset(RTB2004Preset p)
    {
        if (p.Channels?.Length >= 1) { var c = p.Channels[0]; Ch1Enabled = c.Enabled; Ch1Scale = c.Scale; Ch1Offset = c.Offset; Ch1Coupling = c.Coupling; Ch1Probe = c.Probe; }
        if (p.Channels?.Length >= 2) { var c = p.Channels[1]; Ch2Enabled = c.Enabled; Ch2Scale = c.Scale; Ch2Offset = c.Offset; Ch2Coupling = c.Coupling; Ch2Probe = c.Probe; }
        if (p.Channels?.Length >= 3) { var c = p.Channels[2]; Ch3Enabled = c.Enabled; Ch3Scale = c.Scale; Ch3Offset = c.Offset; Ch3Coupling = c.Coupling; Ch3Probe = c.Probe; }
        if (p.Channels?.Length >= 4) { var c = p.Channels[3]; Ch4Enabled = c.Enabled; Ch4Scale = c.Scale; Ch4Offset = c.Offset; Ch4Coupling = c.Coupling; Ch4Probe = c.Probe; }
        SelectedTimescale = p.Timebase      ?? SelectedTimescale;
        TriggerSource     = p.TriggerSource ?? TriggerSource;
        TriggerLevel      = p.TriggerLevel  ?? TriggerLevel;
        TriggerSlope      = p.TriggerSlope  ?? TriggerSlope;
        TriggerMode       = p.TriggerMode   ?? TriggerMode;
        if (p.Measurements?.Length >= 1) { M1Source = p.Measurements[0].Source; M1Type = p.Measurements[0].Type; }
        if (p.Measurements?.Length >= 2) { M2Source = p.Measurements[1].Source; M2Type = p.Measurements[1].Type; }
        if (p.Measurements?.Length >= 3) { M3Source = p.Measurements[2].Source; M3Type = p.Measurements[2].Type; }
        if (p.Measurements?.Length >= 4) { M4Source = p.Measurements[3].Source; M4Type = p.Measurements[3].Type; }
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
            AcquisitionState = "STOP";
            M1Result = M2Result = M3Result = M4Result = "---";
            StatusText = "Reset wykonany";
        }
        catch (Exception ex) { StatusText = $"Błąd resetu: {ex.Message}"; }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string FormatValue(double v, string type)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "OL";
        string unit = GetUnit(type);
        return unit switch
        {
            "Hz" => v >= 1e6 ? $"{v/1e6:F4} M" : v >= 1e3 ? $"{v/1e3:F3} k" : $"{v:F4}",
            "s"  => v >= 1e-3 ? $"{v*1e3:F4} m" : v >= 1e-6 ? $"{v*1e6:F3} µ" : $"{v:G5}",
            "%"  => $"{v:F2}",
            "°"  => $"{v:F2}",
            _    => Math.Abs(v) >= 1e3 ? $"{v/1e3:F4} k"
                 : Math.Abs(v) >= 1    ? $"{v:F6}"
                 : Math.Abs(v) >= 1e-3 ? $"{v*1e3:F4} m"
                 : $"{v:G5}",
        };
    }

    private static string GetUnit(string type) => type.ToUpperInvariant() switch
    {
        "FREQ"  => "Hz",
        "PERI"  => "s",
        "RMS"   => "V",
        "MEAN"  => "V",
        "AMPL"  => "V",
        "PK2PK" => "V",
        "PHAS"  => "°",
        "DEL"   => "s",
        "CRIS"  => "s",
        "FFAL"  => "s",
        "PWID"  => "s",
        "NWID"  => "s",
        "DCYC"  => "%",
        _       => "",
    };

    private static string FormatTime(double secs) =>
        secs >= 1    ? $"{secs:G3} s"
      : secs >= 1e-3 ? $"{secs*1e3:G3} ms"
      : secs >= 1e-6 ? $"{secs*1e6:G3} µs"
                     : $"{secs*1e9:G3} ns";
}
