using System.Globalization;

namespace Agilent34970A.Cards;

/// <summary>
/// Agilent 34907A — moduł wielofunkcyjny. Jedna karta łączy trzy niezależne funkcje,
/// dlatego w architekturze „per karta" obsługuje je wszystkie:
///
/// Stałe przypisanie kanałów (kluczowe — nie wolno mylić!):
///   • kanał s01 — port cyfrowy DIO 1 (8 bitów),
///   • kanał s02 — port cyfrowy DIO 2 (8 bitów),
///   • kanał s03 — totalizator (licznik impulsów, 26-bit, do 100 kHz),
///   • kanał s04 — wyjście analogowe DAC 1 (±12 V),
///   • kanał s05 — wyjście analogowe DAC 2 (±12 V).
///
/// Wcześniejszy błąd 305 wynikał z wysyłania SOURce:VOLTage na kanały s01/s02
/// (porty cyfrowe) zamiast na s04/s05 (DAC).
/// </summary>
public sealed class Card34907A : CardBase
{
    /// <summary>Zakres napięcia DAC: ±12 V.</summary>
    public const double DacMin = -12.0;
    public const double DacMax = 12.0;

    public Card34907A(int slot) : base(slot) { }

    public override string Model => "34907A";
    public override string DisplayName => "34907A — moduł wielofunkcyjny (DAC + DIO + totalizator)";

    // ── Mapowanie kanałów ─────────────────────────────────────────────────────

    /// <summary>Kanał portu cyfrowego: port 1 → s01, port 2 → s02.</summary>
    public int DioChannel(int port) => Slot + (port == 2 ? 2 : 1);

    /// <summary>Kanał totalizatora: s03.</summary>
    public int TotalizerChannel => Slot + 3;

    /// <summary>Kanał DAC: DAC1 → s04, DAC2 → s05.</summary>
    public int DacChannel(int dac) => Slot + (dac == 2 ? 5 : 4);

    // ── Budowa komend SCPI ──────────────────────────────────────────────────────

    /// <summary>SOURce:VOLTage — ustawienie napięcia DAC (kanał s04/s05).</summary>
    public string BuildSetDacCommand(int dac, double voltage)
    {
        if (dac is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(dac), "Kanał DAC musi być 1 lub 2.");
        if (voltage < DacMin || voltage > DacMax)
            throw new ArgumentOutOfRangeException(nameof(voltage),
                $"Napięcie DAC {voltage} V poza zakresem {DacMin}..{DacMax} V.");

        string v = voltage.ToString("0.####", CultureInfo.InvariantCulture);
        return $"SOUR:VOLT {v},(@{DacChannel(dac)})";
    }

    /// <summary>Odczyt nastawy DAC (set-point).</summary>
    public string BuildReadDacCommand(int dac) => $"SOUR:VOLT? (@{DacChannel(dac)})";

    /// <summary>SOURce:DIGital:DATA:BYTE — zapis bajtu na port cyfrowy.</summary>
    public string BuildDigitalWriteCommand(int port, byte value) =>
        $"SOUR:DIG:DATA:BYTE {value},(@{DioChannel(port)})";

    /// <summary>SENSe:DIGital:DATA:BYTE? — odczyt bajtu z portu cyfrowego.</summary>
    public string BuildDigitalReadCommand(int port) =>
        $"SENS:DIG:DATA:BYTE? (@{DioChannel(port)})";

    /// <summary>MEASure:TOTalize? READ — odczyt licznika bez zerowania.</summary>
    public string BuildTotalizerReadCommand() => $"MEAS:TOT? READ,(@{TotalizerChannel})";

    /// <summary>SENSe:TOTalize:CLEar:IMMediate — wyzerowanie licznika.</summary>
    public string BuildTotalizerResetCommand() => $"SENS:TOT:CLE:IMM (@{TotalizerChannel})";
}
