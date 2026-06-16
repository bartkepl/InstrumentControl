using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace ItechIT6922B;

// ── IT6922B_SetVoltage ───────────────────────────────────────────────────────

public class IT6922B_SetVoltage : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_SetVoltage";
    public override string DisplayName  => "Ustaw Napięcie";
    public override string Description  => "Ustawia napięcie wyjściowe zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x66, 0xCC);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("Voltage", "Napięcie [V]", 5.0),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        double voltage   = GetProp<double>("Voltage", 5.0);

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            await psu.SetVoltageAsync(voltage);
            context.Log?.Invoke($"IT6922B: Napięcie → {voltage:F3} V");
            return BlockExecutionResult.Ok(NextBlockId, voltage);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetVoltage: {ex.Message}"); }
    }

    static IT6922B_SetVoltage() =>
        BlockRegistry.Register("IT6922B_SetVoltage", () => new IT6922B_SetVoltage());
}

// ── IT6922B_SetCurrent ───────────────────────────────────────────────────────

public class IT6922B_SetCurrent : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_SetCurrent";
    public override string DisplayName  => "Ustaw Limit Prądu";
    public override string Description  => "Ustawia limit prądu wyjściowego zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x99, 0xCC);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("Current", "Prąd [A]", 1.0),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        double current   = GetProp<double>("Current", 1.0);

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            await psu.SetCurrentLimitAsync(current);
            context.Log?.Invoke($"IT6922B: Limit prądu → {current:F3} A");
            return BlockExecutionResult.Ok(NextBlockId, current);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetCurrent: {ex.Message}"); }
    }

    static IT6922B_SetCurrent() =>
        BlockRegistry.Register("IT6922B_SetCurrent", () => new IT6922B_SetCurrent());
}

// ── IT6922B_SetOutput ────────────────────────────────────────────────────────

public class IT6922B_SetOutput : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_SetOutput";
    public override string DisplayName  => "Włącz/Wyłącz Wyjście";
    public override string Description  => "Włącza lub wyłącza wyjście zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x88, 0x44);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Check("Enable", "Włącz wyjście", true),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        bool   enable    = GetProp<bool>("Enable", true);

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            await psu.SetOutputEnabledAsync(enable);
            context.Log?.Invoke($"IT6922B: Wyjście {(enable ? "ON" : "OFF")}");
            return BlockExecutionResult.Ok(NextBlockId, enable);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetOutput: {ex.Message}"); }
    }

    static IT6922B_SetOutput() =>
        BlockRegistry.Register("IT6922B_SetOutput", () => new IT6922B_SetOutput());
}

// ── IT6922B_MeasureVoltage ───────────────────────────────────────────────────

public class IT6922B_MeasureVoltage : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_MeasureVoltage";
    public override string DisplayName  => "Zmierz Napięcie";
    public override string Description  => "Mierzy rzeczywiste napięcie wyjściowe zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x66, 0x00);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "voltage"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar    = GetPropStr("OutputVariable", "voltage");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            double v = await psu.MeasureVoltageAsync();
            context.SetVariable(outVar, v);
            context.Log?.Invoke($"IT6922B VOLT: {v:G6} V → '{outVar}'");
            context.AddResult(new MeasurementResult
            {
                Function = "VOLT", Unit = "V", Value = v,
                InstrumentName = instrName, ChannelId = "OUT1", ParameterName = outVar,
            });
            return BlockExecutionResult.Ok(NextBlockId, v);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MeasureVoltage: {ex.Message}"); }
    }

    static IT6922B_MeasureVoltage() =>
        BlockRegistry.Register("IT6922B_MeasureVoltage", () => new IT6922B_MeasureVoltage());
}

// ── IT6922B_MeasureCurrent ───────────────────────────────────────────────────

public class IT6922B_MeasureCurrent : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_MeasureCurrent";
    public override string DisplayName  => "Zmierz Prąd";
    public override string Description  => "Mierzy rzeczywisty prąd wyjściowy zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x44, 0x00);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "current"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar    = GetPropStr("OutputVariable", "current");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            double v = await psu.MeasureCurrentAsync();
            context.SetVariable(outVar, v);
            context.Log?.Invoke($"IT6922B CURR: {v:G6} A → '{outVar}'");
            context.AddResult(new MeasurementResult
            {
                Function = "CURR", Unit = "A", Value = v,
                InstrumentName = instrName, ChannelId = "OUT1", ParameterName = outVar,
            });
            return BlockExecutionResult.Ok(NextBlockId, v);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MeasureCurrent: {ex.Message}"); }
    }

    static IT6922B_MeasureCurrent() =>
        BlockRegistry.Register("IT6922B_MeasureCurrent", () => new IT6922B_MeasureCurrent());
}

// ── IT6922B_MeasurePower ─────────────────────────────────────────────────────

public class IT6922B_MeasurePower : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_MeasurePower";
    public override string DisplayName  => "Zmierz Moc";
    public override string Description  => "Mierzy moc wyjściową zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x88, 0x00);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "power"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string outVar    = GetPropStr("OutputVariable", "power");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            double v = await psu.MeasurePowerAsync();
            context.SetVariable(outVar, v);
            context.Log?.Invoke($"IT6922B POW: {v:G6} W → '{outVar}'");
            context.AddResult(new MeasurementResult
            {
                Function = "POW", Unit = "W", Value = v,
                InstrumentName = instrName, ChannelId = "OUT1", ParameterName = outVar,
            });
            return BlockExecutionResult.Ok(NextBlockId, v);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MeasurePower: {ex.Message}"); }
    }

    static IT6922B_MeasurePower() =>
        BlockRegistry.Register("IT6922B_MeasurePower", () => new IT6922B_MeasurePower());
}

// ── IT6922B_SetOVP ───────────────────────────────────────────────────────────

public class IT6922B_SetOVP : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_SetOVP";
    public override string DisplayName  => "Ustaw OVP";
    public override string Description  => "Ustawia próg i stan zabezpieczenia nadnapięciowego (OVP) zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0x99, 0x00, 0xCC);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("Level", "Próg OVP [V]", 65.0),
        BlockPropertyDefinition.Check("Enable", "Włącz OVP", true),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        double level     = GetProp<double>("Level", 65.0);
        bool   enable    = GetProp<bool>("Enable", true);

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            await psu.SetOvpLevelAsync(level);
            await psu.SetOvpEnabledAsync(enable);
            context.Log?.Invoke($"IT6922B OVP: {level:F2} V  {(enable ? "ON" : "OFF")}");
            return BlockExecutionResult.Ok(NextBlockId, level);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetOVP: {ex.Message}"); }
    }

    static IT6922B_SetOVP() =>
        BlockRegistry.Register("IT6922B_SetOVP", () => new IT6922B_SetOVP());
}

// ── IT6922B_SetOCP ───────────────────────────────────────────────────────────

public class IT6922B_SetOCP : SequenceBlockBase
{
    public override string BlockType    => "IT6922B_SetOCP";
    public override string DisplayName  => "Ustaw OCP";
    public override string Description  => "Ustawia próg i stan zabezpieczenia nadprądowego (OCP) zasilacza IT6922B";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x00, 0x66);
    public override string Category     => "ItechIT6922B";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("Level", "Próg OCP [A]", 5.5),
        BlockPropertyDefinition.Check("Enable", "Włącz OCP", true),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        double level     = GetProp<double>("Level", 5.5);
        bool   enable    = GetProp<bool>("Enable", true);

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not ItechIT6922BDriver psu)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest ItechIT6922BDriver");

        try
        {
            await psu.SetOcpLevelAsync(level);
            await psu.SetOcpEnabledAsync(enable);
            context.Log?.Invoke($"IT6922B OCP: {level:F2} A  {(enable ? "ON" : "OFF")}");
            return BlockExecutionResult.Ok(NextBlockId, level);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetOCP: {ex.Message}"); }
    }

    static IT6922B_SetOCP() =>
        BlockRegistry.Register("IT6922B_SetOCP", () => new IT6922B_SetOCP());
}

// ── Registration entry point ─────────────────────────────────────────────────

public static class ItechIT6922BBlocks
{
    public static void RegisterAll()
    {
        _ = new IT6922B_SetVoltage();
        _ = new IT6922B_SetCurrent();
        _ = new IT6922B_SetOutput();
        _ = new IT6922B_MeasureVoltage();
        _ = new IT6922B_MeasureCurrent();
        _ = new IT6922B_MeasurePower();
        _ = new IT6922B_SetOVP();
        _ = new IT6922B_SetOCP();
    }
}
