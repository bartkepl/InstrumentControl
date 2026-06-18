namespace Agilent34970A.Cards;

/// <summary>
/// Wspólna baza kart wtykowych 34970A. Karta odpowiada za jeden slot (100/200/300)
/// i zna własne możliwości oraz zasady budowy komend SCPI. Architektura jest
/// „per karta", a nie „per funkcja" — pojedyncza karta (np. 34907A) udostępnia
/// kilka funkcji jednocześnie (DAC + DIO + totalizator).
/// </summary>
public abstract class CardBase
{
    /// <summary>Numer slotu w formie SCPI: 100, 200 lub 300.</summary>
    public int Slot { get; }

    protected CardBase(int slot)
    {
        if (slot != 100 && slot != 200 && slot != 300)
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot musi być 100, 200 lub 300.");
        Slot = slot;
    }

    /// <summary>Indeks slotu 1..3.</summary>
    public int SlotIndex => Slot / 100;

    /// <summary>Numer modelu, np. "34901A".</summary>
    public abstract string Model { get; }

    /// <summary>Czytelny opis karty (PL).</summary>
    public abstract string DisplayName { get; }

    /// <summary>Bezwzględny kanał SCPI z numeru kanału 1-bazowego (np. slot 200, kanał 3 → 203).</summary>
    public int AbsChannel(int channel) => Slot + channel;

    public override string ToString() => $"{Model} (slot {Slot})";

    /// <summary>
    /// Fabryka karty na podstawie numeru modelu zwróconego przez SYSTem:CTYPe?.
    /// Pusty slot ("0"/"") → null. Nieobsługiwany model → <see cref="GenericCard"/>
    /// (rozpoznany i wyświetlany, lecz bez pełnej logiki).
    /// </summary>
    public static CardBase? Create(int slot, string? model)
    {
        string m = (model ?? "").Trim().ToUpperInvariant();
        return m switch
        {
            "34901A" => new Card34901A(slot),
            "34907A" => new Card34907A(slot),
            "" or "0" => null,
            _ => new GenericCard(slot, model!.Trim())
        };
    }
}

/// <summary>Karta rozpoznana po modelu, lecz nieobsługiwana w pełni przez sterownik.</summary>
public sealed class GenericCard : CardBase
{
    public GenericCard(int slot, string model) : base(slot) => Model = model;
    public override string Model { get; }
    public override string DisplayName => $"{Model} (rozpoznana, ograniczone wsparcie)";
}
