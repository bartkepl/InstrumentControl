using System.Globalization;
using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace RigolDS1000Z;

// ── Shared helpers ───────────────────────────────────────────────────────────

internal static class Ch
{
    internal static readonly List<string> Channels  = new() { "1", "2", "3", "4" };
    internal static readonly List<string> Couplings = new() { "DC", "AC", "GND" };
    internal static readonly List<string> Probes    = new()
        { "0.1", "0.2", "0.5", "1", "2", "5", "10", "20", "50", "100", "200", "500", "1000" };
    internal static readonly List<string> BWLimits  = new() { "OFF", "20M" };
    internal static readonly List<string> VScales   = new()
        { "0.001","0.002","0.005","0.01","0.02","0.05","0.1","0.2","0.5","1","2","5","10","20","50","100" };
    internal static readonly List<string> TScales   = new()
        { "5E-9","1E-8","2E-8","5E-8","1E-7","2E-7","5E-7","1E-6","2E-6","5E-6",
          "1E-5","2E-5","5E-5","1E-4","2E-4","5E-4","0.001","0.002","0.005",
          "0.01","0.02","0.05","0.1","0.2","0.5","1","2","5","10","20","50" };
}

// ── Plugin-local abstract base with shared helpers ───────────────────────────

public abstract class RigolDS1000ZBlockBase : SequenceBlockBase
{
    protected bool TryGetDriver(SequenceContext context, out RigolDS1000ZDriver scope, out string err)
    {
        string name = GetPropStr("InstrumentName");
        scope = null!;
        if (!context.Instruments.TryGetValue(name, out var drv))
        {
            err = $"Nie znaleziono instrumentu: {name}";
            return false;
        }
        if (drv is not RigolDS1000ZDriver s)
        {
            err = $"Instrument '{name}' nie jest RigolDS1000ZDriver";
            return false;
        }
        scope = s;
        err   = string.Empty;
        return true;
    }

    protected static double ParseDouble(string s, double fallback) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;

    protected static string FormatTime(double secs) =>
        secs >= 1    ? $"{secs:G3} s"
      : secs >= 1e-3 ? $"{secs * 1e3:G3} ms"
      : secs >= 1e-6 ? $"{secs * 1e6:G3} µs"
                     : $"{secs * 1e9:G3} ns";
}

// ── RigolDS1000Z_SetChannel ──────────────────────────────────────────────────

public class RigolDS1000Z_SetChannel : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_SetChannel";
    public override string DisplayName  => "Konfiguruj Kanał";
    public override string Description  => "Ustawia parametry kanału oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0xAA, 0x00);
    public override string Category     => "Rigol DS1000Z";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel",  "Kanał",        Ch.Channels,  "1"),
        BlockPropertyDefinition.Check("Display",  "Wyświetl",     true),
        BlockPropertyDefinition.Combo("Coupling", "Sprzężenie",   Ch.Couplings, "DC"),
        BlockPropertyDefinition.Combo("Scale",    "V/dz",         Ch.VScales,   "1"),
        BlockPropertyDefinition.Text ("Offset",   "Offset [V]",   "0"),
        BlockPropertyDefinition.Combo("Probe",    "Sonda ×",      Ch.Probes,    "10"),
        BlockPropertyDefinition.Combo("BWLimit",  "Ogr. pasma",   Ch.BWLimits,  "OFF"),
        BlockPropertyDefinition.Check("Invert",   "Odwróć",       false),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);

        int ch        = int.TryParse(GetPropStr("Channel", "1"), out int c) ? c : 1;
        bool display  = GetProp<bool>("Display", true);
        string couple = GetPropStr("Coupling", "DC");
        double scale  = ParseDouble(GetPropStr("Scale", "1"), 1.0);
        double offset = ParseDouble(GetPropStr("Offset", "0"), 0.0);
        double probe  = ParseDouble(GetPropStr("Probe", "10"), 10.0);
        string bwl    = GetPropStr("BWLimit", "OFF");
        bool invert   = GetProp<bool>("Invert", false);

        try
        {
            await scope.SetChannelDisplayAsync(ch, display);
            await scope.SetChannelCouplingAsync(ch, couple);
            await scope.SetChannelScaleAsync(ch, scale);
            await scope.SetChannelOffsetAsync(ch, offset);
            await scope.SetChannelProbeAsync(ch, probe);
            await scope.SetChannelBandwidthAsync(ch, bwl);
            await scope.SetChannelInvertAsync(ch, invert);
            context.Log?.Invoke($"DS1000Z CH{ch}: {(display?"ON":"OFF")} {couple} {scale}V/dz offs={offset}V sonda×{probe} BW={bwl}");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetChannel: {ex.Message}"); }
    }

    static RigolDS1000Z_SetChannel() =>
        BlockRegistry.Register("RigolDS1000Z_SetChannel", () => new RigolDS1000Z_SetChannel());
}

// ── RigolDS1000Z_SetTimebase ─────────────────────────────────────────────────

public class RigolDS1000Z_SetTimebase : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_SetTimebase";
    public override string DisplayName  => "Ustaw Podstawę Czasu";
    public override string Description  => "Ustawia podstawę czasu oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x20, 0x70, 0xB8);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> Modes = new() { "MAIN", "XY", "ROLL" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Scale",  "s/dz",           Ch.TScales, "0.001"),
        BlockPropertyDefinition.Text ("Offset", "Offset czasu [s]","0"),
        BlockPropertyDefinition.Combo("Mode",   "Tryb podstawy",  Modes,      "MAIN"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);

        double scale  = ParseDouble(GetPropStr("Scale", "0.001"), 0.001);
        double offset = ParseDouble(GetPropStr("Offset", "0"), 0.0);
        string mode   = GetPropStr("Mode", "MAIN");

        try
        {
            await scope.SetTimebaseModeAsync(mode);
            await scope.SetTimebaseScaleAsync(scale);
            await scope.SetTimebaseOffsetAsync(offset);
            context.Log?.Invoke($"DS1000Z Timebase: {FormatTime(scale)}/dz  offs={offset:G4}s  tryb={mode}");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetTimebase: {ex.Message}"); }
    }

    static RigolDS1000Z_SetTimebase() =>
        BlockRegistry.Register("RigolDS1000Z_SetTimebase", () => new RigolDS1000Z_SetTimebase());
}

// ── RigolDS1000Z_SetTrigger ──────────────────────────────────────────────────

public class RigolDS1000Z_SetTrigger : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_SetTrigger";
    public override string DisplayName  => "Ustaw Wyzwalanie";
    public override string Description  => "Konfiguruje wyzwalanie (edge/pulse/slope) oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x65, 0x00);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> TrigModes  = new() { "EDGE", "PULSe", "SLOPe", "VIDeo", "PATTern", "RS232", "I2C", "SPI" };
    private static readonly List<string> TrigSweeps = new() { "AUTO", "NORMal", "SINGle" };
    private static readonly List<string> TrigSrcs   = new() { "CH1", "CH2", "CH3", "CH4", "AC" };
    private static readonly List<string> TrigSlopes = new() { "POSitive", "NEGative", "RFALl" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("TrigMode",  "Tryb wyzwalania", TrigModes,  "EDGE"),
        BlockPropertyDefinition.Combo("Sweep",     "Akwizycja",       TrigSweeps, "AUTO"),
        BlockPropertyDefinition.Combo("Source",    "Źródło",          TrigSrcs,   "CH1"),
        BlockPropertyDefinition.Combo("Slope",     "Zbocze",          TrigSlopes, "POSitive"),
        BlockPropertyDefinition.Text ("Level",     "Poziom [V]",      "0"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);

        string mode   = GetPropStr("TrigMode", "EDGE");
        string sweep  = GetPropStr("Sweep", "AUTO");
        string source = GetPropStr("Source", "CH1");
        string slope  = GetPropStr("Slope", "POSitive");
        double level  = ParseDouble(GetPropStr("Level", "0"), 0.0);

        try
        {
            await scope.SetTriggerModeAsync(mode);
            await scope.SetTriggerSweepAsync(sweep);
            if (mode.Equals("EDGE", StringComparison.OrdinalIgnoreCase))
            {
                await scope.SetTriggerEdgeSourceAsync(source);
                await scope.SetTriggerEdgeSlopeAsync(slope);
                await scope.SetTriggerEdgeLevelAsync(level);
            }
            context.Log?.Invoke($"DS1000Z Trigger: {mode} {sweep} src={source} slope={slope} lvl={level:G4}V");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetTrigger: {ex.Message}"); }
    }

    static RigolDS1000Z_SetTrigger() =>
        BlockRegistry.Register("RigolDS1000Z_SetTrigger", () => new RigolDS1000Z_SetTrigger());
}

// ── RigolDS1000Z_SetAcquire ──────────────────────────────────────────────────

public class RigolDS1000Z_SetAcquire : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_SetAcquire";
    public override string DisplayName  => "Tryb Akwizycji";
    public override string Description  => "Ustawia typ akwizycji, uśrednianie i głębokość pamięci Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x60, 0x40, 0xC0);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> AcqTypes  = new() { "NORMal", "AVERages", "PEAKdetect", "HRESolution" };
    private static readonly List<string> Averages   = new() { "2","4","8","16","32","64","128","256","512","1024" };
    private static readonly List<string> MemDepths  = new() { "AUTO","12000","120000","1200000","12000000","24000000" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("AcqType",  "Typ akwizycji",  AcqTypes,  "NORMal"),
        BlockPropertyDefinition.Combo("Averages", "Uśrednianie",    Averages,  "16"),
        BlockPropertyDefinition.Combo("MemDepth", "Głębokość pam.", MemDepths, "AUTO"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);

        string type   = GetPropStr("AcqType", "NORMal");
        int avg       = int.TryParse(GetPropStr("Averages", "16"), out int a) ? a : 16;
        string depth  = GetPropStr("MemDepth", "AUTO");

        try
        {
            await scope.SetAcquireTypeAsync(type);
            if (type.Equals("AVERages", StringComparison.OrdinalIgnoreCase))
                await scope.SetAcquireAveragesAsync(avg);
            await scope.SetAcquireMemDepthAsync(depth);
            context.Log?.Invoke($"DS1000Z Acquire: typ={type} avg={avg} mem={depth}");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetAcquire: {ex.Message}"); }
    }

    static RigolDS1000Z_SetAcquire() =>
        BlockRegistry.Register("RigolDS1000Z_SetAcquire", () => new RigolDS1000Z_SetAcquire());
}

// ── RigolDS1000Z_Run ─────────────────────────────────────────────────────────

public class RigolDS1000Z_Run : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_Run";
    public override string DisplayName  => "Uruchom (RUN)";
    public override string Description  => "Uruchamia ciągłą akwizycję oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x10, 0x9A, 0x10);
    public override string Category     => "Rigol DS1000Z";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
        [ BlockPropertyDefinition.Instrument("InstrumentName") ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        try { await scope.RunAsync(); context.Log?.Invoke("DS1000Z: RUN"); return BlockExecutionResult.Ok(NextBlockId); }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd RUN: {ex.Message}"); }
    }

    static RigolDS1000Z_Run() =>
        BlockRegistry.Register("RigolDS1000Z_Run", () => new RigolDS1000Z_Run());
}

// ── RigolDS1000Z_Stop ────────────────────────────────────────────────────────

public class RigolDS1000Z_Stop : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_Stop";
    public override string DisplayName  => "Zatrzymaj (STOP)";
    public override string Description  => "Zatrzymuje akwizycję oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x18, 0x00);
    public override string Category     => "Rigol DS1000Z";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
        [ BlockPropertyDefinition.Instrument("InstrumentName") ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        try { await scope.StopAsync(); context.Log?.Invoke("DS1000Z: STOP"); return BlockExecutionResult.Ok(NextBlockId); }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd STOP: {ex.Message}"); }
    }

    static RigolDS1000Z_Stop() =>
        BlockRegistry.Register("RigolDS1000Z_Stop", () => new RigolDS1000Z_Stop());
}

// ── RigolDS1000Z_Single ──────────────────────────────────────────────────────

public class RigolDS1000Z_Single : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_Single";
    public override string DisplayName  => "Pojedyncza akwizycja (SINGLE)";
    public override string Description  => "Wykonuje jednorazową akwizycję oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x60, 0xBB);
    public override string Category     => "Rigol DS1000Z";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("WaitMs", "Czekaj po [ms]", 500),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        int waitMs = (int)GetProp<double>("WaitMs", 500);
        try
        {
            await scope.SingleAsync();
            if (waitMs > 0) await Task.Delay(waitMs, context.CancellationToken);
            context.Log?.Invoke($"DS1000Z: SINGLE (czekaj {waitMs}ms)");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SINGLE: {ex.Message}"); }
    }

    static RigolDS1000Z_Single() =>
        BlockRegistry.Register("RigolDS1000Z_Single", () => new RigolDS1000Z_Single());
}

// ── RigolDS1000Z_AutoScale ───────────────────────────────────────────────────

public class RigolDS1000Z_AutoScale : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_AutoScale";
    public override string DisplayName  => "Auto Scale";
    public override string Description  => "Uruchamia automatyczne skalowanie oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0xC8, 0x7E, 0x00);
    public override string Category     => "Rigol DS1000Z";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("WaitMs", "Czekaj po [ms]", 2000),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        int waitMs = (int)GetProp<double>("WaitMs", 2000);
        try
        {
            await scope.AutoScaleAsync();
            if (waitMs > 0) await Task.Delay(waitMs, context.CancellationToken);
            context.Log?.Invoke($"DS1000Z: AutoScale (czekaj {waitMs}ms)");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd AutoScale: {ex.Message}"); }
    }

    static RigolDS1000Z_AutoScale() =>
        BlockRegistry.Register("RigolDS1000Z_AutoScale", () => new RigolDS1000Z_AutoScale());
}

// ── RigolDS1000Z_MeasureVoltage ──────────────────────────────────────────────

public class RigolDS1000Z_MeasureVoltage : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_MeasureVoltage";
    public override string DisplayName  => "Zmierz Napięcie";
    public override string Description  => "Mierzy parametr napięciowy kanału oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x10, 0x88, 0xA0);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> VParams = new()
        { "VMAX", "VMIN", "VPP", "VTOP", "VBASe", "VAMP", "VAVG", "VRMS", "OVERshoot", "PREShoot" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel",   "Kanał",     Ch.Channels, "1"),
        BlockPropertyDefinition.Combo("Parameter", "Parametr",  VParams,     "VPP"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "voltage"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        int ch        = int.TryParse(GetPropStr("Channel", "1"), out int c) ? c : 1;
        string param  = GetPropStr("Parameter", "VPP");
        string outVar = GetPropStr("OutputVariable", "voltage");
        try
        {
            double val = await scope.MeasureAsync(param, ch);
            context.SetVariable(outVar, val);
            context.Log?.Invoke($"DS1000Z CH{ch} {param}: {val:G6} {RigolDS1000ZDriver.GetMeasUnit(param)} → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, val);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MeasureVoltage: {ex.Message}"); }
    }

    static RigolDS1000Z_MeasureVoltage() =>
        BlockRegistry.Register("RigolDS1000Z_MeasureVoltage", () => new RigolDS1000Z_MeasureVoltage());
}

// ── RigolDS1000Z_MeasureTime ─────────────────────────────────────────────────

public class RigolDS1000Z_MeasureTime : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_MeasureTime";
    public override string DisplayName  => "Zmierz Czas/Częstotliwość";
    public override string Description  => "Mierzy parametr czasowy lub częstotliwość kanału oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x10, 0x80, 0x80);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> TParams = new()
        { "FREQuency", "PERiod", "RISetime", "FALLtime", "PWIDth", "NWIDth" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel",   "Kanał",     Ch.Channels, "1"),
        BlockPropertyDefinition.Combo("Parameter", "Parametr",  TParams,     "FREQuency"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "frequency"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        int ch        = int.TryParse(GetPropStr("Channel", "1"), out int c) ? c : 1;
        string param  = GetPropStr("Parameter", "FREQuency");
        string outVar = GetPropStr("OutputVariable", "frequency");
        try
        {
            double val = await scope.MeasureAsync(param, ch);
            context.SetVariable(outVar, val);
            context.Log?.Invoke($"DS1000Z CH{ch} {param}: {val:G6} {RigolDS1000ZDriver.GetMeasUnit(param)} → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, val);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MeasureTime: {ex.Message}"); }
    }

    static RigolDS1000Z_MeasureTime() =>
        BlockRegistry.Register("RigolDS1000Z_MeasureTime", () => new RigolDS1000Z_MeasureTime());
}

// ── RigolDS1000Z_MeasureDuty ─────────────────────────────────────────────────

public class RigolDS1000Z_MeasureDuty : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_MeasureDuty";
    public override string DisplayName  => "Zmierz Wypełnienie";
    public override string Description  => "Mierzy współczynnik wypełnienia sygnału oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x20, 0x60, 0xA8);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> DParams = new() { "PDUTycycle", "NDUTycycle" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel",   "Kanał",     Ch.Channels, "1"),
        BlockPropertyDefinition.Combo("Parameter", "Parametr",  DParams,     "PDUTycycle"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "duty"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        int ch        = int.TryParse(GetPropStr("Channel", "1"), out int c) ? c : 1;
        string param  = GetPropStr("Parameter", "PDUTycycle");
        string outVar = GetPropStr("OutputVariable", "duty");
        try
        {
            double val = await scope.MeasureAsync(param, ch);
            context.SetVariable(outVar, val);
            context.Log?.Invoke($"DS1000Z CH{ch} {param}: {val:G5} % → '{outVar}'");
            return BlockExecutionResult.Ok(NextBlockId, val);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MeasureDuty: {ex.Message}"); }
    }

    static RigolDS1000Z_MeasureDuty() =>
        BlockRegistry.Register("RigolDS1000Z_MeasureDuty", () => new RigolDS1000Z_MeasureDuty());
}

// ── RigolDS1000Z_ReadWaveform ─────────────────────────────────────────────────

public class RigolDS1000Z_ReadWaveform : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_ReadWaveform";
    public override string DisplayName  => "Odczytaj Przebieg";
    public override string Description  => "Odczytuje dane przebiegu z oscyloskopu Rigol DS1000Z i zapisuje do CSV";
    public override Color  BlockColor   => Color.FromRgb(0x08, 0x70, 0x50);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> WavModes = new() { "NORMal", "MAXimum", "RAW" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel",  "Kanał",          Ch.Channels, "1"),
        BlockPropertyDefinition.Combo("WaveMode", "Tryb przebiegu", WavModes,    "NORMal"),
        BlockPropertyDefinition.FilePath("FilePath", "Plik CSV"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);
        int ch       = int.TryParse(GetPropStr("Channel", "1"), out int c) ? c : 1;
        string mode  = GetPropStr("WaveMode", "NORMal");
        string path  = GetPropStr("FilePath", "");

        if (string.IsNullOrWhiteSpace(path))
            return BlockExecutionResult.Fail("Brak ścieżki pliku CSV");

        try
        {
            await scope.SetWaveformModeAsync(mode);
            await scope.SaveWaveformToCsvAsync(ch, path);
            context.Log?.Invoke($"DS1000Z Waveform CH{ch} ({mode}) → {path}");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd ReadWaveform: {ex.Message}"); }
    }

    static RigolDS1000Z_ReadWaveform() =>
        BlockRegistry.Register("RigolDS1000Z_ReadWaveform", () => new RigolDS1000Z_ReadWaveform());
}

// ── RigolDS1000Z_MathSetup ───────────────────────────────────────────────────

public class RigolDS1000Z_MathSetup : RigolDS1000ZBlockBase
{
    public override string BlockType    => "RigolDS1000Z_MathSetup";
    public override string DisplayName  => "Konfiguruj Math/FFT";
    public override string Description  => "Konfiguruje kanał MATH lub FFT oscyloskopu Rigol DS1000Z";
    public override Color  BlockColor   => Color.FromRgb(0x60, 0x30, 0x80);
    public override string Category     => "Rigol DS1000Z";

    private static readonly List<string> MathOps  = new()
        { "ADD", "SUBTract", "MULTiply", "DIVision", "FFT", "ANDer", "ORer", "XORer", "NOTer", "INTGral", "DIFF", "SQRT", "LOG", "LN", "EXP", "ABS" };
    private static readonly List<string> FFTWins  = new()
        { "RECTangle", "BLACkman", "HANNing", "HAMMing", "FLATtop", "TRIangle" };
    private static readonly List<string> FFTUnits = new() { "VRMS", "DB" };
    private static readonly List<string> ChSrcs   = new() { "CH1", "CH2", "CH3", "CH4" };

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Operation", "Operacja",       MathOps,  "ADD"),
        BlockPropertyDefinition.Combo("Source1",   "Źródło 1",      ChSrcs,   "CH1"),
        BlockPropertyDefinition.Combo("Source2",   "Źródło 2",      ChSrcs,   "CH2"),
        BlockPropertyDefinition.Check("Enable",    "Włącz Math",     true),
        BlockPropertyDefinition.Combo("FFTWindow", "FFT: okno",      FFTWins,  "HANNing"),
        BlockPropertyDefinition.Combo("FFTUnit",   "FFT: jednostka", FFTUnits, "DB"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        if (!TryGetDriver(context, out var scope, out string err))
            return BlockExecutionResult.Fail(err);

        string op     = GetPropStr("Operation", "ADD");
        string src1   = GetPropStr("Source1", "CH1");
        string src2   = GetPropStr("Source2", "CH2");
        bool enable   = GetProp<bool>("Enable", true);
        string fftWin = GetPropStr("FFTWindow", "HANNing");
        string fftUnit= GetPropStr("FFTUnit", "DB");

        try
        {
            await scope.SetMathOperationAsync(op);
            await scope.SetMathSource1Async(src1);
            bool needSrc2 = !op.Equals("FFT", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("INTGral", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("DIFF", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("SQRT", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("LOG", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("LN", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("EXP", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("ABS", StringComparison.OrdinalIgnoreCase)
                         && !op.Equals("NOTer", StringComparison.OrdinalIgnoreCase);
            if (needSrc2)
                await scope.SetMathSource2Async(src2);

            if (op.Equals("FFT", StringComparison.OrdinalIgnoreCase))
            {
                await scope.SetFFTSourceAsync(src1);
                await scope.SetFFTWindowAsync(fftWin);
                await scope.SetFFTUnitAsync(fftUnit);
            }
            await scope.SetMathDisplayAsync(enable);
            context.Log?.Invoke($"DS1000Z Math: {op} src1={src1} {(needSrc2 ? "src2="+src2 : "")} {(enable ? "ON" : "OFF")}");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd MathSetup: {ex.Message}"); }
    }

    static RigolDS1000Z_MathSetup() =>
        BlockRegistry.Register("RigolDS1000Z_MathSetup", () => new RigolDS1000Z_MathSetup());
}

// ── Registration entry point ─────────────────────────────────────────────────

public static class RigolDS1000ZBlocks
{
    public static void RegisterAll()
    {
        _ = new RigolDS1000Z_SetChannel();
        _ = new RigolDS1000Z_SetTimebase();
        _ = new RigolDS1000Z_SetTrigger();
        _ = new RigolDS1000Z_SetAcquire();
        _ = new RigolDS1000Z_Run();
        _ = new RigolDS1000Z_Stop();
        _ = new RigolDS1000Z_Single();
        _ = new RigolDS1000Z_AutoScale();
        _ = new RigolDS1000Z_MeasureVoltage();
        _ = new RigolDS1000Z_MeasureTime();
        _ = new RigolDS1000Z_MeasureDuty();
        _ = new RigolDS1000Z_ReadWaveform();
        _ = new RigolDS1000Z_MathSetup();
    }
}
