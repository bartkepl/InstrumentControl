using Agilent34970A.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Agilent34970A.Views;

/// <summary>
/// Zakładka karty 34907A — DAC (×2, ze spinnerami), dwa porty cyfrowe obok siebie
/// oraz totalizator. Slot jest stały (wynika z karty), więc nie ma już wyboru slotu.
/// </summary>
public partial class Multi34907AViewModel : CardTabViewModel
{
    public override string Header => $"Slot {Slot} · 34907A";

    public DioPortViewModel Port1 { get; }
    public DioPortViewModel Port2 { get; }

    public Multi34907AViewModel(Agilent34970ADriver driver, int slot, Action<string> setStatus)
        : base(driver, slot, setStatus)
    {
        Port1 = new DioPortViewModel(driver, slot, 1, setStatus);
        Port2 = new DioPortViewModel(driver, slot, 2, setStatus);
    }

    // ── DAC ────────────────────────────────────────────────────────────────────
    [ObservableProperty] private double _dac1Voltage;
    [ObservableProperty] private double _dac2Voltage;
    [ObservableProperty] private double _dacStep = 0.1;

    public string DacRangeHint => $"Zakres {Card34907A.DacMin:0.#}…{Card34907A.DacMax:0.#} V (kanały s04 / s05)";

    [RelayCommand] private Task SetDac1Async() => SendDacAsync(1, Dac1Voltage);
    [RelayCommand] private Task SetDac2Async() => SendDacAsync(2, Dac2Voltage);

    [RelayCommand] private Task Dac1UpAsync()   { Dac1Voltage = Clamp(Dac1Voltage + DacStep); return SendDacAsync(1, Dac1Voltage); }
    [RelayCommand] private Task Dac1DownAsync() { Dac1Voltage = Clamp(Dac1Voltage - DacStep); return SendDacAsync(1, Dac1Voltage); }
    [RelayCommand] private Task Dac2UpAsync()   { Dac2Voltage = Clamp(Dac2Voltage + DacStep); return SendDacAsync(2, Dac2Voltage); }
    [RelayCommand] private Task Dac2DownAsync() { Dac2Voltage = Clamp(Dac2Voltage - DacStep); return SendDacAsync(2, Dac2Voltage); }

    private static double Clamp(double v) => Math.Clamp(v, Card34907A.DacMin, Card34907A.DacMax);

    private async Task SendDacAsync(int dac, double voltage)
    {
        if (!EnsureConnected()) return;
        try
        {
            await Driver.SetDacAsync(Slot, dac, voltage);
            SetStatus($"DAC{dac} slot {Slot}: {voltage:F4} V");
        }
        catch (Exception ex) { SetStatus($"Błąd DAC{dac}: {ex.Message}"); }
    }

    // ── Totalizator (kanał s03) ──────────────────────────────────────────────────
    [ObservableProperty] private string _totalizerValueText = "---";

    [RelayCommand]
    private async Task ReadTotalizerAsync()
    {
        if (!EnsureConnected()) return;
        try
        {
            double v = await Driver.ReadTotalizerAsync(Slot);
            TotalizerValueText = double.IsNaN(v) ? "ERR" : $"{v:F0}";
            SetStatus($"Totalizer slot {Slot}: {TotalizerValueText}");
        }
        catch (Exception ex) { SetStatus($"Błąd totalizer: {ex.Message}"); TotalizerValueText = "ERR"; }
    }

    [RelayCommand]
    private async Task ResetTotalizerAsync()
    {
        if (!EnsureConnected()) return;
        try
        {
            await Driver.ResetTotalizerAsync(Slot);
            TotalizerValueText = "0";
            SetStatus($"Totalizer slot {Slot} — reset");
        }
        catch (Exception ex) { SetStatus($"Błąd reset totalizer: {ex.Message}"); }
    }
}
