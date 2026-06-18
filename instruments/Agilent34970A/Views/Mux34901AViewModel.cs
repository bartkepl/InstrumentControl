using System.Collections.ObjectModel;
using System.Globalization;
using Agilent34970A.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.Core.Models;

namespace Agilent34970A.Views;

/// <summary>
/// Jeden wiersz konfiguracji kanału MUX. Pola zaawansowane (rozdzielczość, NPLC,
/// skalowanie Mx+B) edytuje się w osobnym oknie konfiguracji kanału.
/// </summary>
public partial class ChannelConfigRow : ObservableObject
{
    /// <summary>Właściciel (zakładka MUX) — dostarcza dynamiczne listy kanałów/funkcji.</summary>
    internal Mux34901AViewModel? Owner;

    [ObservableProperty] private int _channel = 101;
    [ObservableProperty] private string _function = "VDC";
    [ObservableProperty] private string _range = "AUTO";
    [ObservableProperty] private string _param = "";

    /// <summary>Dynamiczna lista kanałów dla tego wiersza (zajęte przez inne wiersze znikają).</summary>
    public IReadOnlyList<int> AvailableChannels =>
        Owner?.GetAvailableChannelsFor(this) ?? new List<int> { Channel };

    /// <summary>Dynamiczna lista funkcji (kanały prądowe 21/22 → tylko CURR_DC/AC).</summary>
    public IReadOnlyList<string> FunctionOptions =>
        Owner?.GetFunctionOptionsFor(this) ?? new List<string> { Function };

    /// <summary>Odświeża dynamiczne listy po zmianie w innym wierszu.</summary>
    internal void RaiseAvailabilityChanged()
    {
        OnPropertyChanged(nameof(AvailableChannels));
        OnPropertyChanged(nameof(FunctionOptions));
    }

    partial void OnChannelChanged(int value)
    {
        if (Owner == null) return;
        // Kanały 21/22 obsługują tylko prąd; pozostałe — nie prąd. Auto-przełącz funkcję.
        bool isCurrent = Owner.IsCurrentChannel(value);
        if (isCurrent && Function is not ("CURR_DC" or "CURR_AC"))
            Function = "CURR_DC";
        else if (!isCurrent && Function is "CURR_DC" or "CURR_AC")
            Function = "VDC";
        Owner.OnRowConfigChanged();
    }

    private string _lastValidRange = "AUTO";

    /// <summary>
    /// Walidacja zakresu: dopuszczalne słowa kluczowe (AUTO/DEF/MIN/MAX) albo liczba.
    /// Wartość nieprawidłowa (tekst) jest cofana do ostatniej poprawnej.
    /// </summary>
    partial void OnRangeChanged(string value)
    {
        if (IsValidRange(value)) _lastValidRange = value;
        else Range = _lastValidRange;
    }

    private static bool IsValidRange(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return true;
        string t = v.Trim().ToUpperInvariant();
        if (t is "AUTO" or "DEF" or "MIN" or "MAX") return true;
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    // ── Zaawansowane (okno konfiguracji kanału) ──
    [ObservableProperty] private string _resolution = "DEF";
    [ObservableProperty] private string _nplc = "";
    [ObservableProperty] private bool _scaleEnabled;
    [ObservableProperty] private double _scaleGain = 1.0;
    [ObservableProperty] private double _scaleOffset;
    [ObservableProperty] private string _scaleUnit = "";

    /// <summary>Czy funkcja ma sensowny parametr (typ TC / α RTD / typ termistora).</summary>
    public bool HasParam => ParamOptions.Count > 0;

    /// <summary>Opcje pola Parametr zależne od funkcji.</summary>
    public List<string> ParamOptions => Function?.ToUpperInvariant() switch
    {
        "TEMP_TC" => new() { "J", "K", "T", "E", "N", "R", "S", "B" },
        "TEMP_RTD" or "TEMP_RTD4W" => new() { "85", "91" },
        "TEMP_THERM" => new() { "2200", "5000", "10000" },
        _ => new()
    };

    public string ParamLabel => Function?.ToUpperInvariant() switch
    {
        "TEMP_TC" => "Typ termoelementu:",
        "TEMP_RTD" or "TEMP_RTD4W" => "Współczynnik α RTD:",
        "TEMP_THERM" => "Typ termistora [Ω]:",
        _ => "Parametr (nie dotyczy tej funkcji):"
    };

    /// <summary>Podsumowanie skalowania Mx+B do osobnej kolumny.</summary>
    public string ScaleSummary => ScaleEnabled
        ? $"M={ScaleGain:0.###}  B={ScaleOffset:0.###}{(string.IsNullOrWhiteSpace(ScaleUnit) ? "" : " " + ScaleUnit)}"
        : "—";

    partial void OnFunctionChanged(string value)
    {
        OnPropertyChanged(nameof(ParamOptions));
        OnPropertyChanged(nameof(ParamLabel));
        OnPropertyChanged(nameof(HasParam));
        // Dla funkcji temperatury ustaw sensowny domyślny parametr, jeśli pusty/niepasujący.
        var opts = ParamOptions;
        if (opts.Count > 0 && !opts.Contains(Param)) Param = opts[0];
        else if (opts.Count == 0) Param = "";
        // Zmiana na/za 4W zmienia rezerwację kanału n+10 → odśwież listy w innych wierszach.
        Owner?.OnRowConfigChanged();
    }

    // Odśwież podsumowanie skalowania po zmianie pól skalowania.
    partial void OnScaleEnabledChanged(bool value) => OnPropertyChanged(nameof(ScaleSummary));
    partial void OnScaleGainChanged(double value) => OnPropertyChanged(nameof(ScaleSummary));
    partial void OnScaleOffsetChanged(double value) => OnPropertyChanged(nameof(ScaleSummary));
    partial void OnScaleUnitChanged(string value) => OnPropertyChanged(nameof(ScaleSummary));

    public ChannelMeasurement ToMeasurement()
    {
        var m = ChannelMeasurement.FromUi(Channel, Function, Range, Param);
        m.Resolution = string.IsNullOrWhiteSpace(Resolution) ? "DEF" : Resolution.Trim();
        m.Nplc = Nplc?.Trim() ?? "";
        m.ScaleEnabled = ScaleEnabled;
        m.ScaleGain = ScaleGain;
        m.ScaleOffset = ScaleOffset;
        m.ScaleUnit = ScaleUnit?.Trim() ?? "";
        return m;
    }
}

public partial class Mux34901AViewModel : CardTabViewModel
{
    public override string Header => $"Slot {Slot} · 34901A";

    public Mux34901AViewModel(Agilent34970ADriver driver, int slot, Action<string> setStatus)
        : base(driver, slot, setStatus)
    {
        // Pełna lista kanałów karty: 1..20 (multiplekser) + 21,22 (prąd).
        _allChannels = Enumerable.Range(1, 22).Select(c => slot + c).ToList();

        // Domyślny przykład: 2× VDC + 1× RTD 4W.
        AddConfiguredRow(slot + 1, "VDC");
        AddConfiguredRow(slot + 2, "VDC");
        AddConfiguredRow(slot + 3, "TEMP_RTD4W", "85");
        OnRowConfigChanged();
    }

    // ── Dostępne kanały / podpowiedzi ─────────────────────────────────────────────
    private readonly List<int> _allChannels;

    /// <summary>Czy to jeden z dwóch kanałów prądowych (21/22).</summary>
    public bool IsCurrentChannel(int absChannel) => absChannel == Slot + 21 || absChannel == Slot + 22;

    private HashSet<int> OccupiedChannels(ChannelConfigRow? except)
    {
        var occ = new HashSet<int>();
        foreach (var r in ChannelConfigs)
        {
            if (ReferenceEquals(r, except)) continue;
            occ.Add(r.Channel);
            // Pomiar 4W rezerwuje dodatkowo kanał n+10.
            if (r.Function is "OHM4W" or "TEMP_RTD4W") occ.Add(r.Channel + 10);
        }
        return occ;
    }

    /// <summary>Lista kanałów dostępnych dla danego wiersza (zajęte + pary 4W znikają).</summary>
    public IReadOnlyList<int> GetAvailableChannelsFor(ChannelConfigRow row)
    {
        var occ = OccupiedChannels(row);
        var list = _allChannels.Where(ch => !occ.Contains(ch)).ToList();
        if (!list.Contains(row.Channel)) { list.Add(row.Channel); list.Sort(); }
        return list;
    }

    /// <summary>Funkcje dostępne dla wiersza: kanały 21/22 → tylko prąd, pozostałe → bez prądu.</summary>
    public IReadOnlyList<string> GetFunctionOptionsFor(ChannelConfigRow row) =>
        IsCurrentChannel(row.Channel)
            ? new List<string> { "CURR_DC", "CURR_AC" }
            : FunctionNames.Where(f => f is not ("CURR_DC" or "CURR_AC")).ToList();

    /// <summary>Po zmianie dowolnego wiersza odśwież dynamiczne listy we wszystkich wierszach.</summary>
    public void OnRowConfigChanged()
    {
        foreach (var r in ChannelConfigs) r.RaiseAvailabilityChanged();
    }

    private void AddConfiguredRow(int channel, string function, string param = "")
    {
        var row = new ChannelConfigRow { Channel = channel, Function = function, Param = param };
        row.Owner = this;
        ChannelConfigs.Add(row);
    }

    public string ChannelHint =>
        $"Kanały {Slot + 1}–{Slot + 20}. Prąd tylko na {Slot + 21}/{Slot + 22}. " +
        $"Pomiar 4W (FRES / RTD 4W): kanał źródłowy {Slot + 1}–{Slot + 10}, parowany z n+10 ({Slot + 11}–{Slot + 20}).";

    public List<string> FunctionNames { get; } = new()
    {
        "VDC", "VAC", "OHM2W", "OHM4W", "CURR_DC", "CURR_AC",
        "FREQ", "PERIOD", "TEMP_TC", "TEMP_RTD", "TEMP_RTD4W", "TEMP_THERM"
    };

    // ── Konfiguracja kanałów ──────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ChannelConfigRow> _channelConfigs = new();
    [ObservableProperty] private ChannelConfigRow? _selectedConfigRow;
    [ObservableProperty] private ObservableCollection<MeasurementResult> _scanResults = new();

    [ObservableProperty] private bool _isContinuousScan;
    [ObservableProperty] private string _selectedScanInterval = "2000";
    public List<string> ScanIntervals { get; } = new() { "500", "1000", "2000", "5000", "10000" };

    private CancellationTokenSource? _continuousScanCts;

    [RelayCommand]
    private void AddRow()
    {
        var occupied = OccupiedChannels(null);
        int next = _allChannels.FirstOrDefault(ch => !occupied.Contains(ch));
        if (next == 0) { SetStatus("Brak wolnych kanałów."); return; }
        AddConfiguredRow(next, IsCurrentChannel(next) ? "CURR_DC" : "VDC");
        OnRowConfigChanged();
    }

    [RelayCommand]
    private void RemoveRow()
    {
        if (SelectedConfigRow != null) ChannelConfigs.Remove(SelectedConfigRow);
        else if (ChannelConfigs.Count > 0) ChannelConfigs.RemoveAt(ChannelConfigs.Count - 1);
        OnRowConfigChanged();
    }

    [RelayCommand]
    private void OpenChannelConfig()
    {
        if (SelectedConfigRow == null) { SetStatus("Zaznacz kanał w tabeli."); return; }
        var win = new ChannelConfigWindow { DataContext = SelectedConfigRow };
        win.Owner = System.Windows.Application.Current?.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    private void OpenScanConfig()
    {
        var win = new ScanConfigWindow { DataContext = this };
        win.Owner = System.Windows.Application.Current?.MainWindow;
        win.ShowDialog();
    }

    // ── Ustawienia skanu (edytowane w oknie konfiguracji skanu) ────────────────────
    public List<string> TriggerSources { get; } = new() { "IMM", "TIMER", "EXT", "BUS", "ALARM" };
    [ObservableProperty] private string _selectedTriggerSource = "IMM";
    [ObservableProperty] private string _timerIntervalText = "1";
    [ObservableProperty] private int _scanCount = 1;
    [ObservableProperty] private bool _useChannelDelay;
    [ObservableProperty] private string _channelDelayText = "0";

    private ScanSettings BuildScanSettings() => new()
    {
        TriggerSource = SelectedTriggerSource,
        TimerInterval = ParseD(TimerIntervalText),
        ScanCount = Math.Max(1, ScanCount),
        ChannelDelay = UseChannelDelay ? ParseD(ChannelDelayText) : -1
    };

    private static double ParseD(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

    // ── Skan ────────────────────────────────────────────────────────────────────────
    private async Task ExecuteScanAsync()
    {
        if (!EnsureConnected()) return;

        var configs = ChannelConfigs.Select(r => r.ToMeasurement()).ToList();
        if (configs.Count == 0) { SetStatus("Dodaj przynajmniej jeden kanał."); return; }

        SetStatus($"Skanowanie {configs.Count} kanałów…");
        var results = await Driver.ScanAsync(configs, BuildScanSettings());
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ScanResults.Clear();
            foreach (var r in results) ScanResults.Add(r);
        });
        SetStatus($"OK — {results.Count} pomiarów  {DateTime.Now:HH:mm:ss}");
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        try { await ExecuteScanAsync(); }
        catch (Exception ex) { SetStatus($"Błąd skanowania: {ex.Message}"); }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleContinuousScanAsync()
    {
        if (IsContinuousScan) { _continuousScanCts?.Cancel(); return; }

        IsContinuousScan = true;
        _continuousScanCts = new CancellationTokenSource();
        var ct = _continuousScanCts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try { await ExecuteScanAsync(); }
                catch (Exception ex) { SetStatus($"Błąd: {ex.Message}"); }

                int intervalMs = int.TryParse(SelectedScanInterval, out int iv) ? iv : 2000;
                try { await Task.Delay(intervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            IsContinuousScan = false;
            SetStatus("Ciągłe skanowanie zatrzymane.");
        }
    }
}
