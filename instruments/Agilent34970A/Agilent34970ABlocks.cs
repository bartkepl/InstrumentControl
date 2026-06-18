using System.Windows.Media;
using Agilent34970A.Cards;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace Agilent34970A;

// ─────────────────────────────────────────────────────────────────────────────
// Helper — wspólne pobranie sterownika z kontekstu
// ─────────────────────────────────────────────────────────────────────────────

internal static class A34970ABlockHelpers
{
    public static (Agilent34970ADriver? daq, BlockExecutionResult? error) ResolveDriver(
        SequenceBlockBase block, SequenceContext context, string instrName)
    {
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return (null, BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest aktywny."));
        if (driver is not Agilent34970ADriver daq)
            return (null, BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest sterownikiem Agilent34970A."));
        return (daq, null);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 1. A34970A_DetectCards — wykrycie kart w slotach
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_DetectCards : SequenceBlockBase
{
    public override string BlockType => "A34970A_DetectCards";
    public override string DisplayName => "Wykryj karty";
    public override string Description => "Odpytuje sloty (SYST:CTYP?) i konfiguruje wykryte karty 34970A";
    public override Color BlockColor => Color.FromRgb(0x2C, 0x3E, 0x50);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        try
        {
            var cards = await daq!.DetectCardsAsync();
            foreach (var kv in cards)
                context.Log?.Invoke($"  Slot {kv.Key}: {(string.IsNullOrEmpty(kv.Value) ? "pusty" : kv.Value)}");
            int count = cards.Values.Count(v => !string.IsNullOrEmpty(v));
            return BlockExecutionResult.Ok(NextBlockId, count);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd wykrywania kart: {ex.Message}");
        }
    }

    static A34970A_DetectCards() =>
        BlockRegistry.Register("A34970A_DetectCards", () => new A34970A_DetectCards());
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. A34970A_ScanChannels — skan listy kanałów z jedną funkcją
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_ScanChannels : SequenceBlockBase
{
    public override string BlockType => "A34970A_ScanChannels";
    public override string DisplayName => "Skanuj kanały";
    public override string Description => "Konfiguruje i skanuje listę kanałów MUX z jedną funkcją; wyniki zapisuje jako zmienne";
    public override Color BlockColor => Color.FromRgb(0x29, 0x80, 0xB9);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Text("ChannelList", "Lista kanałów (SCPI)", "101,102,103"),
        BlockPropertyDefinition.Combo("Function", "Funkcja",
            new() { "VDC", "VAC", "OHM2W", "OHM4W", "CURR_DC", "CURR_AC", "FREQ", "PERIOD" }, "VDC"),
        BlockPropertyDefinition.Text("Range", "Zakres", "AUTO"),
        BlockPropertyDefinition.Variable("OutputPrefix", "Prefiks zmiennej wyjściowej", "ch"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        string channelList = GetPropStr("ChannelList", "101,102,103");
        string functionStr = GetPropStr("Function", "VDC");
        string range = GetPropStr("Range", "AUTO");
        string prefix = GetPropStr("OutputPrefix", "ch");

        if (!Enum.TryParse<MuxFunction>(functionStr, out var function))
            return BlockExecutionResult.Fail($"Nieznana funkcja: {functionStr}");

        try
        {
            context.Log?.Invoke($"[34970A] Skanowanie kanałów: {channelList}, funkcja: {functionStr}");
            var results = await daq!.ScanUniformAsync(channelList, function, range);

            foreach (var result in results)
            {
                string varName = $"{prefix}_{result.ChannelId}";
                context.SetVariable(varName, result.Value);
                context.Log?.Invoke($"  Kanał {result.ChannelId}: {result.Value:G6} {result.Unit} → {varName}");
            }

            return BlockExecutionResult.Ok(NextBlockId, results);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd skanowania: {ex.Message}");
        }
    }

    static A34970A_ScanChannels() =>
        BlockRegistry.Register("A34970A_ScanChannels", () => new A34970A_ScanChannels());
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. A34970A_ScanMixed — skan mieszany (różne funkcje per kanał)
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_ScanMixed : SequenceBlockBase
{
    public override string BlockType => "A34970A_ScanMixed";
    public override string DisplayName => "Skan mieszany";
    public override string Description =>
        "Skanuje kanały o różnych funkcjach naraz. Format: 'kanał=FUNK[:param][@zakres]' rozdzielane ';'";
    public override Color BlockColor => Color.FromRgb(0x16, 0x6B, 0x8F);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Text("Spec", "Specyfikacja kanałów",
            "101=VDC; 102=VDC; 103=RTD4W:85"),
        BlockPropertyDefinition.Variable("OutputPrefix", "Prefiks zmiennej wyjściowej", "ch"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        string spec = GetPropStr("Spec", "");
        string prefix = GetPropStr("OutputPrefix", "ch");

        try
        {
            context.Log?.Invoke($"[34970A] Skan mieszany: {spec}");
            var results = await daq!.ScanSpecAsync(spec);

            if (results.Count == 0)
                return BlockExecutionResult.Fail("Pusta lub nieprawidłowa specyfikacja kanałów.");

            foreach (var result in results)
            {
                string varName = $"{prefix}_{result.ChannelId}";
                context.SetVariable(varName, result.Value);
                context.Log?.Invoke($"  Kanał {result.ChannelId} ({result.Function}): {result.Value:G6} {result.Unit} → {varName}");
            }

            return BlockExecutionResult.Ok(NextBlockId, results);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd skanu mieszanego: {ex.Message}");
        }
    }

    static A34970A_ScanMixed() =>
        BlockRegistry.Register("A34970A_ScanMixed", () => new A34970A_ScanMixed());
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. A34970A_MeasureChannel — pomiar pojedynczego kanału
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_MeasureChannel : SequenceBlockBase
{
    public override string BlockType => "A34970A_MeasureChannel";
    public override string DisplayName => "Zmierz kanał";
    public override string Description => "Mierzy jeden kanał MUX i zapisuje wynik w zmiennej";
    public override Color BlockColor => Color.FromRgb(0x27, 0xAE, 0x60);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Number("Channel", "Kanał (1-22)", 1),
        BlockPropertyDefinition.Combo("Function", "Funkcja",
            new() { "VDC", "VAC", "OHM2W", "OHM4W", "CURR_DC", "CURR_AC",
                    "TEMP_TC", "TEMP_RTD", "TEMP_RTD4W", "FREQ", "PERIOD" }, "VDC"),
        BlockPropertyDefinition.Text("Range", "Zakres", "AUTO"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "chValue"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        int channel = (int)GetProp<double>("Channel", 1);
        string functionStr = GetPropStr("Function", "VDC");
        string range = GetPropStr("Range", "AUTO");
        string outVar = GetPropStr("OutputVariable", "chValue");

        if (!Enum.TryParse<MuxFunction>(functionStr, out var function))
            return BlockExecutionResult.Fail($"Nieznana funkcja: {functionStr}");

        try
        {
            context.Log?.Invoke($"[34970A] Pomiar kanał {slot + channel}, funkcja: {functionStr}");
            var results = await daq!.ScanAsync(slot, new[] { channel }, function, range);

            if (results.Count == 0)
                return BlockExecutionResult.Fail("Brak wyników pomiaru.");

            var first = results[0];
            context.SetVariable(outVar, first.Value);
            context.Log?.Invoke($"  Kanał {first.ChannelId}: {first.Value:G6} {first.Unit} → {outVar}");

            return BlockExecutionResult.Ok(NextBlockId, first.Value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd pomiaru: {ex.Message}");
        }
    }

    static A34970A_MeasureChannel() =>
        BlockRegistry.Register("A34970A_MeasureChannel", () => new A34970A_MeasureChannel());
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. A34970A_SetDAC — wyjście DAC (34907A, kanał s04/s05)
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_SetDAC : SequenceBlockBase
{
    public override string BlockType => "A34970A_SetDAC";
    public override string DisplayName => "Ustaw DAC";
    public override string Description => "Ustawia wyjście DAC na karcie 34907A (±12 V)";
    public override Color BlockColor => Color.FromRgb(0xE7, 0x4C, 0x3C);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Combo("DacChannel", "Kanał DAC", new() { "1", "2" }, "1"),
        BlockPropertyDefinition.Number("Voltage", "Napięcie [V] (-12..12)", 0.0),
        BlockPropertyDefinition.Text("VoltageVariable", "Zmienna napięcia (opcjonalne)", ""),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        int dacChannel = int.TryParse(GetPropStr("DacChannel", "1"), out int dc) ? dc : 1;
        string voltVar = GetPropStr("VoltageVariable", "");

        double voltage = !string.IsNullOrWhiteSpace(voltVar)
            ? context.GetVariableAsDouble(voltVar, 0.0)
            : GetProp<double>("Voltage", 0.0);

        try
        {
            context.Log?.Invoke($"[34970A] DAC{dacChannel} slot {slot}: {voltage:F4} V");
            await daq!.SetDacAsync(slot, dacChannel, voltage);
            return BlockExecutionResult.Ok(NextBlockId, voltage);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd ustawiania DAC: {ex.Message}");
        }
    }

    static A34970A_SetDAC() =>
        BlockRegistry.Register("A34970A_SetDAC", () => new A34970A_SetDAC());
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. A34970A_SetDigitalOutput — wyjście cyfrowe (34907A)
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_SetDigitalOutput : SequenceBlockBase
{
    public override string BlockType => "A34970A_SetDigitalOutput";
    public override string DisplayName => "Cyfrowe wyjście";
    public override string Description => "Ustawia bajt wyjścia cyfrowego na karcie 34907A";
    public override Color BlockColor => Color.FromRgb(0x8E, 0x44, 0xAD);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Combo("Port", "Port (1/2)", new() { "1", "2" }, "1"),
        BlockPropertyDefinition.Number("Value", "Wartość (0-255)", 0),
        BlockPropertyDefinition.Text("ValueVariable", "Zmienna wartości (opcjonalne)", ""),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        int port = int.TryParse(GetPropStr("Port", "1"), out int p) ? p : 1;
        string valVar = GetPropStr("ValueVariable", "");

        double raw = !string.IsNullOrWhiteSpace(valVar)
            ? context.GetVariableAsDouble(valVar, 0)
            : GetProp<double>("Value", 0);
        byte value = (byte)Math.Clamp((int)raw, 0, 255);

        try
        {
            context.Log?.Invoke($"[34970A] Digital OUT slot {slot} port {port}: 0x{value:X2} ({value})");
            await daq!.SetDigitalOutputAsync(slot, port, value);
            return BlockExecutionResult.Ok(NextBlockId, (int)value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd wyjścia cyfrowego: {ex.Message}");
        }
    }

    static A34970A_SetDigitalOutput() =>
        BlockRegistry.Register("A34970A_SetDigitalOutput", () => new A34970A_SetDigitalOutput());
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. A34970A_ReadDigitalInput — wejście cyfrowe (34907A)
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_ReadDigitalInput : SequenceBlockBase
{
    public override string BlockType => "A34970A_ReadDigitalInput";
    public override string DisplayName => "Cyfrowe wejście";
    public override string Description => "Odczytuje bajt wejścia cyfrowego z karty 34907A";
    public override Color BlockColor => Color.FromRgb(0x16, 0xA0, 0x85);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Combo("Port", "Port (1/2)", new() { "1", "2" }, "1"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "digitalIn"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        int port = int.TryParse(GetPropStr("Port", "1"), out int p) ? p : 1;
        string outVar = GetPropStr("OutputVariable", "digitalIn");

        try
        {
            context.Log?.Invoke($"[34970A] Digital IN slot {slot} port {port}...");
            byte value = await daq!.ReadDigitalInputAsync(slot, port);
            context.SetVariable(outVar, (double)value);
            context.Log?.Invoke($"  Digital IN: 0x{value:X2} ({value}) → {outVar}");
            return BlockExecutionResult.Ok(NextBlockId, (int)value);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd odczytu cyfrowego: {ex.Message}");
        }
    }

    static A34970A_ReadDigitalInput() =>
        BlockRegistry.Register("A34970A_ReadDigitalInput", () => new A34970A_ReadDigitalInput());
}

// ─────────────────────────────────────────────────────────────────────────────
// 8. A34970A_ReadTotalizer — odczyt totalizatora (34907A, kanał s03)
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_ReadTotalizer : SequenceBlockBase
{
    public override string BlockType => "A34970A_ReadTotalizer";
    public override string DisplayName => "Odczyt totalizatora";
    public override string Description => "Odczytuje licznik impulsów (totalizator) karty 34907A";
    public override Color BlockColor => Color.FromRgb(0x0E, 0x74, 0x90);
    public override string Category => "Agilent34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Check("ResetAfter", "Wyzeruj po odczycie", false),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "count"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        var (daq, error) = A34970ABlockHelpers.ResolveDriver(this, context, GetPropStr("InstrumentName"));
        if (error != null) return error;

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        bool resetAfter = GetProp<bool>("ResetAfter", false);
        string outVar = GetPropStr("OutputVariable", "count");

        try
        {
            double count = await daq!.ReadTotalizerAsync(slot);
            context.SetVariable(outVar, count);
            context.Log?.Invoke($"[34970A] Totalizer slot {slot}: {count:F0} → {outVar}");
            if (resetAfter)
                await daq.ResetTotalizerAsync(slot);
            return BlockExecutionResult.Ok(NextBlockId, count);
        }
        catch (Exception ex)
        {
            return BlockExecutionResult.Fail($"Błąd odczytu totalizatora: {ex.Message}");
        }
    }

    static A34970A_ReadTotalizer() =>
        BlockRegistry.Register("A34970A_ReadTotalizer", () => new A34970A_ReadTotalizer());
}

// ─────────────────────────────────────────────────────────────────────────────
// Registry helper
// ─────────────────────────────────────────────────────────────────────────────

public static class Agilent34970ABlocks
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_DetectCards).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_ScanChannels).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_ScanMixed).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_MeasureChannel).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_SetDAC).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_SetDigitalOutput).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_ReadDigitalInput).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(A34970A_ReadTotalizer).TypeHandle);
    }

    public static IEnumerable<ISequenceBlock> CreateAllBlocks() =>
    [
        new A34970A_DetectCards(),
        new A34970A_ScanChannels(),
        new A34970A_ScanMixed(),
        new A34970A_MeasureChannel(),
        new A34970A_SetDAC(),
        new A34970A_SetDigitalOutput(),
        new A34970A_ReadDigitalInput(),
        new A34970A_ReadTotalizer(),
    ];
}
