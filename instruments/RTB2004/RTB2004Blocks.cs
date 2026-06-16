using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace RTB2004;

// ── RTB2004_SetChannel ───────────────────────────────────────────────────────

public class RTB2004_SetChannel : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_SetChannel";
    public override string DisplayName  => "Konfiguruj Kanał";
    public override string Description  => "Ustawia parametry kanału oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x80, 0x80);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel", "Kanał", new List<string> { "1","2","3","4" }, "1"),
        BlockPropertyDefinition.Check("Enabled", "Włącz kanał", true),
        BlockPropertyDefinition.Combo("Scale", "Skala [V/dz]",
            new List<string> { "0.001","0.002","0.005","0.01","0.02","0.05","0.1","0.2","0.5","1","2","5","10" }, "1"),
        BlockPropertyDefinition.Number("Offset", "Offset [V]", 0.0),
        BlockPropertyDefinition.Combo("Coupling", "Sprzężenie", new List<string> { "DC","AC","GND" }, "DC"),
        BlockPropertyDefinition.Combo("Probe", "Sonda [x]", new List<string> { "1","10","100" }, "10"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        int    channel   = int.TryParse(GetPropStr("Channel", "1"), out int ch) ? ch : 1;
        bool   enabled   = GetProp<bool>("Enabled", true);
        string scaleStr  = GetPropStr("Scale", "1");
        double offset    = GetProp<double>("Offset", 0.0);
        string coupling  = GetPropStr("Coupling", "DC");
        string probeStr  = GetPropStr("Probe", "10");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        double scale = double.TryParse(scaleStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double s) ? s : 1.0;
        double probe = double.TryParse(probeStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double p) ? p : 10.0;

        try
        {
            await scope.SetChannelEnabledAsync(channel, enabled);
            await scope.SetChannelScaleAsync(channel, scale);
            await scope.SetChannelOffsetAsync(channel, offset);
            await scope.SetChannelCouplingAsync(channel, coupling);
            await scope.SetChannelProbeAsync(channel, probe);
            context.Log?.Invoke($"RTB2004 CH{channel}: {(enabled ? "ON" : "OFF")}  {scale} V/dz  {coupling}  {probe}x");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetChannel: {ex.Message}"); }
    }

    static RTB2004_SetChannel() =>
        BlockRegistry.Register("RTB2004_SetChannel", () => new RTB2004_SetChannel());
}

// ── RTB2004_SetTimebase ──────────────────────────────────────────────────────

public class RTB2004_SetTimebase : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_SetTimebase";
    public override string DisplayName  => "Ustaw Podstawę Czasu";
    public override string Description  => "Ustawia podstawę czasu (s/dz) oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x44, 0xAA);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("SecsPerDiv", "Czas [s/dz]",
            new List<string>
            {
                "1E-9","2E-9","5E-9","1E-8","2E-8","5E-8",
                "1E-7","2E-7","5E-7","1E-6","2E-6","5E-6",
                "1E-5","2E-5","5E-5","0.0001","0.0002","0.0005",
                "0.001","0.002","0.005","0.01","0.02","0.05",
                "0.1","0.2","0.5","1","2","5","10","20","50"
            }, "0.001"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string secsStr   = GetPropStr("SecsPerDiv", "0.001");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        if (!double.TryParse(secsStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double secs))
            return BlockExecutionResult.Fail($"Nieprawidłowa wartość podstawy czasu: {secsStr}");

        try
        {
            await scope.SetTimescaleAsync(secs);
            context.Log?.Invoke($"RTB2004: Podstawa czasu → {secs:G3} s/dz");
            return BlockExecutionResult.Ok(NextBlockId, secs);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetTimebase: {ex.Message}"); }
    }

    static RTB2004_SetTimebase() =>
        BlockRegistry.Register("RTB2004_SetTimebase", () => new RTB2004_SetTimebase());
}

// ── RTB2004_SetTrigger ───────────────────────────────────────────────────────

public class RTB2004_SetTrigger : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_SetTrigger";
    public override string DisplayName  => "Ustaw Wyzwalanie";
    public override string Description  => "Konfiguruje wyzwalanie (trigger) oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0x66, 0x00, 0xCC);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Source", "Źródło",
            new List<string> { "CH1","CH2","CH3","CH4","EXT" }, "CH1"),
        BlockPropertyDefinition.Number("Level", "Poziom [V]", 0.0),
        BlockPropertyDefinition.Combo("Slope", "Zbocze",
            new List<string> { "POS","NEG","EITH" }, "POS"),
        BlockPropertyDefinition.Combo("Mode", "Tryb",
            new List<string> { "AUTO","NORM" }, "AUTO"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        string source    = GetPropStr("Source", "CH1");
        double level     = GetProp<double>("Level", 0.0);
        string slope     = GetPropStr("Slope", "POS");
        string mode      = GetPropStr("Mode", "AUTO");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        int chNum = source.StartsWith("CH") && int.TryParse(source[2..], out int n) ? n : 1;

        try
        {
            await scope.SetTriggerSourceAsync(source);
            await scope.SetTriggerLevelAsync(chNum, level);
            await scope.SetTriggerSlopeAsync(slope);
            await scope.SetTriggerModeAsync(mode);
            context.Log?.Invoke($"RTB2004 TRIG: {source}  {level:F3} V  {slope}  {mode}");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd SetTrigger: {ex.Message}"); }
    }

    static RTB2004_SetTrigger() =>
        BlockRegistry.Register("RTB2004_SetTrigger", () => new RTB2004_SetTrigger());
}

// ── RTB2004_Run ──────────────────────────────────────────────────────────────

public class RTB2004_Run : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_Run";
    public override string DisplayName  => "Start (RUN)";
    public override string Description  => "Uruchamia ciągłą akwizycję oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x88, 0x22);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        try
        {
            await scope.RunAsync();
            context.Log?.Invoke("RTB2004: RUN");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd Run: {ex.Message}"); }
    }

    static RTB2004_Run() =>
        BlockRegistry.Register("RTB2004_Run", () => new RTB2004_Run());
}

// ── RTB2004_Stop ─────────────────────────────────────────────────────────────

public class RTB2004_Stop : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_Stop";
    public override string DisplayName  => "Stop";
    public override string Description  => "Zatrzymuje akwizycję oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x00, 0x00);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        try
        {
            await scope.StopAsync();
            context.Log?.Invoke("RTB2004: STOP");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd Stop: {ex.Message}"); }
    }

    static RTB2004_Stop() =>
        BlockRegistry.Register("RTB2004_Stop", () => new RTB2004_Stop());
}

// ── RTB2004_Single ───────────────────────────────────────────────────────────

public class RTB2004_Single : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_Single";
    public override string DisplayName  => "Pojedyncza Akwizycja";
    public override string Description  => "Wykonuje pojedynczą akwizycję i czeka na zakończenie";
    public override Color  BlockColor   => Color.FromRgb(0xCC, 0x66, 0x00);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Number("WaitMs", "Czas oczekiwania [ms]", 1000),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        int    waitMs    = (int)GetProp<double>("WaitMs", 1000);

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        try
        {
            await scope.SingleAsync();
            await Task.Delay(Math.Max(100, waitMs), context.CancellationToken);
            context.Log?.Invoke($"RTB2004: SINGLE (oczekiwanie {waitMs} ms)");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (OperationCanceledException) { return BlockExecutionResult.Fail("Anulowano"); }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd Single: {ex.Message}"); }
    }

    static RTB2004_Single() =>
        BlockRegistry.Register("RTB2004_Single", () => new RTB2004_Single());
}

// ── RTB2004_Autoscale ────────────────────────────────────────────────────────

public class RTB2004_Autoscale : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_Autoscale";
    public override string DisplayName  => "Autoskalowanie";
    public override string Description  => "Wykonuje autoskalowanie oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0xAA, 0xAA);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        try
        {
            await scope.AutoscaleAsync();
            await Task.Delay(2000, context.CancellationToken);
            context.Log?.Invoke("RTB2004: Autoskalowanie OK");
            return BlockExecutionResult.Ok(NextBlockId);
        }
        catch (OperationCanceledException) { return BlockExecutionResult.Fail("Anulowano"); }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd Autoscale: {ex.Message}"); }
    }

    static RTB2004_Autoscale() =>
        BlockRegistry.Register("RTB2004_Autoscale", () => new RTB2004_Autoscale());
}

// ── RTB2004_Measure ──────────────────────────────────────────────────────────

public class RTB2004_Measure : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_Measure";
    public override string DisplayName  => "Pomiar Parametru";
    public override string Description  => "Mierzy wybrany parametr sygnału na kanale oscyloskopu RTB2004";
    public override Color  BlockColor   => Color.FromRgb(0x44, 0x55, 0x88);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel", "Kanał", new List<string> { "1","2","3","4" }, "1"),
        BlockPropertyDefinition.Combo("MeasType", "Typ pomiaru",
            new List<string>
            {
                "FREQ","PERI","AMPL","RMS","MEAN","PK2PK",
                "PHAS","DEL","CRIS","FFAL","PWID","NWID","DCYC"
            }, "FREQ"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "measurement"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        int    channel   = int.TryParse(GetPropStr("Channel", "1"), out int ch) ? ch : 1;
        string measType  = GetPropStr("MeasType", "FREQ");
        string outVar    = GetPropStr("OutputVariable", "measurement");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        try
        {
            double value = await scope.MeasureChannelAsync(channel, measType);
            context.SetVariable(outVar, value);
            context.Log?.Invoke($"RTB2004 CH{channel} {measType}: {value:G6} → '{outVar}'");
            context.AddResult(new MeasurementResult
            {
                Function = measType, Value = value,
                InstrumentName = instrName,
                ChannelId = $"CH{channel}",
                ParameterName = outVar,
            });
            return BlockExecutionResult.Ok(NextBlockId, value);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd Measure: {ex.Message}"); }
    }

    static RTB2004_Measure() =>
        BlockRegistry.Register("RTB2004_Measure", () => new RTB2004_Measure());
}

// ── RTB2004_ReadWaveform ─────────────────────────────────────────────────────

public class RTB2004_ReadWaveform : SequenceBlockBase
{
    public override string BlockType    => "RTB2004_ReadWaveform";
    public override string DisplayName  => "Zapisz Przebieg do CSV";
    public override string Description  => "Pobiera dane przebiegu z kanału oscyloskopu i zapisuje je do pliku CSV";
    public override Color  BlockColor   => Color.FromRgb(0x00, 0x55, 0x22);
    public override string Category     => "RTB2004";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Channel", "Kanał", new List<string> { "1","2","3","4" }, "1"),
        BlockPropertyDefinition.FilePath("FilePath", "Plik CSV"),
        BlockPropertyDefinition.Variable("PointsVariable", "Liczba próbek → zmienna", "waveform_points"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName   = GetPropStr("InstrumentName");
        int    channel     = int.TryParse(GetPropStr("Channel", "1"), out int ch) ? ch : 1;
        string filePath    = GetPropStr("FilePath", "waveform.csv");
        string pointsVar   = GetPropStr("PointsVariable", "waveform_points");

        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Nie znaleziono instrumentu: {instrName}");
        if (driver is not RTB2004Driver scope)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest RTB2004Driver");

        try
        {
            await scope.SaveWaveformToCsvAsync(channel, filePath);
            var (voltages, _, _) = await scope.ReadWaveformAsync(channel);
            context.SetVariable(pointsVar, (double)voltages.Length);
            context.Log?.Invoke($"RTB2004 CH{channel}: {voltages.Length} próbek → {filePath}");
            return BlockExecutionResult.Ok(NextBlockId, (double)voltages.Length);
        }
        catch (Exception ex) { return BlockExecutionResult.Fail($"Błąd ReadWaveform: {ex.Message}"); }
    }

    static RTB2004_ReadWaveform() =>
        BlockRegistry.Register("RTB2004_ReadWaveform", () => new RTB2004_ReadWaveform());
}

// ── Registration entry point ─────────────────────────────────────────────────

public static class RTB2004Blocks
{
    public static void RegisterAll()
    {
        _ = new RTB2004_SetChannel();
        _ = new RTB2004_SetTimebase();
        _ = new RTB2004_SetTrigger();
        _ = new RTB2004_Run();
        _ = new RTB2004_Stop();
        _ = new RTB2004_Single();
        _ = new RTB2004_Autoscale();
        _ = new RTB2004_Measure();
        _ = new RTB2004_ReadWaveform();
    }
}
