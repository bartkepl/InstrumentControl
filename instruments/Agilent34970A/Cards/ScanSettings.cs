namespace Agilent34970A.Cards;

/// <summary>
/// Ustawienia przebiegu skanu 34970A: źródło wyzwalania, odstęp (timer),
/// liczba przebiegów oraz opóźnienie kanału. Mapuje się na komendy
/// TRIGger:SOURce / TRIGger:TIMer / TRIGger:COUNt / ROUTe:CHANnel:DELay.
/// </summary>
public sealed class ScanSettings
{
    /// <summary>IMM | TIMER | EXT | BUS | ALARM.</summary>
    public string TriggerSource { get; set; } = "IMM";

    /// <summary>Odstęp między przebiegami w sekundach (gdy TriggerSource = TIMER).</summary>
    public double TimerInterval { get; set; } = 0;

    /// <summary>Liczba przebiegów skanu (TRIG:COUN).</summary>
    public int ScanCount { get; set; } = 1;

    /// <summary>Opóźnienie kanału w sekundach; wartość &lt; 0 oznacza „nie ustawiaj" (auto).</summary>
    public double ChannelDelay { get; set; } = -1;

    /// <summary>Mapuje przyjazną nazwę źródła na słowo kluczowe SCPI.</summary>
    public string ScpiTriggerSource => (TriggerSource ?? "IMM").Trim().ToUpperInvariant() switch
    {
        "TIMER" => "TIM",
        "EXT" => "EXT",
        "BUS" => "BUS",
        "ALARM" => "ALAR",
        _ => "IMM"
    };

    public bool IsTimer => ScpiTriggerSource == "TIM";
}
