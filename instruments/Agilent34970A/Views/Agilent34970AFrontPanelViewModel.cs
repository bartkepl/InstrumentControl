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
            StatusText = $"Slot {slot}: {cardType}";
        }
    }

    [RelayCommand]
    private async Task DetectCardsAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        IsDetecting = true;
        _suppressRebuild = true;
        try
        {
            var cards = await _driver.DetectCardsAsync();
            Slot100CardType = ModelToCombo(cards.TryGetValue(100, out var m1) ? m1 : "");
            Slot200CardType = ModelToCombo(cards.TryGetValue(200, out var m2) ? m2 : "");
            Slot300CardType = ModelToCombo(cards.TryGetValue(300, out var m3) ? m3 : "");
            int found = cards.Values.Count(v => !string.IsNullOrEmpty(v));
            StatusText = $"Wykrywanie zakończone — znaleziono {found} kart.";
        }
        catch (Exception ex) { StatusText = $"Błąd wykrywania kart: {ex.Message}"; }
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
    [ObservableProperty] private string _statusText = "Gotowy";

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try { await _driver.ResetAsync(); StatusText = "Reset wykonany."; }
        catch (Exception ex) { StatusText = $"Błąd resetu: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task QueryStatusAsync()
    {
        if (!_driver.IsConnected) { StatusText = "Instrument nie jest połączony."; return; }
        try { StatusText = await _driver.GetIdentificationAsync(); }
        catch (Exception ex) { StatusText = $"Błąd statusu: {ex.Message}"; }
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

        driver.StatusChanged += (_, msg) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => StatusText = msg);
        driver.ErrorOccurred += (_, ex) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => StatusText = $"Błąd: {ex.Message}");
    }

    private static string CardToCombo(CardBase card) => card switch
    {
        Card34901A => "34901A (20ch Mux)",
        Card34907A => "34907A (Multifunction)",
        _ => "Empty"
    };
}
