using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace HP34401A;

// ── HP34401A_MeasureDCV ──────────────────────────────────────────────────────

public class HP34401A_MeasureDCV : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureDCV";
    public override string DisplayName => "Zmierz DCV";
    public override string Description => "Pomiar napięcia stałego (DCV) multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0x29, 0x80, 0xB9);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        string scpiRange = MapRange(range);

        try
        {
            double value = await dmm.MeasureDCV(scpiRange, nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A DCV: {value:G6} V → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru DCV: {ex.Message}");
        }
    }

    private static string MapRange(string range) => range switch
    {
        "100mV" => "0.1",
        "1V"    => "1",
        "10V"   => "10",
        "100V"  => "100",
        "1000V" => "1000",
        _       => "DEF",
    };

    static HP34401A_MeasureDCV() =>
        BlockRegistry.Register("HP34401A_MeasureDCV", () => new HP34401A_MeasureDCV());
}

// ── HP34401A_MeasureACV ──────────────────────────────────────────────────────

public class HP34401A_MeasureACV : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureACV";
    public override string DisplayName => "Zmierz ACV";
    public override string Description => "Pomiar napięcia przemiennego (ACV) multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0x1A, 0xBC, 0x9C);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        string scpiRange = MapRange(range);

        try
        {
            double value = await dmm.MeasureACV(scpiRange, nplc);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A ACV: {value:G6} V AC → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru ACV: {ex.Message}");
        }
    }

    private static string MapRange(string range) => range switch
    {
        "100mV" => "0.1",
        "1V"    => "1",
        "10V"   => "10",
        "100V"  => "100",
        "750V"  => "750",
        _       => "DEF",
    };

    static HP34401A_MeasureACV() =>
        BlockRegistry.Register("HP34401A_MeasureACV", () => new HP34401A_MeasureACV());
}

// ── HP34401A_MeasureResistance ───────────────────────────────────────────────

public class HP34401A_MeasureResistance : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureResistance";
    public override string DisplayName => "Zmierz Rezystancję";
    public override string Description => "Pomiar rezystancji (2W lub 4W) multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0xE7, 0x4C, 0x3C);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        string scpiRange = MapRange(range);

        try
        {
            double value = wireMode == "4W"
                ? await dmm.MeasureResistance4W(scpiRange, nplc)
                : await dmm.MeasureResistance2W(scpiRange, nplc);

            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A RES ({wireMode}): {value:G6} Ω → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru rezystancji: {ex.Message}");
        }
    }

    private static string MapRange(string range) => range switch
    {
        "100Ω"   => "100",
        "1kΩ"    => "1E3",
        "10kΩ"   => "10E3",
        "100kΩ"  => "100E3",
        "1MΩ"    => "1E6",
        "10MΩ"   => "10E6",
        "100MΩ"  => "100E6",
        _        => "DEF",
    };

    static HP34401A_MeasureResistance() =>
        BlockRegistry.Register("HP34401A_MeasureResistance", () => new HP34401A_MeasureResistance());
}

// ── HP34401A_MeasureCurrent ──────────────────────────────────────────────────

public class HP34401A_MeasureCurrent : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureCurrent";
    public override string DisplayName => "Zmierz Prąd";
    public override string Description => "Pomiar prądu (DC lub AC) multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0xF3, 0x9C, 0x12);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        double nplc = double.TryParse(nplcStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : 1.0;

        string scpiRange = MapRange(range);
        string unit = acMode ? "A AC" : "A";

        try
        {
            double value = acMode
                ? await dmm.MeasureACI(scpiRange, nplc)
                : await dmm.MeasureDCI(scpiRange, nplc);

            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A {(acMode ? "ACI" : "DCI")}: {value:G6} {unit} → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru prądu: {ex.Message}");
        }
    }

    private static string MapRange(string range) => range switch
    {
        "10mA"  => "0.01",
        "100mA" => "0.1",
        "1A"    => "1",
        "3A"    => "3",
        _       => "DEF",
    };

    static HP34401A_MeasureCurrent() =>
        BlockRegistry.Register("HP34401A_MeasureCurrent", () => new HP34401A_MeasureCurrent());
}

// ── HP34401A_MeasureFrequency ────────────────────────────────────────────────

public class HP34401A_MeasureFrequency : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureFrequency";
    public override string DisplayName => "Zmierz Częstotliwość";
    public override string Description => "Pomiar częstotliwości multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0x9B, 0x59, 0xB6);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        try
        {
            double value = await dmm.MeasureFrequency();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A FREQ: {value:G6} Hz → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru częstotliwości: {ex.Message}");
        }
    }

    static HP34401A_MeasureFrequency() =>
        BlockRegistry.Register("HP34401A_MeasureFrequency", () => new HP34401A_MeasureFrequency());
}

// ── HP34401A_MeasurePeriod ───────────────────────────────────────────────────

public class HP34401A_MeasurePeriod : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasurePeriod";
    public override string DisplayName => "Zmierz Okres";
    public override string Description => "Pomiar okresu sygnału multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0x7F, 0x8C, 0x8D);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        try
        {
            double value = await dmm.MeasurePeriod();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A PERIOD: {value:G6} s → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru okresu: {ex.Message}");
        }
    }

    static HP34401A_MeasurePeriod() =>
        BlockRegistry.Register("HP34401A_MeasurePeriod", () => new HP34401A_MeasurePeriod());
}

// ── HP34401A_MeasureDiode ────────────────────────────────────────────────────

public class HP34401A_MeasureDiode : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureDiode";
    public override string DisplayName => "Test Diody";
    public override string Description => "Test diody (napięcie przewodzenia) multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0x16, 0xA0, 0x85);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        try
        {
            double value = await dmm.MeasureDiode();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A DIODE: {value:G6} V → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd testu diody: {ex.Message}");
        }
    }

    static HP34401A_MeasureDiode() =>
        BlockRegistry.Register("HP34401A_MeasureDiode", () => new HP34401A_MeasureDiode());
}

// ── HP34401A_MeasureContinuity ───────────────────────────────────────────────

public class HP34401A_MeasureContinuity : SequenceBlockBase
{
    public override string BlockType => "HP34401A_MeasureContinuity";
    public override string DisplayName => "Test Ciągłości";
    public override string Description => "Test ciągłości obwodu (próg ~10Ω) multimetrem HP 34401A";
    public override Color BlockColor => Color.FromRgb(0xD3, 0x54, 0x00);
    public override string Category => "HP34401A";

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

        if (driver is not HP34401ADriver dmm)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest HP34401ADriver");

        try
        {
            double value = await dmm.MeasureContinuity();
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"HP34401A CONT: {value:G6} Ω → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd testu ciągłości: {ex.Message}");
        }
    }

    static HP34401A_MeasureContinuity() =>
        BlockRegistry.Register("HP34401A_MeasureContinuity", () => new HP34401A_MeasureContinuity());
}

// ── Registration entry point ─────────────────────────────────────────────────

public static class HP34401ABlocks
{
    public static void RegisterAll()
    {
        // Force static constructors to run for each block type
        _ = new HP34401A_MeasureDCV();
        _ = new HP34401A_MeasureACV();
        _ = new HP34401A_MeasureResistance();
        _ = new HP34401A_MeasureCurrent();
        _ = new HP34401A_MeasureFrequency();
        _ = new HP34401A_MeasurePeriod();
        _ = new HP34401A_MeasureDiode();
        _ = new HP34401A_MeasureContinuity();
    }
}
