using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Agilent34970A.Views;

/// <summary>
/// ViewModel jednego 8-bitowego portu cyfrowego karty 34907A (port 1 = s01, port 2 = s02).
/// Pozwala pokazać oba porty obok siebie zamiast jednego z przełącznikiem.
/// </summary>
public partial class DioPortViewModel : ObservableObject
{
    private readonly Agilent34970ADriver _driver;
    private readonly int _slot;
    private readonly int _port;
    private readonly Action<string> _setStatus;

    public DioPortViewModel(Agilent34970ADriver driver, int slot, int port, Action<string> setStatus)
    {
        _driver = driver;
        _slot = slot;
        _port = port;
        _setStatus = setStatus;
    }

    public string Title => $"PORT {_port}  (kanał {_slot + _port})";

    // ── Wyjście ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private byte _outputValue;

    [ObservableProperty] private bool _outputBit7;
    [ObservableProperty] private bool _outputBit6;
    [ObservableProperty] private bool _outputBit5;
    [ObservableProperty] private bool _outputBit4;
    [ObservableProperty] private bool _outputBit3;
    [ObservableProperty] private bool _outputBit2;
    [ObservableProperty] private bool _outputBit1;
    [ObservableProperty] private bool _outputBit0;

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
        int v = (OutputBit7 ? 0x80 : 0) | (OutputBit6 ? 0x40 : 0) |
                (OutputBit5 ? 0x20 : 0) | (OutputBit4 ? 0x10 : 0) |
                (OutputBit3 ? 0x08 : 0) | (OutputBit2 ? 0x04 : 0) |
                (OutputBit1 ? 0x02 : 0) | (OutputBit0 ? 0x01 : 0);
        OutputValue = (byte)v;
    }

    // ── Wejście ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private byte _inputValue;

    private static readonly SolidColorBrush _highBrush = new(Color.FromRgb(0x27, 0xAE, 0x60));
    private static readonly SolidColorBrush _lowBrush = new(Color.FromRgb(0x3D, 0x3D, 0x5C));

    public SolidColorBrush InputBit7Brush => BitBrush(InputValue, 7);
    public SolidColorBrush InputBit6Brush => BitBrush(InputValue, 6);
    public SolidColorBrush InputBit5Brush => BitBrush(InputValue, 5);
    public SolidColorBrush InputBit4Brush => BitBrush(InputValue, 4);
    public SolidColorBrush InputBit3Brush => BitBrush(InputValue, 3);
    public SolidColorBrush InputBit2Brush => BitBrush(InputValue, 2);
    public SolidColorBrush InputBit1Brush => BitBrush(InputValue, 1);
    public SolidColorBrush InputBit0Brush => BitBrush(InputValue, 0);

    private static SolidColorBrush BitBrush(byte value, int bit) =>
        (value & (1 << bit)) != 0 ? _highBrush : _lowBrush;

    partial void OnInputValueChanged(byte value)
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

    // ── Komendy ───────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ReadAsync()
    {
        if (!_driver.IsConnected) { _setStatus("Instrument nie jest połączony."); return; }
        try
        {
            InputValue = await _driver.ReadDigitalInputAsync(_slot, _port);
            _setStatus($"Digital IN slot {_slot} port {_port}: 0x{InputValue:X2}");
        }
        catch (Exception ex) { _setStatus($"Błąd odczytu cyfrowego: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task WriteAsync()
    {
        if (!_driver.IsConnected) { _setStatus("Instrument nie jest połączony."); return; }
        try
        {
            await _driver.SetDigitalOutputAsync(_slot, _port, OutputValue);
            _setStatus($"Digital OUT slot {_slot} port {_port}: 0x{OutputValue:X2}");
        }
        catch (Exception ex) { _setStatus($"Błąd wyjścia cyfrowego: {ex.Message}"); }
    }
}
