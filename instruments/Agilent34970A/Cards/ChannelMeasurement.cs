using System.Globalization;

namespace Agilent34970A.Cards;

/// <summary>
/// Funkcja pomiarowa pojedynczego kanału multipleksera (34901A / 34902A).
/// Każdy kanał MUX może mieć własną, niezależną funkcję — dzięki temu jeden
/// skan może mierzyć np. 2× napięcie DC i 1× temperaturę RTD 4-przewodową naraz.
/// </summary>
public enum MuxFunction
{
    VDC,
    VAC,
    OHM2W,
    OHM4W,
    CURR_DC,
    CURR_AC,
    FREQ,
    PERIOD,
    TEMP_TC,
    TEMP_RTD,
    TEMP_RTD4W,
    TEMP_THERM
}

/// <summary>Typ termoelementu dla pomiaru TEMP_TC.</summary>
public enum TcType { B, E, J, K, N, R, S, T }

/// <summary>
/// Konfiguracja pomiaru jednego kanału MUX: numer kanału (SCPI, np. 101),
/// funkcja, zakres oraz parametry szczegółowe (typ TC, α RTD, typ termistora).
/// Klasa generuje poprawną komendę CONF zgodną z zasadami 34970A.
/// </summary>
public sealed class ChannelMeasurement
{
    /// <summary>Bezwzględny numer kanału SCPI, np. 101, 215, 320.</summary>
    public int Channel { get; set; }

    public MuxFunction Function { get; set; } = MuxFunction.VDC;

    /// <summary>Zakres: "AUTO" albo wartość liczbowa (np. "10", "0.1").</summary>
    public string Range { get; set; } = "AUTO";

    /// <summary>Rozdzielczość dla funkcji DC: "DEF" albo wartość (np. "MAX").</summary>
    public string Resolution { get; set; } = "DEF";

    /// <summary>Typ termoelementu dla TEMP_TC.</summary>
    public TcType TcType { get; set; } = TcType.K;

    /// <summary>Współczynnik α RTD: 85 (=0.00385, PT100) lub 91 (=0.00391).</summary>
    public int RtdAlpha { get; set; } = 85;

    /// <summary>Rezystancja nominalna termistora: 2200, 5000 lub 10000 Ω.</summary>
    public int ThermistorType { get; set; } = 5000;

    /// <summary>
    /// Integracja w cyklach sieci (NPLC): "" = bez zmiany; inaczej 0.02/0.2/1/2/10/20/100/MIN/MAX.
    /// Dotyczy tylko VDC / 2W / 4W / prądu DC.
    /// </summary>
    public string Nplc { get; set; } = "";

    /// <summary>Skalowanie Mx+B (CALCulate:SCALe).</summary>
    public bool ScaleEnabled { get; set; }
    public double ScaleGain { get; set; } = 1.0;   // M
    public double ScaleOffset { get; set; } = 0.0; // B
    public string ScaleUnit { get; set; } = "";    // do 3 znaków, np. "PSI"

    public ChannelMeasurement() { }

    public ChannelMeasurement(int channel, MuxFunction function, string range = "AUTO")
    {
        Channel = channel;
        Function = function;
        Range = range;
    }

    /// <summary>
    /// W komendach CONFigure zakres podaje się jako wartość liczbową albo
    /// MIN/MAX/DEF; autozakres wybiera słowo kluczowe DEF (tak jak w sterowniku 34401A).
    /// </summary>
    private string R =>
        string.IsNullOrWhiteSpace(Range) || Range.Trim().Equals("AUTO", StringComparison.OrdinalIgnoreCase)
            ? "DEF"
            : Range.Trim();

    /// <summary>
    /// Buduje komendę CONFigure dla tego kanału. Wywoływana per kanał, dzięki czemu
    /// każdy kanał skanu zachowuje własną, niezależną konfigurację.
    /// </summary>
    public string BuildConfCommand() => Function switch
    {
        MuxFunction.VDC        => $"CONF:VOLT:DC {R},{Resolution},(@{Channel})",
        MuxFunction.VAC        => $"CONF:VOLT:AC {R},(@{Channel})",
        MuxFunction.OHM2W      => $"CONF:RES {R},{Resolution},(@{Channel})",
        MuxFunction.OHM4W      => $"CONF:FRES {R},{Resolution},(@{Channel})",
        MuxFunction.CURR_DC    => $"CONF:CURR:DC {R},{Resolution},(@{Channel})",
        MuxFunction.CURR_AC    => $"CONF:CURR:AC {R},(@{Channel})",
        MuxFunction.FREQ       => $"CONF:FREQ {R},(@{Channel})",
        MuxFunction.PERIOD     => $"CONF:PER {R},(@{Channel})",
        MuxFunction.TEMP_TC    => $"CONF:TEMP TC,{TcType},(@{Channel})",
        MuxFunction.TEMP_RTD   => $"CONF:TEMP RTD,{RtdAlpha},(@{Channel})",
        MuxFunction.TEMP_RTD4W => $"CONF:TEMP FRTD,{RtdAlpha},(@{Channel})",
        MuxFunction.TEMP_THERM => $"CONF:TEMP THER,{ThermistorType},(@{Channel})",
        _ => throw new ArgumentOutOfRangeException(nameof(Function), Function, "Nieznana funkcja pomiaru.")
    };

    /// <summary>
    /// Komenda ustawiająca NPLC dla tego kanału (lub null, jeśli funkcja nie obsługuje NPLC
    /// albo NPLC nie zostało podane).
    /// </summary>
    public string? BuildNplcCommand()
    {
        if (string.IsNullOrWhiteSpace(Nplc)) return null;
        string? sub = Function switch
        {
            MuxFunction.VDC     => "VOLT:DC",
            MuxFunction.OHM2W   => "RES",
            MuxFunction.OHM4W   => "FRES",
            MuxFunction.CURR_DC => "CURR:DC",
            _ => null
        };
        return sub == null ? null : $"SENS:{sub}:NPLC {Nplc.Trim()},(@{Channel})";
    }

    /// <summary>Komendy skalowania Mx+B (CALCulate:SCALe) dla tego kanału.</summary>
    public IEnumerable<string> BuildScalingCommands()
    {
        if (!ScaleEnabled)
        {
            yield return $"CALC:SCAL:STAT OFF,(@{Channel})";
            yield break;
        }

        var ci = CultureInfo.InvariantCulture;
        yield return $"CALC:SCAL:GAIN {ScaleGain.ToString("0.######", ci)},(@{Channel})";
        yield return $"CALC:SCAL:OFFS {ScaleOffset.ToString("0.######", ci)},(@{Channel})";
        if (!string.IsNullOrWhiteSpace(ScaleUnit))
            yield return $"CALC:SCAL:UNIT \"{ScaleUnit.Trim()}\",(@{Channel})";
        yield return $"CALC:SCAL:STAT ON,(@{Channel})";
    }

    public string Unit => Function switch
    {
        MuxFunction.VDC or MuxFunction.VAC => "V",
        MuxFunction.OHM2W or MuxFunction.OHM4W => "Ω",
        MuxFunction.CURR_DC or MuxFunction.CURR_AC => "A",
        MuxFunction.FREQ => "Hz",
        MuxFunction.PERIOD => "s",
        MuxFunction.TEMP_TC or MuxFunction.TEMP_RTD or MuxFunction.TEMP_RTD4W or MuxFunction.TEMP_THERM => "°C",
        _ => ""
    };

    /// <summary>
    /// Tworzy konfigurację kanału na podstawie pól z UI / sekwencji.
    /// <paramref name="funcName"/> akceptuje aliasy (VDC, RES, FRES, IDC, RTD4W, TC, …).
    /// <paramref name="param"/> to typ TC (litera), α RTD (85/91) lub typ termistora.
    /// </summary>
    public static ChannelMeasurement FromUi(int channel, string funcName, string? range, string? param)
    {
        var m = new ChannelMeasurement
        {
            Channel = channel,
            Range = string.IsNullOrWhiteSpace(range) ? "AUTO" : range!.Trim()
        };

        param = param?.Trim() ?? "";

        switch ((funcName ?? "").Trim().ToUpperInvariant())
        {
            case "VDC": m.Function = MuxFunction.VDC; break;
            case "VAC": m.Function = MuxFunction.VAC; break;
            case "OHM2W": case "RES": m.Function = MuxFunction.OHM2W; break;
            case "OHM4W": case "FRES": m.Function = MuxFunction.OHM4W; break;
            case "IDC": case "CURR_DC": case "CURRDC": m.Function = MuxFunction.CURR_DC; break;
            case "IAC": case "CURR_AC": case "CURRAC": m.Function = MuxFunction.CURR_AC; break;
            case "FREQ": m.Function = MuxFunction.FREQ; break;
            case "PER": case "PERIOD": m.Function = MuxFunction.PERIOD; break;

            case "TC": case "TEMP_TC":
                m.Function = MuxFunction.TEMP_TC;
                if (Enum.TryParse<TcType>(param, ignoreCase: true, out var tc)) m.TcType = tc;
                break;

            case "RTD": case "RTD2W": case "TEMP_RTD":
                m.Function = MuxFunction.TEMP_RTD;
                if (int.TryParse(param, out var a1) && a1 > 0) m.RtdAlpha = a1;
                break;

            case "RTD4W": case "FRTD": case "TEMP_RTD4W":
                m.Function = MuxFunction.TEMP_RTD4W;
                if (int.TryParse(param, out var a2) && a2 > 0) m.RtdAlpha = a2;
                break;

            case "THERM": case "TEMP_THERM":
                m.Function = MuxFunction.TEMP_THERM;
                if (int.TryParse(param, out var th) && th > 0) m.ThermistorType = th;
                break;

            default: m.Function = MuxFunction.VDC; break;
        }

        return m;
    }

    public override string ToString() =>
        $"@{Channel} {Function} {Range}".Trim();
}
