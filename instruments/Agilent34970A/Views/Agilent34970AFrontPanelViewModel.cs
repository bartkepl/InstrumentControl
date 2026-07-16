using System.Collections.ObjectModel;
using System.Windows;
using Agilent34970A.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Agilent34970A.Views;

/// <summary>
/// Powłoka panelu czołowego 34970A. Odpowiada za wykrywanie/konfigurację kart i buduje
/// zakładki <b>dynamicznie na podstawie kart w slotach</b> — jedna zakładka na kartę.
/// </summary>
public partial class Agilent34970AFrontPanelViewModel : ObservableObject
{
    private readonly Agilent34970ADriver _driver;
    private bool _suppressRebuild;

    // ── Konfiguracja kart (sidebar) ─────────────────────────────────────────────
    public List<string> CardTypes { get; } = new() { "Empty", "34901A (20ch Mux)", "34907A (Multifunction)" };

    [ObservableProperty] private string _slot100CardType = "Empty";
    [ObservableProperty] private string _slot200CardType = "Empty";
    [ObservableProperty] private string _slot300CardType = "Empty";
    [ObservableProperty] private bool _isDetecting;

    partial void OnSlot100CardTypeChanged(string value) => ApplyCardConfig(100, value);
    partial void OnSlot200CardTypeChanged(string value) => ApplyCardConfig(200, value);
    partial void OnSlot300CardTypeChanged(string value) => ApplyCardConfig(300, value);

    private void ApplyCardConfig(int slot, string cardType)
    {
        _driver.Cards.Remove(slot);
        if (cardType.StartsWith("34901A")) _driver.AddCard34901A(slot);
        else if (cardType.StartsWith("34907A")) _driver.AddCard34907A(slot);

        if (!_suppressRebuild)
        {
            RebuildTabs();
            StatusText = T("FP_A34970A_SlotStatus", "Slot {0}: {1}", slot, cardType);
        }
    }

    [RelayCommand]
    private async Task DetectCardsAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_A34970A_NotConnected", "Instrument is not connected."); return; }
        IsDetecting = true;
        _suppressRebuild = true;
        try
        {
            var cards = await _driver.DetectCardsAsync();
            Slot100CardType = ModelToCombo(cards.TryGetValue(100, out var m1) ? m1 : "");
            Slot200CardType = ModelToCombo(cards.TryGetValue(200, out var m2) ? m2 : "");
            Slot300CardType = ModelToCombo(cards.TryGetValue(300, out var m3) ? m3 : "");
            int found = cards.Values.Count(v => !string.IsNullOrEmpty(v));
            StatusText = T("FP_A34970A_DetectDone", "Detection complete — found {0} cards.", found);
        }
        catch (Exception ex) { StatusText = T("FP_A34970A_ErrDetect", "Card detection error: {0}", ex.Message); }
        finally
        {
            _suppressRebuild = false;
            RebuildTabs();
            IsDetecting = false;
        }
    }

    private static string ModelToCombo(string model) => model switch
    {
        "34901A" => "34901A (20ch Mux)",
        "34907A" => "34907A (Multifunction)",
        _ => "Empty"
    };

    // ── Zakładki budowane z kart ─────────────────────────────────────────────────
    public ObservableCollection<CardTabViewModel> CardTabs { get; } = new();
    [ObservableProperty] private CardTabViewModel? _selectedTab;

    public Visibility TabsVisibility => CardTabs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => CardTabs.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

    private void RebuildTabs()
    {
        CardTabs.Clear();
        foreach (int slot in Agilent34970ADriver.Slots)
        {
            if (!_driver.Cards.TryGetValue(slot, out var card)) continue;
            CardTabViewModel? vm = card switch
            {
                Card34901A => new Mux34901AViewModel(_driver, slot, SetStatus),
                Card34907A => new Multi34907AViewModel(_driver, slot, SetStatus),
                _ => null
            };
            if (vm != null) CardTabs.Add(vm);
        }
        SelectedTab = CardTabs.FirstOrDefault();
        OnPropertyChanged(nameof(TabsVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
    }

    private void SetStatus(string msg) => StatusText = msg;

    // ── Ogólne ───────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusText = "Ready";

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_A34970A_NotConnected", "Instrument is not connected."); return; }
        try { await _driver.ResetAsync(); StatusText = T("FP_ResetDone", "Reset done"); }
        catch (Exception ex) { StatusText = T("FP_ErrReset", "Reset error: {0}", ex.Message); }
    }

    [RelayCommand]
    private async Task QueryStatusAsync()
    {
        if (!_driver.IsConnected) { StatusText = T("FP_A34970A_NotConnected", "Instrument is not connected."); return; }
        try { StatusText = await _driver.GetIdentificationAsync(); }
        catch (Exception ex) { StatusText = T("FP_A34970A_ErrStatus", "Status error: {0}", ex.Message); }
    }

    // ── Konstruktor ────────────────────────────────────────────────────────────────
    public Agilent34970AFrontPanelViewModel(Agilent34970ADriver driver)
    {
        _driver = driver;

        _suppressRebuild = true;
        if (driver.Cards.TryGetValue(100, out var c100)) _slot100CardType = CardToCombo(c100);
        if (driver.Cards.TryGetValue(200, out var c200)) _slot200CardType = CardToCombo(c200);
        if (driver.Cards.TryGetValue(300, out var c300)) _slot300CardType = CardToCombo(c300);
        _suppressRebuild = false;

        RebuildTabs();

        _statusChangedHandler = (_, msg) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => StatusText = msg);
        _errorOccurredHandler = (_, ex) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => StatusText = T("FP_ErrGeneric", "Error: {0}", ex.Message));
        driver.StatusChanged += _statusChangedHandler;
        driver.ErrorOccurred += _errorOccurredHandler;
    }

    private readonly EventHandler<string> _statusChangedHandler;
    private readonly EventHandler<Exception> _errorOccurredHandler;

    // Called when the front panel is torn down (e.g. the user switches to a
    // different connected instrument) so this ViewModel stops reacting to driver
    // events and can be garbage collected instead of leaking as a "zombie"
    // listener that keeps handling every future status/error update.
    public void Detach()
    {
        _driver.StatusChanged -= _statusChangedHandler;
        _driver.ErrorOccurred -= _errorOccurredHandler;
    }

    private static string CardToCombo(CardBase card) => card switch
    {
        Card34901A => "34901A (20ch Mux)",
        Card34907A => "34907A (Multifunction)",
        _ => "Empty"
    };

    private static string T(string key, string fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;

    private static string T(string key, string fallback, params object[] args) =>
        string.Format(T(key, fallback), args);
}
