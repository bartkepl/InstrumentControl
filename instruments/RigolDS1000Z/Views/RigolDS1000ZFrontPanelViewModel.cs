using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Services;
using InstrumentControl.Core.Views;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace RigolDS1000Z.Views;

// ── Preset model ─────────────────────────────────────────────────────────────

public class RigolChannelPreset
{
    public bool   Enabled  { get; set; } = true;
    public string Scale    { get; set; } = "1";
    public string Offset   { get; set; } = "0";
    public string Coupling { get; set; } = "DC";
    public string Probe    { get; set; } = "10";
    public string BWLimit  { get; set; } = "OFF";
    public bool   Invert   { get; set; }
}

public class RigolPreset
{
    public RigolChannelPreset[] Channels      { get; set; } = new RigolChannelPreset[4];
    public string               Timebase      { get; set; } = "0.001";
    public string               TriggerSource { get; set; } = "CH1";
    public string               TriggerLevel  { get; set; } = "0";
    public string               TriggerSlope  { get; set; } = "POSitive";
    public string               TriggerSweep  { get; set; } = "AUTO";
    public string               AcqType       { get; set; } = "NORMal";
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public partial class RigolDS1000ZFrontPanelViewModel : ObservableObject
{
    private readonly RigolDS1000ZDriver _driver;
    private          PlotModel          _scopeModel = null!;
    public           PlotModel          ScopeModel  => _scopeModel;

    // ── State ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _statusText       = "Brak połączenia";
    [ObservableProperty] private string _acquisitionState = "STOP";
    [ObservableProperty] private bool   _isCapturing;

    // ── Channel labels ───────────────────────────────────────────────────────
    [ObservableProperty] private string _ch1Label = "CH1 ■";
    [ObservableProperty] private string _ch2Label = "CH2 □";
    [ObservableProperty] private string _ch3Label = "CH3 □";
    [ObservableProperty] private string _ch4Label = "CH4 □";
    [ObservableProperty] private string _timescaleDisplay = "1ms/dz";

    // ── Channel 1 ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _ch1Enabled  = true;
    [ObservableProperty] private string _ch1Scale    = "1";
    [ObservableProperty] private string _ch1Offset   = "0";
    [ObservableProperty] private string _ch1Coupling = "DC";
    [ObservableProperty] private string _ch1Probe    = "10";
    [ObservableProperty] private string _ch1BwLimit  = "OFF";
    [ObservableProperty] private bool   _ch1Invert;

    // ── Channel 2 ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _ch2Enabled;
    [ObservableProperty] private string _ch2Scale    = "1";
    [ObservableProperty] private string _ch2Offset   = "0";
    [ObservableProperty] private string _ch2Coupling = "DC";
    [ObservableProperty] private string _ch2Probe    = "10";
    [ObservableProperty] private string _ch2BwLimit  = "OFF";
    [ObservableProperty] private bool   _ch2Invert;

    // ── Channel 3 ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _ch3Enabled;
    [ObservableProperty] private string _ch3Scale    = "1";
    [ObservableProperty] private string _ch3Offset   = "0";
    [ObservableProperty] private string _ch3Coupling = "DC";
    [ObservableProperty] private string _ch3Probe    = "10";
    [ObservableProperty] private string _ch3BwLimit  = "OFF";
    [ObservableProperty] private bool   _ch3Invert;

    // ── Channel 4 ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _ch4Enabled;
    [ObservableProperty] private string _ch4Scale    = "1";
    [ObservableProperty] private string _ch4Offset   = "0";
    [ObservableProperty] private string _ch4Coupling = "DC";
    [ObservableProperty] private string _ch4Probe    = "10";
    [ObservableProperty] private string _ch4BwLimit  = "OFF";
    [ObservableProperty] private bool   _ch4Invert;

    // ── Timebase ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedTimescale = "0.001";
    [ObservableProperty] private string _timebaseOffset    = "0";
    [ObservableProperty] private string _timebaseMode      = "MAIN";

    // ── Trigger ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _triggerMode   = "EDGE";
    [ObservableProperty] private string _triggerSource = "CH1";
    [ObservableProperty] private string _triggerSlope  = "POSitive";
    [ObservableProperty] private string _triggerLevel  = "0";
    [ObservableProperty] private string _triggerSweep  = "AUTO";

    // ── Acquire ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _acquireType = "NORMal";
    [ObservableProperty] private string _acquireAvg  = "16";
    [ObservableProperty] private string _acquireMem  = "AUTO";

    // ── Measurements (4 slots) ───────────────────────────────────────────────
    [ObservableProperty] private string _m1Source = "CH1";
    [ObservableProperty] private string _m1Type   = "FREQuency";
    [ObservableProperty] private string _m1Result = "---";
    [ObservableProperty] private string _m1Unit   = "Hz";

    [ObservableProperty] private string _m2Source = "CH1";
    [ObservableProperty] private string _m2Type   = "VPP";
    [ObservableProperty] private string _m2Result = "---";
    [ObservableProperty] private string _m2Unit   = "V";

    [ObservableProperty] private string _m3Source = "CH1";
    [ObservableProperty] private string _m3Type   = "VRMS";
    [ObservableProperty] private string _m3Result = "---";
    [ObservableProperty] private string _m3Unit   = "V";

    [ObservableProperty] private string _m4Source = "CH1";
    [ObservableProperty] private string _m4Type   = "PDUTycycle";
    [ObservableProperty] private string _m4Result = "---";
    [ObservableProperty] private string _m4Unit   = "%";

    // ── Cursor readout ────────────────────────────────────────────────────────
    [ObservableProperty] private string _cursorAX = "---";
    [ObservableProperty] private string _cursorBX = "---";
    [ObservableProperty] private string _cursorDX = "---";
    [ObservableProperty] private string _cursorAY = "---";
    [ObservableProperty] private string _cursorBY = "---";
    [ObservableProperty] private string _cursorDY = "---";

    // ── Sample rate readout ───────────────────────────────────────────────────
    [ObservableProperty] private string _sampleRateText = "---";
    [ObservableProperty] private string _trigStatusText = "---";

    // ── Option lists ─────────────────────────────────────────────────────────
    public List<string> VScaleOptions    { get; } = new()
        { "0.001","0.002","0.005","0.01","0.02","0.05","0.1","0.2","0.5","1","2","5","10","20","50","100" };
    public List<string> TScaleOptions    { get; } = new()
        { "5E-9","1E-8","2E-8","5E-8","1E-7","2E-7","5E-7","1E-6","2E-6","5E-6",
          "1E-5","2E-5","5E-5","1E-4","2E-4","5E-4","0.001","0.002","0.005",
          "0.01","0.02","0.05","0.1","0.2","0.5","1","2","5","10","20","50" };
    public List<string> CouplingOptions  { get; } = new() { "DC", "AC", "GND" };
    public List<string> ProbeOptions     { get; } = new()
        { "0.1","0.2","0.5","1","2","5","10","20","50","100","200","500","1000" };
    public List<string> BWLimitOptions   { get; } = new() { "OFF", "20M" };
    public List<string> TbModeOptions    { get; } = new() { "MAIN", "XY", "ROLL" };
    public List<string> TrigModeOptions  { get; } = new() { "EDGE","PULSe","SLOPe","VIDeo","PATTern","RS232","I2C","SPI" };
    public List<string> TrigSweepOptions { get; } = new() { "AUTO", "NORMal", "SINGle" };
    public List<string> TrigSrcOptions   { get; } = new() { "CH1","CH2","CH3","CH4","AC" };
    public List<string> TrigSlopeOptions { get; } = new() { "POSitive","NEGative","RFALl" };
    public List<string> AcqTypeOptions   { get; } = new() { "NORMal","AVERages","PEAKdetect","HRESolution" };
    public List<string> AcqAvgOptions    { get; } = new() { "2","4","8","16","32","64","128","256","512","1024" };
    public List<string> MemDepthOptions  { get; } = new() { "AUTO","12000","120000","1200000","12000000","24000000" };
    public List<string> ChannelSources   { get; } = new() { "CH1","CH2","CH3","CH4" };
    public List<string> MeasTypes        { get; } = new()
        { "FREQuency","PERiod","VMAX","VMIN","VPP","VTOP","VBASe","VAMP","VAVG","VRMS",
          "OVERshoot","PREShoot","RISetime","FALLtime","PWIDth","NWIDth","PDUTycycle","NDUTycycle" };

    private LiveDataWindow? _liveWindow;

    public RigolDS1000ZFrontPanelViewModel(RigolDS1000ZDriver driver)
    {
        _driver      = driver;
        _isConnected = driver.IsConnected;
        _statusText  = driver.IsConnected ? "Połączono" : "Brak połączenia";

        driver.StatusChanged += (_, s) =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { StatusText = s; IsConnected = _driver.IsConnected; });
        driver.ErrorOccurred += (_, ex) =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusText = $"BŁĄD: {ex.Message}");

        AppLocalization.LanguageChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                StatusText = _driver.IsConnected ? "Połączono" : "Brak połączenia");

        InitializeScopeModel();
    }

    // ── OxyPlot scope display ─────────────────────────────────────────────────

    private void InitializeScopeModel()
    {
        _scopeModel = new PlotModel
        {
            Background          = OxyColors.Transparent,
            PlotAreaBackground  = OxyColor.FromRgb(6, 8, 20),
            PlotAreaBorderColor = OxyColor.FromRgb(40, 55, 80),
            PlotAreaBorderThickness = new OxyThickness(1),
            Padding             = new OxyThickness(4),
            IsLegendVisible     = false,
        };

        OxyColor gridMaj = OxyColor.FromRgb(28, 40, 60);
        OxyColor gridMin = OxyColor.FromRgb(16, 24, 38);
        OxyColor axLine  = OxyColor.FromRgb(50, 68, 100);
        OxyColor axText  = OxyColor.FromRgb(110, 145, 195);

        _scopeModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            AxislineStyle = LineStyle.Solid, AxislineColor = axLine,
            TicklineColor = axLine, TextColor = axText, FontSize = 8,
            MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = gridMaj,
            MinorGridlineStyle = LineStyle.Dot,   MinorGridlineColor = gridMin,
            MajorStep = 0.5, MinorStep = 0.1, Minimum = -2.5, Maximum = 2.5,
            StringFormat = "0.##",
        });

        _scopeModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            AxislineStyle = LineStyle.Solid, AxislineColor = axLine,
            TicklineColor = axLine, TextColor = axText, FontSize = 8,
            MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = gridMaj,
            MinorGridlineStyle = LineStyle.Dot,   MinorGridlineColor = gridMin,
            MajorStep = 1.0, MinorStep = 0.2, Minimum = -4.0, Maximum = 4.0,
            StringFormat = "0.##",
        });

        OxyColor[] chColors =
        {
            OxyColor.FromRgb(255, 220,   0),  // CH1 yellow
            OxyColor.FromRgb(  0, 200, 230),  // CH2 cyan
            OxyColor.FromRgb(255,   0, 200),  // CH3 magenta
            OxyColor.FromRgb( 60, 230,  60),  // CH4 green
        };

        for (int i = 0; i < 4; i++)
            _scopeModel.Series.Add(new LineSeries
            {
                Color           = chColors[i],
                StrokeThickness = 1.3,
                IsVisible       = false,
                Title           = $"CH{i + 1}",
            });

        OnPropertyChanged(nameof(ScopeModel));
    }

    // ── Property change reactions ─────────────────────────────────────────────

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

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RunAsync()
    {
        if (!CheckConn()) return;
        try { await _driver.RunAsync(); AcquisitionState = "RUN"; }
        catch (Exception ex) { StatusText = $"Błąd RUN: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!CheckConn()) return;
        try { await _driver.StopAsync(); AcquisitionState = "STOP"; }
        catch (Exception ex) { StatusText = $"Błąd STOP: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SingleAsync()
    {
        if (!CheckConn()) return;
        try { AcquisitionState = "SINGLE"; await _driver.SingleAsync(); StatusText = "SINGLE — oczekiwanie na wyzwalanie"; }
        catch (Exception ex) { StatusText = $"Błąd SINGLE: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task AutoScaleAsync()
    {
        if (!CheckConn()) return;
        try { StatusText = "Auto Scale..."; await _driver.AutoScaleAsync(); StatusText = $"Auto Scale OK  {Now()}"; }
        catch (Exception ex) { StatusText = $"Błąd AutoScale: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyChannelsAsync()
    {
        if (!CheckConn()) return;
        try
        {
            await ApplyCh(1, Ch1Enabled, Ch1Scale, Ch1Offset, Ch1Coupling, Ch1Probe, Ch1BwLimit, Ch1Invert);
            await ApplyCh(2, Ch2Enabled, Ch2Scale, Ch2Offset, Ch2Coupling, Ch2Probe, Ch2BwLimit, Ch2Invert);
            await ApplyCh(3, Ch3Enabled, Ch3Scale, Ch3Offset, Ch3Coupling, Ch3Probe, Ch3BwLimit, Ch3Invert);
            await ApplyCh(4, Ch4Enabled, Ch4Scale, Ch4Offset, Ch4Coupling, Ch4Probe, Ch4BwLimit, Ch4Invert);
            StatusText = $"Kanały zastosowane  {Now()}";
        }
        catch (Exception ex) { StatusText = $"Błąd ApplyChannels: {ex.Message}"; }
    }

    private async Task ApplyCh(int ch, bool en, string scale, string offset,
                                string coupling, string probe, string bwl, bool inv)
    {
        double sc  = D(scale,  1.0);
        double off = D(offset, 0.0);
        double pr  = D(probe,  10.0);
        await _driver.SetChannelDisplayAsync(ch, en);
        await _driver.SetChannelScaleAsync(ch, sc);
        await _driver.SetChannelOffsetAsync(ch, off);
        await _driver.SetChannelCouplingAsync(ch, coupling);
        await _driver.SetChannelProbeAsync(ch, pr);
        await _driver.SetChannelBandwidthAsync(ch, bwl);
        await _driver.SetChannelInvertAsync(ch, inv);
    }

    [RelayCommand]
    private async Task ApplyTimebaseAsync()
    {
        if (!CheckConn()) return;
        try
        {
            double secs = D(SelectedTimescale, 0.001);
            double off  = D(TimebaseOffset, 0.0);
            await _driver.SetTimebaseModeAsync(TimebaseMode);
            await _driver.SetTimebaseScaleAsync(secs);
            await _driver.SetTimebaseOffsetAsync(off);
            TimescaleDisplay = FormatTime(secs) + "/dz";
            StatusText = $"Timebase: {FormatTime(secs)}/dz  {Now()}";
        }
        catch (Exception ex) { StatusText = $"Błąd Timebase: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyTriggerAsync()
    {
        if (!CheckConn()) return;
        try
        {
            double level = D(TriggerLevel, 0.0);
            await _driver.SetTriggerModeAsync(TriggerMode);
            await _driver.SetTriggerSweepAsync(TriggerSweep);
            await _driver.SetTriggerEdgeSourceAsync(TriggerSource);
            await _driver.SetTriggerEdgeSlopeAsync(TriggerSlope);
            await _driver.SetTriggerEdgeLevelAsync(level);
            StatusText = $"Trigger: {TriggerSource} {TriggerSlope} {level:F3}V  {Now()}";
        }
        catch (Exception ex) { StatusText = $"Błąd Trigger: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ApplyAcquireAsync()
    {
        if (!CheckConn()) return;
        try
        {
            await _driver.SetAcquireTypeAsync(AcquireType);
            if (AcquireType.Equals("AVERages", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(AcquireAvg, out int avg))
                await _driver.SetAcquireAveragesAsync(avg);
            await _driver.SetAcquireMemDepthAsync(AcquireMem);
            StatusText = $"Acquire: {AcquireType}  {Now()}";
        }
        catch (Exception ex) { StatusText = $"Błąd Acquire: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task UpdateMeasurementsAsync()
    {
        if (!CheckConn()) return;
        try
        {
            (M1Result, M1Unit) = await ReadMeas(M1Source, M1Type);
            (M2Result, M2Unit) = await ReadMeas(M2Source, M2Type);
            (M3Result, M3Unit) = await ReadMeas(M3Source, M3Type);
            (M4Result, M4Unit) = await ReadMeas(M4Source, M4Type);
            StatusText = $"Pomiary odświeżone  {Now()}";
        }
        catch (Exception ex) { StatusText = $"Błąd pomiarów: {ex.Message}"; }
    }

    private async Task<(string Result, string Unit)> ReadMeas(string source, string param)
    {
        try
        {
            int ch = source.StartsWith("CH") && int.TryParse(source[2..], out int n) ? n : 1;
            double val = await _driver.MeasureAsync(param, ch);
            return (FormatValue(val, param), GetUnit(param));
        }
        catch { return ("ERR", ""); }
    }

    [RelayCommand]
    private async Task ReadTriggerStatusAsync()
    {
        if (!CheckConn()) return;
        try { TrigStatusText = await _driver.GetTriggerStatusAsync(); }
        catch (Exception ex) { StatusText = $"Błąd TrigStatus: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ReadSampleRateAsync()
    {
        if (!CheckConn()) return;
        try
        {
            double sr = await _driver.GetAcquireSampleRateAsync();
            SampleRateText = sr >= 1e9 ? $"{sr / 1e9:G4} GSa/s"
                           : sr >= 1e6 ? $"{sr / 1e6:G4} MSa/s"
                           : sr >= 1e3 ? $"{sr / 1e3:G4} kSa/s"
                                       : $"{sr:G4} Sa/s";
        }
        catch (Exception ex) { StatusText = $"Błąd SampleRate: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task CaptureWaveformAsync()
    {
        if (!CheckConn()) return;
        IsCapturing = true;
        try
        {
            StatusText = "Odczyt przebiegów...";
            bool[] enabled = { Ch1Enabled, Ch2Enabled, Ch3Enabled, Ch4Enabled };

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
            }

            _scopeModel.ResetAllAxes();
            _scopeModel.InvalidatePlot(true);
            StatusText = $"Przebiegi wczytane  {Now()}";
        }
        catch (Exception ex) { StatusText = $"Błąd odczytu: {ex.Message}"; }
        finally { IsCapturing = false; }
    }

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        if (!CheckConn()) return;
        var dlg = new SaveFileDialog
        {
            Title      = "Zapisz screenshot oscyloskopu",
            Filter     = "BMP (*.bmp)|*.bmp|Wszystkie pliki (*.*)|*.*",
            DefaultExt = ".bmp",
            FileName   = $"DS1000Z_{DateTime.Now:yyyyMMdd_HHmmss}",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            StatusText = "Pobieranie screenshota...";
            byte[] data = await _driver.TakeScreenshotAsync();
            if (data.Length == 0) { StatusText = "Screenshot: brak danych"; return; }
            await File.WriteAllBytesAsync(dlg.FileName, data);
            StatusText = $"Screenshot: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex) { StatusText = $"Błąd screenshot: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ReadCursorsAsync()
    {
        if (!CheckConn()) return;
        try
        {
            double ax = await _driver.GetCursorAXValueAsync();
            double bx = await _driver.GetCursorBXValueAsync();
            double dx = await _driver.GetCursorXDeltaAsync();
            double ay = await _driver.GetCursorAYValueAsync();
            double by = await _driver.GetCursorBYValueAsync();
            double dy = await _driver.GetCursorYDeltaAsync();
            CursorAX = $"{ax:G5} s";
            CursorBX = $"{bx:G5} s";
            CursorDX = $"{dx:G5} s";
            CursorAY = $"{ay:G5} V";
            CursorBY = $"{by:G5} V";
            CursorDY = $"{dy:G5} V";
        }
        catch (Exception ex) { StatusText = $"Błąd kursorów: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ForceTriggerAsync()
    {
        if (!CheckConn()) return;
        try { await _driver.ForceTriggerAsync(); StatusText = "Force Trigger"; }
        catch (Exception ex) { StatusText = $"Błąd ForceTrig: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ClearMeasurementsAsync()
    {
        if (!CheckConn()) return;
        try
        {
            await _driver.ClearMeasurementsAsync();
            M1Result = M2Result = M3Result = M4Result = "---";
            StatusText = "Pomiary wyczyszczone";
        }
        catch (Exception ex) { StatusText = $"Błąd ClearMeas: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Zapisz preset oscyloskopu",
            Filter     = "Preset JSON (*.json)|*.json",
            DefaultExt = ".json",
            FileName   = $"DS1000Z_preset_{DateTime.Now:yyyyMMdd_HHmmss}",
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
            var preset  = JsonSerializer.Deserialize<RigolPreset>(json);
            if (preset == null) { StatusText = "Błąd: nieprawidłowy plik presetu"; return; }
            ApplyPreset(preset);
            StatusText = $"Preset wczytany: {Path.GetFileName(dlg.FileName)}";
            if (_driver.IsConnected)
            {
                await ApplyChannelsAsync();
                await ApplyTimebaseAsync();
                await ApplyTriggerAsync();
            }
        }
        catch (Exception ex) { StatusText = $"Błąd wczytywania: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!CheckConn()) return;
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

    [RelayCommand]
    private void OpenLiveWindow()
    {
        if (_liveWindow != null && _liveWindow.IsLoaded) { _liveWindow.Activate(); return; }
        _liveWindow = new LiveDataWindow(_driver);
        _liveWindow.Show();
    }

    // ── Preset helpers ────────────────────────────────────────────────────────

    private RigolPreset BuildPreset() => new()
    {
        Channels = new[]
        {
            new RigolChannelPreset { Enabled=Ch1Enabled, Scale=Ch1Scale, Offset=Ch1Offset, Coupling=Ch1Coupling, Probe=Ch1Probe, BWLimit=Ch1BwLimit, Invert=Ch1Invert },
            new RigolChannelPreset { Enabled=Ch2Enabled, Scale=Ch2Scale, Offset=Ch2Offset, Coupling=Ch2Coupling, Probe=Ch2Probe, BWLimit=Ch2BwLimit, Invert=Ch2Invert },
            new RigolChannelPreset { Enabled=Ch3Enabled, Scale=Ch3Scale, Offset=Ch3Offset, Coupling=Ch3Coupling, Probe=Ch3Probe, BWLimit=Ch3BwLimit, Invert=Ch3Invert },
            new RigolChannelPreset { Enabled=Ch4Enabled, Scale=Ch4Scale, Offset=Ch4Offset, Coupling=Ch4Coupling, Probe=Ch4Probe, BWLimit=Ch4BwLimit, Invert=Ch4Invert },
        },
        Timebase      = SelectedTimescale,
        TriggerSource = TriggerSource,
        TriggerLevel  = TriggerLevel,
        TriggerSlope  = TriggerSlope,
        TriggerSweep  = TriggerSweep,
        AcqType       = AcquireType,
    };

    private void ApplyPreset(RigolPreset p)
    {
        if (p.Channels?.Length >= 1) Apply1(p.Channels[0]);
        if (p.Channels?.Length >= 2) Apply2(p.Channels[1]);
        if (p.Channels?.Length >= 3) Apply3(p.Channels[2]);
        if (p.Channels?.Length >= 4) Apply4(p.Channels[3]);
        SelectedTimescale = p.Timebase      ?? SelectedTimescale;
        TriggerSource     = p.TriggerSource ?? TriggerSource;
        TriggerLevel      = p.TriggerLevel  ?? TriggerLevel;
        TriggerSlope      = p.TriggerSlope  ?? TriggerSlope;
        TriggerSweep      = p.TriggerSweep  ?? TriggerSweep;
        AcquireType       = p.AcqType       ?? AcquireType;
    }

    private void Apply1(RigolChannelPreset c) { Ch1Enabled=c.Enabled; Ch1Scale=c.Scale; Ch1Offset=c.Offset; Ch1Coupling=c.Coupling; Ch1Probe=c.Probe; Ch1BwLimit=c.BWLimit; Ch1Invert=c.Invert; }
    private void Apply2(RigolChannelPreset c) { Ch2Enabled=c.Enabled; Ch2Scale=c.Scale; Ch2Offset=c.Offset; Ch2Coupling=c.Coupling; Ch2Probe=c.Probe; Ch2BwLimit=c.BWLimit; Ch2Invert=c.Invert; }
    private void Apply3(RigolChannelPreset c) { Ch3Enabled=c.Enabled; Ch3Scale=c.Scale; Ch3Offset=c.Offset; Ch3Coupling=c.Coupling; Ch3Probe=c.Probe; Ch3BwLimit=c.BWLimit; Ch3Invert=c.Invert; }
    private void Apply4(RigolChannelPreset c) { Ch4Enabled=c.Enabled; Ch4Scale=c.Scale; Ch4Offset=c.Offset; Ch4Coupling=c.Coupling; Ch4Probe=c.Probe; Ch4BwLimit=c.BWLimit; Ch4Invert=c.Invert; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool CheckConn()
    {
        if (_driver.IsConnected) return true;
        StatusText = "Nie połączono";
        return false;
    }

    private static double D(string s, double fallback) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;

    private static string Now() => DateTime.Now.ToString("HH:mm:ss.fff");

    private static string FormatValue(double v, string type)
    {
        if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 9.9e37) return "OL";
        string unit = GetUnit(type);
        return unit switch
        {
            "Hz" => v >= 1e9 ? $"{v/1e9:F4}G" : v >= 1e6 ? $"{v/1e6:F4}M" : v >= 1e3 ? $"{v/1e3:F3}k" : $"{v:F4}",
            "s"  => Math.Abs(v) >= 1 ? $"{v:G4}" : Math.Abs(v) >= 1e-3 ? $"{v*1e3:F4}m" : Math.Abs(v) >= 1e-6 ? $"{v*1e6:F3}µ" : $"{v*1e9:F3}n",
            "%"  => $"{v:F2}",
            "°"  => $"{v:F2}",
            _    => Math.Abs(v) >= 1 ? $"{v:F5}" : Math.Abs(v) >= 1e-3 ? $"{v*1e3:F4}m" : $"{v:G5}",
        };
    }

    internal static string GetUnit(string type) => type.ToUpperInvariant() switch
    {
        "VMAX" or "VMIN" or "VPP" or "VTOP" or "VBASE"
        or "VAMP" or "VAVG" or "VRMS"
        or "VTOP" or "VBASE"              => "V",
        "FREQUENCY" or "FREQUEN" or "FREQ" => "Hz",
        "PERIOD"  or "PER"                => "s",
        "RISETIME" or "RISE"              => "s",
        "FALLTIME" or "FALL"              => "s",
        "PWIDTH"  or "PWID"               => "s",
        "NWIDTH"  or "NWID"               => "s",
        "PDUTY"   or "NDUTY"
        or "PDUTYC" or "NDUTYC"          => "%",
        "OVERSHOOT" or "PRESHOOT"        => "%",
        "PHASE"                           => "°",
        _                                 => "",
    };

    private static string FormatTime(double secs) =>
        secs >= 1    ? $"{secs:G3} s"
      : secs >= 1e-3 ? $"{secs*1e3:G3} ms"
      : secs >= 1e-6 ? $"{secs*1e6:G3} µs"
                     : $"{secs*1e9:G3} ns";
}
