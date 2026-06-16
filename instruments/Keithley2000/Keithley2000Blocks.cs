using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace Keithley2000;

// ── K2000_MeasureDCV ─────────────────────────────────────────────────────────

public class K2000_MeasureDCV : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureDCV";
    public override string DisplayName => "K2000 Zmierz DCV";
    public override string Description => "Pomiar napięcia stałego (DCV) multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0xD4, 0x86, 0x0A);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Range", "Zakres", new List<string> { "AUTO", "100mV", "1V", "10V", "100V", "1000V" }, "AUTO"),
        BlockPropertyDefinition.Combo("NPLC", "NPLC", new List<string> { "0.02", "0.2", "1", "10", "100" }, "1"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "voltage"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string range = GetPropStr("Range", "AUTO");
        string nplcStr = GetPropStr("NPLC", "1");
        string outVar = GetPropStr("OutputVariable", "voltage");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        try
        {
            double value = await dmm.MeasureDCV(MapRange(range), nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 DCV: {value:G6} V → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru DCV: {ex.Message}"); }
    }

    private static string MapRange(string r) => r switch
    {
        "100mV" => "0.1", "1V" => "1", "10V" => "10", "100V" => "100", "1000V" => "1000", _ => "DEF"
    };

    static K2000_MeasureDCV() => BlockRegistry.Register("K2000_MeasureDCV", () => new K2000_MeasureDCV());
}

// ── K2000_MeasureACV ─────────────────────────────────────────────────────────

public class K2000_MeasureACV : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureACV";
    public override string DisplayName => "K2000 Zmierz ACV";
    public override string Description => "Pomiar napięcia przemiennego (ACV) multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0xCB, 0x68, 0x00);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Range", "Zakres", new List<string> { "AUTO", "100mV", "1V", "10V", "100V", "750V" }, "AUTO"),
        BlockPropertyDefinition.Combo("NPLC", "NPLC", new List<string> { "0.02", "0.2", "1", "10", "100" }, "1"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "voltage_ac"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string range = GetPropStr("Range", "AUTO");
        string nplcStr = GetPropStr("NPLC", "1");
        string outVar = GetPropStr("OutputVariable", "voltage_ac");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        try
        {
            double value = await dmm.MeasureACV(MapRange(range), nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 ACV: {value:G6} V AC → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru ACV: {ex.Message}"); }
    }

    private static string MapRange(string r) => r switch
    {
        "100mV" => "0.1", "1V" => "1", "10V" => "10", "100V" => "100", "750V" => "750", _ => "DEF"
    };

    static K2000_MeasureACV() => BlockRegistry.Register("K2000_MeasureACV", () => new K2000_MeasureACV());
}

// ── K2000_MeasureResistance ───────────────────────────────────────────────────

public class K2000_MeasureResistance : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureResistance";
    public override string DisplayName => "K2000 Zmierz Rezystancję";
    public override string Description => "Pomiar rezystancji (2W lub 4W) multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0x79, 0x55, 0x48);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Range", "Zakres", new List<string> { "AUTO", "100Ω", "1kΩ", "10kΩ", "100kΩ", "1MΩ", "10MΩ", "100MΩ" }, "AUTO"),
        BlockPropertyDefinition.Combo("NPLC", "NPLC", new List<string> { "0.02", "0.2", "1", "10", "100" }, "1"),
        BlockPropertyDefinition.Combo("WireMode", "Tryb pomiaru", new List<string> { "2W", "4W" }, "2W"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "resistance"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string range = GetPropStr("Range", "AUTO");
        string nplcStr = GetPropStr("NPLC", "1");
        string wireMode = GetPropStr("WireMode", "2W");
        string outVar = GetPropStr("OutputVariable", "resistance");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;
        string scpiRange = MapRange(range);

        try
        {
            double value = wireMode == "4W"
                ? await dmm.MeasureResistance4W(scpiRange, nplc)
                : await dmm.MeasureResistance2W(scpiRange, nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 RES ({wireMode}): {value:G6} Ω → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru rezystancji: {ex.Message}"); }
    }

    private static string MapRange(string r) => r switch
    {
        "100Ω" => "100", "1kΩ" => "1E3", "10kΩ" => "10E3",
        "100kΩ" => "100E3", "1MΩ" => "1E6", "10MΩ" => "10E6", "100MΩ" => "100E6", _ => "DEF"
    };

    static K2000_MeasureResistance() => BlockRegistry.Register("K2000_MeasureResistance", () => new K2000_MeasureResistance());
}

// ── K2000_MeasureCurrent ──────────────────────────────────────────────────────

public class K2000_MeasureCurrent : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureCurrent";
    public override string DisplayName => "K2000 Zmierz Prąd";
    public override string Description => "Pomiar prądu (DC lub AC) multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0xB8, 0x86, 0x0B);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Range", "Zakres", new List<string> { "AUTO", "10mA", "100mA", "1A", "3A" }, "AUTO"),
        BlockPropertyDefinition.Combo("NPLC", "NPLC", new List<string> { "0.02", "0.2", "1", "10", "100" }, "1"),
        BlockPropertyDefinition.Check("ACMode", "Tryb AC", false),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "current"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string range = GetPropStr("Range", "AUTO");
        string nplcStr = GetPropStr("NPLC", "1");
        bool acMode = GetProp<bool>("ACMode", false);
        string outVar = GetPropStr("OutputVariable", "current");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;
        string unit = acMode ? "A AC" : "A";

        try
        {
            double value = acMode
                ? await dmm.MeasureACI(MapRange(range), nplc)
                : await dmm.MeasureDCI(MapRange(range), nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 {(acMode ? "ACI" : "DCI")}: {value:G6} {unit} → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru prądu: {ex.Message}"); }
    }

    private static string MapRange(string r) => r switch
    {
        "10mA" => "0.01", "100mA" => "0.1", "1A" => "1", "3A" => "3", _ => "DEF"
    };

    static K2000_MeasureCurrent() => BlockRegistry.Register("K2000_MeasureCurrent", () => new K2000_MeasureCurrent());
}

// ── K2000_MeasureFrequency ────────────────────────────────────────────────────

public class K2000_MeasureFrequency : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureFrequency";
    public override string DisplayName => "K2000 Zmierz Częstotliwość";
    public override string Description => "Pomiar częstotliwości multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0x8E, 0x44, 0xAD);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "frequency"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar = GetPropStr("OutputVariable", "frequency");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        try
        {
            double value = await dmm.MeasureFrequency();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 FREQ: {value:G6} Hz → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru częstotliwości: {ex.Message}"); }
    }

    static K2000_MeasureFrequency() => BlockRegistry.Register("K2000_MeasureFrequency", () => new K2000_MeasureFrequency());
}

// ── K2000_MeasurePeriod ───────────────────────────────────────────────────────

public class K2000_MeasurePeriod : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasurePeriod";
    public override string DisplayName => "K2000 Zmierz Okres";
    public override string Description => "Pomiar okresu sygnału multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0x6D, 0x5C, 0x4A);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "period"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar = GetPropStr("OutputVariable", "period");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        try
        {
            double value = await dmm.MeasurePeriod();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 PERIOD: {value:G6} s → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru okresu: {ex.Message}"); }
    }

    static K2000_MeasurePeriod() => BlockRegistry.Register("K2000_MeasurePeriod", () => new K2000_MeasurePeriod());
}

// ── K2000_MeasureDiode ────────────────────────────────────────────────────────

public class K2000_MeasureDiode : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureDiode";
    public override string DisplayName => "K2000 Test Diody";
    public override string Description => "Test diody (napięcie przewodzenia) multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0x0E, 0x7A, 0x65);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "diode_v"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar = GetPropStr("OutputVariable", "diode_v");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        try
        {
            double value = await dmm.MeasureDiode();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 DIODE: {value:G6} V → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd testu diody: {ex.Message}"); }
    }

    static K2000_MeasureDiode() => BlockRegistry.Register("K2000_MeasureDiode", () => new K2000_MeasureDiode());
}

// ── K2000_MeasureContinuity ───────────────────────────────────────────────────

public class K2000_MeasureContinuity : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureContinuity";
    public override string DisplayName => "K2000 Test Ciągłości";
    public override string Description => "Test ciągłości obwodu multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0xA9, 0x44, 0x00);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "continuity_ohm"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar = GetPropStr("OutputVariable", "continuity_ohm");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        try
        {
            double value = await dmm.MeasureContinuity();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 CONT: {value:G6} Ω → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd testu ciągłości: {ex.Message}"); }
    }

    static K2000_MeasureContinuity() => BlockRegistry.Register("K2000_MeasureContinuity", () => new K2000_MeasureContinuity());
}

// ── K2000_MeasureTemperature ──────────────────────────────────────────────────

public class K2000_MeasureTemperature : SequenceBlockBase
{
    public override string BlockType => "K2000_MeasureTemperature";
    public override string DisplayName => "K2000 Zmierz Temperaturę";
    public override string Description => "Pomiar temperatury (termoelementem J/K/T/E) multimetrem Keithley 2000";
    public override Color BlockColor => Color.FromRgb(0xC0, 0x39, 0x2B);
    public override string Category => "Keithley2000";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("TCType", "Typ TC", new List<string> { "J", "K", "T", "E", "N", "R", "S", "B" }, "K"),
        BlockPropertyDefinition.Combo("NPLC", "NPLC", new List<string> { "0.02", "0.2", "1", "10", "100" }, "1"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "temperature"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string tcType = GetPropStr("TCType", "K");
        string nplcStr = GetPropStr("NPLC", "1");
        string outVar = GetPropStr("OutputVariable", "temperature");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not Keithley2000Driver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest Keithley2000Driver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        try
        {
            double value = await dmm.MeasureTemperature(tcType, nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"K2000 TEMP TC-{tcType}: {value:F2} °C → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd pomiaru temperatury: {ex.Message}"); }
    }

    static K2000_MeasureTemperature() => BlockRegistry.Register("K2000_MeasureTemperature", () => new K2000_MeasureTemperature());
}

// ── Registration ─────────────────────────────────────────────────────────────

public static class Keithley2000Blocks
{
    public static void RegisterAll()
    {
        _ = new K2000_MeasureDCV();
        _ = new K2000_MeasureACV();
        _ = new K2000_MeasureResistance();
        _ = new K2000_MeasureCurrent();
        _ = new K2000_MeasureFrequency();
        _ = new K2000_MeasurePeriod();
        _ = new K2000_MeasureDiode();
        _ = new K2000_MeasureContinuity();
        _ = new K2000_MeasureTemperature();
    }
}
