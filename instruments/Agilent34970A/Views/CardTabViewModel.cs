using CommunityToolkit.Mvvm.ComponentModel;

namespace Agilent34970A.Views;

/// <summary>
/// Wspólna baza ViewModelu zakładki przypisanej do jednej karty w jednym slocie.
/// Zakładki w panelu są budowane dynamicznie na podstawie kart wykrytych/skonfigurowanych
/// w slotach, dlatego każda zna swój slot i nagłówek.
/// </summary>
public abstract partial class CardTabViewModel : ObservableObject
{
    protected readonly Agilent34970ADriver Driver;
    protected readonly Action<string> SetStatus;

    public int Slot { get; }

    protected CardTabViewModel(Agilent34970ADriver driver, int slot, Action<string> setStatus)
    {
        Driver = driver;
        Slot = slot;
        SetStatus = setStatus;
    }

    /// <summary>Nagłówek zakładki, np. "Slot 100 · 34901A".</summary>
    public abstract string Header { get; }

    protected bool EnsureConnected()
    {
        if (Driver.IsConnected) return true;
        SetStatus("Instrument nie jest połączony.");
        return false;
    }
}
