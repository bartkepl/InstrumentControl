namespace Agilent34970A.Cards;

/// <summary>
/// Agilent 34901A — 20-kanałowy multiplekser zwierny (armature).
///
/// Możliwości i ograniczenia (egzekwowane przez <see cref="Validate"/>):
/// • kanały 1–20: napięcie DC/AC, rezystancja 2W/4W, częstotliwość/okres,
///   temperatura (TC / RTD / termistor) — każdy kanał niezależnie,
/// • kanały 21–22: wyłącznie pomiar prądu DC/AC (wbudowane boczniki),
/// • pomiar 4-przewodowy (FRES / RTD 4W): kanał źródłowy musi być w zakresie 1–10;
///   przyrząd automatycznie paruje go z kanałem n+10 (11–20), który NIE może być
///   wtedy osobno na liście skanu,
/// • wbudowane złącze odniesienia termopary (isothermal block) dla pomiarów TC.
/// </summary>
public sealed class Card34901A : CardBase
{
    public const int MuxChannelCount = 20;

    public Card34901A(int slot) : base(slot) { }

    public override string Model => "34901A";
    public override string DisplayName => "34901A — multiplekser 20-kanałowy";

    /// <summary>Kanał 1–20 (multiplekser) lub 21–22 (prąd).</summary>
    public bool IsValidChannel(int ch1Based) => ch1Based is >= 1 and <= 22;

    /// <summary>Kanały 21 i 22 obsługują wyłącznie pomiar prądu.</summary>
    public bool IsCurrentChannel(int ch1Based) => ch1Based is 21 or 22;

    /// <summary>
    /// Sprawdza, czy konfiguracja kanału jest dopuszczalna dla 34901A.
    /// Rzuca <see cref="InvalidOperationException"/> z czytelnym komunikatem (PL).
    /// </summary>
    public void Validate(ChannelMeasurement m)
    {
        int ch = m.Channel - Slot; // numer kanału 1-bazowy w obrębie karty

        if (!IsValidChannel(ch))
            throw new InvalidOperationException(
                $"Kanał {m.Channel}: karta 34901A obsługuje kanały {Slot + 1}..{Slot + 20} " +
                $"(oraz {Slot + 21}/{Slot + 22} dla prądu).");

        bool isCurrent = m.Function is MuxFunction.CURR_DC or MuxFunction.CURR_AC;

        if (isCurrent && !IsCurrentChannel(ch))
            throw new InvalidOperationException(
                $"Pomiar prądu na 34901A jest możliwy tylko na kanałach {Slot + 21} i {Slot + 22}.");

        if (!isCurrent && IsCurrentChannel(ch))
            throw new InvalidOperationException(
                $"Kanały {Slot + 21}/{Slot + 22} służą wyłącznie do pomiaru prądu.");

        bool is4Wire = m.Function is MuxFunction.OHM4W or MuxFunction.TEMP_RTD4W;

        if (is4Wire && ch is < 1 or > 10)
            throw new InvalidOperationException(
                $"Pomiar 4-przewodowy wymaga kanału źródłowego {Slot + 1}..{Slot + 10} " +
                $"(parowany automatycznie z {Slot + 11}..{Slot + 20}).");
    }

    /// <summary>
    /// Kanał odniesienia (sense) dla 4-przewodowego kanału źródłowego (n → n+10).
    /// </summary>
    public int PairedSenseChannel(int sourceAbsChannel) => sourceAbsChannel + 10;
}
