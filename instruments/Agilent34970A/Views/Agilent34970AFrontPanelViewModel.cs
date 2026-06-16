using System.Collections.ObjectModel;
using System.Windows.Media;
using Agilent34970A.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace Agilent34970A.Views;

public partial class Agilent34970AFrontPanelViewModel : ObservableObject
{
    private readonly Agilent34970ADriver _driver;

    // ── Card config ───────────────────────────────────────────────────────────
    public List<string> CardTypes { get; } = new() { "Empty", "34901A (20ch Mux)", "34907A (Multifunction)" };
    public List<int> AvailableSlots { get; } = new() { 100, 200, 300 };

    [ObservableProperty] private string _slot100CardType = "Empty";
    [ObservableProperty] private string _slot200CardType = "Empty";
    [ObservableProperty] private string _slot300CardType = "Empty";

    partial void OnSlot100CardTypeChanged(string value) => ApplyCardConfig(100, value);
    partial void OnSlot200CardTypeChanged(string value) => ApplyCardConfig(200, value);
    partial void OnSlot300CardTypeChanged(string value) => ApplyCardConfig(300, value);

    private void ApplyCardConfig(int slot, string cardType)
    {
        _driver.Cards.Remove(slot);
        if (cardType.StartsWith("34901A"))
            _driver.AddCard34901A(slot);
        else if (cardType.StartsWith("34907A"))
            _driver.AddCard34907A(slot);
        StatusText = $"Slot {slot}: {cardType}";
    }

    // ── Scan ──────────────────────────────────────────────────────────────────
    public List<string> AvailableFunctions { get; } = new() { "VDC", "VAC", "OHM2W", "OHM4W", "FREQ" };

    [ObservableProperty] private string _channelList = "101,102,103";
    [ObservableProperty] private string _selectedFunction = "VDC";
    [ObservableProperty] private ObservableCollection<MeasurementResult> _scanResults = new();

    [ObservableProperty] private bool _isContinuousScan;
    [ObservableProperty] private string _selectedScanInterval = "2000";
    public List<string> ScanIntervals { get; } = new() { "500", "1000", "2000", "5000", "10000" };

    private CancellationTokenSource? _continuousScanCts;

    private async Task ExecuteScanAsync()
    {
        if (!_driver.IsConnected)
        {
            StatusText = "Instrument nie jest połączony.";
            return;
        }
        StatusText = $"Skanowanie: {ChannelList}…";
        var results = await _driver.ScanChannelListAsync(ChannelList);
        ScanResults.Clear();
        foreach (var r in results)
            ScanResults.Add(r);
        StatusText = $"OK — {results.Count} pomiarów  {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        try { await ExecuteScanAsync(); }
        catch (Exception ex) { StatusText = $"Błąd skanowania: {ex.Message}"; }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleContinuousScanAsync()
    {
        if (IsContinuousScan)
        {
            _continuousScanCts?.Cancel();
            return;
        }

        IsContinuousScan = true;
        _continuousScanCts = new CancellationTokenSource();
        var ct = _continuousScanCts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try { await ExecuteScanAsync(); }
                catch (Exception ex) { StatusText = $"Błąd: {ex.Message}"; }

                int intervalMs = int.TryParse(SelectedScanInterval, out int iv) ? iv : 2000;
                try { await Task.Delay(intervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            IsContinuousScan = false;
            StatusText = "Ciągłe skanowanie zatrzymane.";
        }
    }

    // ── DAC ───────────────────────────────────────────────────────────────────
    [ObservableProperty] private double _dac1Voltage = 0.0;
    [ObservableProperty] private double _dac2Voltage = 0.0;
    [ObservableProperty] private int _selectedSlotFor34907A = 100;

    [RelayCommand]
    private async Task SetDac1Async()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            await _driver.SetDacAsync(SelectedSlotFor34907A, 1, Dac1Voltage);
            StatusText = $"DAC1 slot {SelectedSlotFor34907A}: {Dac1Voltage:F4} V";
        }
        catch (Exception ex) { StatusText = $"Błąd DAC1: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SetDac2Async()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            await _driver.SetDacAsync(SelectedSlotFor34907A, 2, Dac2Voltage);
            StatusText = $"DAC2 slot {SelectedSlotFor34907A}: {Dac2Voltage:F4} V";
        }
        catch (Exception ex) { StatusText = $"Błąd DAC2: {ex.Message}"; }
    }

    // ── Digital I/O ───────────────────────────────────────────────────────────
    [ObservableProperty] private byte _digitalOutputValue = 0;
    [ObservableProperty] private byte _digitalInputValue  = 0;

    // Individual output bits — two-way bound to toggle buttons
    [ObservableProperty] private bool _outputBit7;
    [ObservableProperty] private bool _outputBit6;
    [ObservableProperty] private bool _outputBit5;
    [ObservableProperty] private bool _outputBit4;
    [ObservableProperty] private bool _outputBit3;
    [ObservableProperty] private bool _outputBit2;
    [ObservableProperty] private bool _outputBit1;
    [ObservableProperty] private bool _outputBit0;

    // Recalculate DigitalOutputValue whenever a bit changes
    partial void OnOutputBit7Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit6Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit5Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit4Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit3Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit2Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit1Changed(bool value) => RecalcOutputByte();
    partial void OnOutputBit0Changed(bool value) => RecalcOutputByte();

    private void RecalcOutputByte()
    {
        int v = (OutputBit7 ? 0x80 : 0) |
                (OutputBit6 ? 0x40 : 0) |
                (OutputBit5 ? 0x20 : 0) |
                (OutputBit4 ? 0x10 : 0) |
                (OutputBit3 ? 0x08 : 0) |
                (OutputBit2 ? 0x04 : 0) |
                (OutputBit1 ? 0x02 : 0) |
                (OutputBit0 ? 0x01 : 0);
        DigitalOutputValue = (byte)v;
    }

    // Input bit indicator brushes
    private static readonly SolidColorBrush _highBrush = new(Color.FromRgb(0x27, 0xAE, 0x60));
    private static readonly SolidColorBrush _lowBrush  = new(Color.FromRgb(0x3D, 0x3D, 0x5C));

    public SolidColorBrush InputBit7Brush => BitBrush(DigitalInputValue, 7);
    public SolidColorBrush InputBit6Brush => BitBrush(DigitalInputValue, 6);
    public SolidColorBrush InputBit5Brush => BitBrush(DigitalInputValue, 5);
    public SolidColorBrush InputBit4Brush => BitBrush(DigitalInputValue, 4);
    public SolidColorBrush InputBit3Brush => BitBrush(DigitalInputValue, 3);
    public SolidColorBrush InputBit2Brush => BitBrush(DigitalInputValue, 2);
    public SolidColorBrush InputBit1Brush => BitBrush(DigitalInputValue, 1);
    public SolidColorBrush InputBit0Brush => BitBrush(DigitalInputValue, 0);

    private static SolidColorBrush BitBrush(byte value, int bit) =>
        (value & (1 << bit)) != 0 ? _highBrush : _lowBrush;

    partial void OnDigitalInputValueChanged(byte value)
    {
        OnPropertyChanged(nameof(InputBit7Brush));
        OnPropertyChanged(nameof(InputBit6Brush));
        OnPropertyChanged(nameof(InputBit5Brush));
        OnPropertyChanged(nameof(InputBit4Brush));
        OnPropertyChanged(nameof(InputBit3Brush));
        OnPropertyChanged(nameof(InputBit2Brush));
        OnPropertyChanged(nameof(InputBit1Brush));
        OnPropertyChanged(nameof(InputBit0Brush));
    }

    [RelayCommand]
    private async Task ReadDigitalAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            DigitalInputValue = await _driver.ReadDigitalInputAsync(SelectedSlotFor34907A);
            StatusText = $"Digital IN slot {SelectedSlotFor34907A}: 0x{DigitalInputValue:X2}";
        }
        catch (Exception ex) { StatusText = $"Błąd odczytu cyfrowego: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task WriteDigitalAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            await _driver.SetDigitalOutputAsync(SelectedSlotFor34907A, DigitalOutputValue);
            StatusText = $"Digital OUT slot {SelectedSlotFor34907A}: 0x{DigitalOutputValue:X2}";
        }
        catch (Exception ex) { StatusText = $"Błąd wyjścia cyfrowego: {ex.Message}"; }
    }

    // ── Temperature TC scan ───────────────────────────────────────────────────
    [ObservableProperty] private string _tempChannelList = "101,102";
    [ObservableProperty] private string _selectedTempTcType = "K";
    [ObservableProperty] private bool _isContinuousTempScan = false;
    [ObservableProperty] private string _selectedTempScanInterval = "2000";
    [ObservableProperty] private ObservableCollection<MeasurementResult> _tempResults = new();
    public List<string> TcTypes { get; } = new() { "J", "K", "T", "E", "N", "R", "S", "B" };

    private CancellationTokenSource? _continuousTempScanCts;

    [RelayCommand]
    private async Task ScanTemperatureAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        StatusText = $"Skan temp TC-{SelectedTempTcType}: {TempChannelList}…";
        try
        {
            var results = await _driver.MeasureTemperatureTCAsync(TempChannelList, SelectedTempTcType);
            TempResults.Clear();
            foreach (var r in results) TempResults.Add(r);
            StatusText = $"Temp OK — {results.Count} kanałów  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { StatusText = $"Błąd temp: {ex.Message}"; }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleContinuousTempScanAsync()
    {
        if (IsContinuousTempScan)
        {
            _continuousTempScanCts?.Cancel();
            return;
        }
        IsContinuousTempScan = true;
        _continuousTempScanCts = new CancellationTokenSource();
        var ct = _continuousTempScanCts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var results = await _driver.MeasureTemperatureTCAsync(TempChannelList, SelectedTempTcType);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        TempResults.Clear();
                        foreach (var r in results) TempResults.Add(r);
                    });
                    StatusText = $"Temp OK — {results.Count} kanałów  {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex) { StatusText = $"Błąd temp: {ex.Message}"; }
                int iv = int.TryParse(SelectedTempScanInterval, out int ivv) ? ivv : 2000;
                try { await Task.Delay(iv, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            IsContinuousTempScan = false;
            StatusText = "Ciągły skan temp zatrzymany.";
        }
    }

    // ── Totalizer (34907A) ────────────────────────────────────────────────────
    [ObservableProperty] private int _totalizerSlot = 100;
    [ObservableProperty] private string _totalizerValueText = "---";
    public List<int> TotalizerSlots { get; } = new() { 100, 200, 300 };

    [RelayCommand]
    private async Task ReadTotalizerAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            double v = await _driver.ReadTotalizerAsync(TotalizerSlot);
            TotalizerValueText = double.IsNaN(v) ? "ERR" : $"{v:F0}";
            StatusText = $"Totalizer slot {TotalizerSlot}: {TotalizerValueText}";
        }
        catch (Exception ex) { StatusText = $"Błąd totalizer: {ex.Message}"; TotalizerValueText = "ERR"; }
    }

    [RelayCommand]
    private async Task ResetTotalizerAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            await _driver.ResetTotalizerAsync(TotalizerSlot);
            TotalizerValueText = "0";
            StatusText = $"Totalizer slot {TotalizerSlot} — reset";
        }
        catch (Exception ex) { StatusText = $"Błąd reset totalizer: {ex.Message}"; }
    }

    // ── General ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusText = "Ready";

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            await _driver.ResetAsync();
            StatusText = "Reset wykonany.";
        }
        catch (Exception ex) { StatusText = $"Błąd resetu: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task QueryStatusAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try
        {
            string idn = await _driver.GetIdentificationAsync();
            StatusText = idn;
        }
        catch (Exception ex) { StatusText = $"Błąd statusu: {ex.Message}"; }
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public Agilent34970AFrontPanelViewModel(Agilent34970ADriver driver)
    {
        _driver = driver;
        _statusText = System.Windows.Application.Current?.TryFindResource("FP_Ready") as string ?? "Ready";
        AppLocalization.LanguageChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                StatusText = System.Windows.Application.Current?.TryFindResource("FP_Ready") as string ?? "Ready");

        // Sync card type comboboxes from already-configured cards
        if (driver.Cards.TryGetValue(100, out var c100))
            _slot100CardType = c100 is Card34901A ? "34901A (20ch Mux)" :
                               c100 is Card34907A ? "34907A (Multifunction)" : "Empty";
        if (driver.Cards.TryGetValue(200, out var c200))
            _slot200CardType = c200 is Card34901A ? "34901A (20ch Mux)" :
                               c200 is Card34907A ? "34907A (Multifunction)" : "Empty";
        if (driver.Cards.TryGetValue(300, out var c300))
            _slot300CardType = c300 is Card34901A ? "34901A (20ch Mux)" :
                               c300 is Card34907A ? "34907A (Multifunction)" : "Empty";

        // Subscribe to driver events
        driver.StatusChanged   += (_, msg) => StatusText = msg;
        driver.ErrorOccurred   += (_, ex)  => StatusText = $"Błąd: {ex.Message}";
        driver.MeasurementReceived += (_, r) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                // Keep the grid in sync when triggered externally
                ScanResults.Add(r);
                if (ScanResults.Count > 500) ScanResults.RemoveAt(0);
            });
        };
    }
}
