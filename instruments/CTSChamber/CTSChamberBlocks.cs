using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace CTSChamber;

// ── CTS_SetTemperature ────────────────────────────────────────────────────────

public class CTS_SetTemperature : SequenceBlockBase
{
    public override string BlockType   => "CTS_SetTemperature";
    public override string DisplayName => "CTS: Ustaw temperaturę";
    public override string Description => "Ustawia temperaturę zadaną komory CTS (−75 … 185 °C)";
    public override Color  BlockColor  => Color.FromRgb(0xCC, 0x55, 0x00);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("Temperature", "Temperatura [°C]", 25.0),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name = GetPropStr("InstrumentName");
        double temp = GetProp<double>("Temperature", 25.0);
        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");
        try
        {
            await cts.SetTemperatureAsync(temp);
            context.Log?.Invoke($"CTS SetTemp: {temp:F1} °C");
            return BlockExecutionResult.Ok(NextBlockId, temp);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetTemperature: {ex.Message}"); }
    }

    static CTS_SetTemperature() =>
        BlockRegistry.Register("CTS_SetTemperature", () => new CTS_SetTemperature());
}

// ── CTS_SetRamp ───────────────────────────────────────────────────────────────

public class CTS_SetRamp : SequenceBlockBase
{
    public override string BlockType   => "CTS_SetRamp";
    public override string DisplayName => "CTS: Ustaw gradient";
    public override string Description => "Ustawia gradient wzrostu i spadku temperatury [K/min]";
    public override Color  BlockColor  => Color.FromRgb(0x00, 0x88, 0xCC);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("RampUp",   "Gradient wzrostu [K/min]", 5.0),
        BlockPropertyDefinition.Number("RampDown", "Gradient spadku [K/min]",  5.0),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name     = GetPropStr("InstrumentName");
        double rampUp   = GetProp<double>("RampUp",   5.0);
        double rampDown = GetProp<double>("RampDown", 5.0);
        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");
        try
        {
            await cts.SetRampUpAsync(rampUp);
            await cts.SetRampDownAsync(rampDown);
            context.Log?.Invoke($"CTS SetRamp: wzrost={rampUp:F1} K/min, spadek={rampDown:F1} K/min");
            return BlockExecutionResult.Ok(NextBlockId, rampUp);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetRamp: {ex.Message}"); }
    }

    static CTS_SetRamp() =>
        BlockRegistry.Register("CTS_SetRamp", () => new CTS_SetRamp());
}

// ── CTS_ChamberStart ──────────────────────────────────────────────────────────

public class CTS_ChamberStart : SequenceBlockBase
{
    public override string BlockType   => "CTS_ChamberStart";
    public override string DisplayName => "CTS: Uruchom komorę";
    public override string Description => "Uruchamia komorę CTS (s1 1)";
    public override Color  BlockColor  => Color.FromRgb(0x00, 0xAA, 0x44);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");
        try
        {
            await cts.ChamberStartAsync();
            context.Log?.Invoke("CTS: Komora uruchomiona");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd ChamberStart: {ex.Message}"); }
    }

    static CTS_ChamberStart() =>
        BlockRegistry.Register("CTS_ChamberStart", () => new CTS_ChamberStart());
}

// ── CTS_ChamberStop ───────────────────────────────────────────────────────────

public class CTS_ChamberStop : SequenceBlockBase
{
    public override string BlockType   => "CTS_ChamberStop";
    public override string DisplayName => "CTS: Zatrzymaj komorę";
    public override string Description => "Zatrzymuje komorę CTS (s1 0)";
    public override Color  BlockColor  => Color.FromRgb(0xCC, 0x22, 0x22);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");
        try
        {
            await cts.ChamberStopAsync();
            context.Log?.Invoke("CTS: Komora zatrzymana");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd ChamberStop: {ex.Message}"); }
    }

    static CTS_ChamberStop() =>
        BlockRegistry.Register("CTS_ChamberStop", () => new CTS_ChamberStop());
}

// ── CTS_ChamberPause ──────────────────────────────────────────────────────────

public class CTS_ChamberPause : SequenceBlockBase
{
    public override string BlockType   => "CTS_ChamberPause";
    public override string DisplayName => "CTS: Wstrzymaj komorę";
    public override string Description => "Wstrzymuje pracę komory CTS (s3 0) / wznawia (s3 1)";
    public override Color  BlockColor  => Color.FromRgb(0xCC, 0x88, 0x00);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Action", "Akcja", new List<string> { "Pauza", "Wznów" }, "Pauza"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name   = GetPropStr("InstrumentName");
        string action = GetPropStr("Action", "Pauza");
        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");
        try
        {
            if (action == "Wznów")
            {
                await cts.ChamberResumeAsync();
                context.Log?.Invoke("CTS: Komora wznowiona");
            }
            else
            {
                await cts.ChamberPauseAsync();
                context.Log?.Invoke("CTS: Komora wstrzymana");
            }
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd ChamberPause: {ex.Message}"); }
    }

    static CTS_ChamberPause() =>
        BlockRegistry.Register("CTS_ChamberPause", () => new CTS_ChamberPause());
}

// ── CTS_ReadTemperature ───────────────────────────────────────────────────────

public class CTS_ReadTemperature : SequenceBlockBase
{
    public override string BlockType   => "CTS_ReadTemperature";
    public override string DisplayName => "CTS: Odczytaj temperaturę";
    public override string Description => "Odczytuje aktualną i zadaną temperaturę komory CTS";
    public override Color  BlockColor  => Color.FromRgb(0x00, 0x77, 0xCC);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Variable("OutputActual",   "Zmienna — temperatura aktualna", "temp_actual"),
        BlockPropertyDefinition.Variable("OutputSetpoint", "Zmienna — temperatura zadana",   "temp_setpoint"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name        = GetPropStr("InstrumentName");
        string varActual   = GetPropStr("OutputActual",   "temp_actual");
        string varSetpoint = GetPropStr("OutputSetpoint", "temp_setpoint");
        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");
        try
        {
            var (actual, setpoint) = await cts.ReadTemperatureAsync();
            if (!string.IsNullOrWhiteSpace(varActual))   context.Variables[varActual]   = actual;
            if (!string.IsNullOrWhiteSpace(varSetpoint)) context.Variables[varSetpoint] = setpoint;
            context.Log?.Invoke($"CTS ReadTemp: aktualna={actual:F1} °C, zadana={setpoint:F1} °C");
            return BlockExecutionResult.Ok(NextBlockId, actual);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd ReadTemperature: {ex.Message}"); }
    }

    static CTS_ReadTemperature() =>
        BlockRegistry.Register("CTS_ReadTemperature", () => new CTS_ReadTemperature());
}

// ── CTS_WaitForTemperature ────────────────────────────────────────────────────

public class CTS_WaitForTemperature : SequenceBlockBase
{
    public override string BlockType   => "CTS_WaitForTemperature";
    public override string DisplayName => "CTS: Czekaj na temperaturę";
    public override string Description => "Czeka, aż temperatura aktualna osiągnie zadaną z podaną tolerancją";
    public override Color  BlockColor  => Color.FromRgb(0x55, 0x00, 0xCC);
    public override string Category    => "CTSChamber";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("Tolerance", "Tolerancja [°C]",           0.5),
        BlockPropertyDefinition.Number("TimeoutS",  "Limit czasu [s]",           3600.0),
        BlockPropertyDefinition.Number("PollMs",    "Interwał sprawdzania [ms]", 5000.0),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name      = GetPropStr("InstrumentName");
        double tolerance = GetProp<double>("Tolerance", 0.5);
        double timeoutS  = GetProp<double>("TimeoutS",  3600.0);
        int    pollMs    = (int)GetProp<double>("PollMs", 5000.0);

        if (!context.Instruments.TryGetValue(name, out var drv))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {name}");
        if (drv is not CTSChamberDriver cts)
            return BlockExecutionResult.Fail($"'{name}' nie jest CTSChamberDriver");

        var deadline = DateTime.Now.AddSeconds(timeoutS);
        while (DateTime.Now < deadline)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return BlockExecutionResult.Fail("Anulowano przez użytkownika");
            try
            {
                var (actual, setpoint) = await cts.ReadTemperatureAsync();
                context.Log?.Invoke($"CTS WaitTemp: {actual:F1} / {setpoint:F1} °C  (tolerancja ±{tolerance:F1})");
                if (Math.Abs(actual - setpoint) <= tolerance)
                    return BlockExecutionResult.Ok(NextBlockId, actual);
            }
            catch (Exception ex)
            {
                context.Log?.Invoke($"CTS WaitTemp błąd odczytu: {ex.Message}");
            }

            try { await Task.Delay(pollMs, context.CancellationToken); }
            catch (OperationCanceledException) { return BlockExecutionResult.Fail("Anulowano"); }
        }
        return BlockExecutionResult.Fail($"Timeout: temperatura nie osiągnęła wartości zadanej w {timeoutS} s");
    }

    static CTS_WaitForTemperature() =>
        BlockRegistry.Register("CTS_WaitForTemperature", () => new CTS_WaitForTemperature());
}

// ── Registration helper ───────────────────────────────────────────────────────

public static class CTSChamberBlocks
{
    public static void RegisterAll()
    {
        _ = new CTS_SetTemperature();
        _ = new CTS_SetRamp();
        _ = new CTS_ChamberStart();
        _ = new CTS_ChamberStop();
        _ = new CTS_ChamberPause();
        _ = new CTS_ReadTemperature();
        _ = new CTS_WaitForTemperature();
    }
}
