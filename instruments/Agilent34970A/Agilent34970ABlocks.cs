using System.Windows.Media;
using Agilent34970A.Cards;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace Agilent34970A;

// ─────────────────────────────────────────────────────────────────────────────
// 1. A34970A_ScanChannels
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_ScanChannels : SequenceBlockBase
{
    public override string BlockType => "A34970A_ScanChannels";
    public override string DisplayName => "Skanuj kanały";
    public override string Description => "Konfiguruje i skanuje listę kanałów 34970A, wyniki zapisuje jako zmienne";
    public override Color BlockColor => Color.FromRgb(0x29, 0x80, 0xB9);
    public override string Category => "Agilent 34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Text("ChannelList", "Lista kanałów (SCPI)", "101,102,103"),
        BlockPropertyDefinition.Combo("Function", "Funkcja",
            new() { "VDC", "VAC", "OHM2W", "OHM4W", "FREQ" }, "VDC"),
        BlockPropertyDefinition.Text("Range", "Zakres", "AUTO"),
        BlockPropertyDefinition.Variable("OutputPrefix", "Prefiks zmiennej wyjściowej", "ch"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest aktywny.");

        if (driver is not Agilent34970ADriver daq)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest sterownikiem Agilent34970A.");

        string channelList = GetPropStr("ChannelList", "101,102,103");
        string functionStr = GetPropStr("Function", "VDC");
        string prefix = GetPropStr("OutputPrefix", "ch");

        try
        {
            context.Log?.Invoke($"[34970A] Skanowanie kanałów: {channelList}, funkcja: {functionStr}");
            var results = await daq.ScanChannelListAsync(channelList);

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
// 2. A34970A_MeasureChannel
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_MeasureChannel : SequenceBlockBase
{
    public override string BlockType => "A34970A_MeasureChannel";
    public override string DisplayName => "Zmierz kanał";
    public override string Description => "Mierzy jeden kanał 34970A i zapisuje wynik w zmiennej";
    public override Color BlockColor => Color.FromRgb(0x27, 0xAE, 0x60);
    public override string Category => "Agilent 34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Number("Channel", "Kanał (1-20)", 1),
        BlockPropertyDefinition.Combo("Function", "Funkcja",
            new() { "VDC", "VAC", "OHM2W", "OHM4W", "TEMP_TC", "TEMP_RTD", "FREQ", "PERIOD" }, "VDC"),
        BlockPropertyDefinition.Text("Range", "Zakres", "AUTO"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "chValue"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest aktywny.");

        if (driver is not Agilent34970ADriver daq)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest sterownikiem Agilent34970A.");

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
            var results = await daq.ScanAsync(slot, new[] { channel }, function, range);

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
// 3. A34970A_SetDAC
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_SetDAC : SequenceBlockBase
{
    public override string BlockType => "A34970A_SetDAC";
    public override string DisplayName => "Ustaw DAC";
    public override string Description => "Ustawia wyjście DAC na karcie 34907A";
    public override Color BlockColor => Color.FromRgb(0xE7, 0x4C, 0x3C);
    public override string Category => "Agilent 34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Combo("DacChannel", "Kanał DAC", new() { "1", "2" }, "1"),
        BlockPropertyDefinition.Number("Voltage", "Napięcie [V]", 0.0),
        BlockPropertyDefinition.Text("VoltageVariable", "Zmienna napięcia (opcjonalne)", ""),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest aktywny.");

        if (driver is not Agilent34970ADriver daq)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest sterownikiem Agilent34970A.");

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        int dacChannel = int.TryParse(GetPropStr("DacChannel", "1"), out int dc) ? dc : 1;
        string voltVar = GetPropStr("VoltageVariable", "");

        double voltage;
        if (!string.IsNullOrWhiteSpace(voltVar))
            voltage = context.GetVariableAsDouble(voltVar, 0.0);
        else
            voltage = GetProp<double>("Voltage", 0.0);

        try
        {
            context.Log?.Invoke($"[34970A] DAC{dacChannel} slot {slot}: {voltage:F4} V");
            await daq.SetDacAsync(slot, dacChannel, voltage);
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
// 4. A34970A_SetDigitalOutput
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_SetDigitalOutput : SequenceBlockBase
{
    public override string BlockType => "A34970A_SetDigitalOutput";
    public override string DisplayName => "Cyfrowe wyjście";
    public override string Description => "Ustawia bajt wyjścia cyfrowego na karcie 34907A";
    public override Color BlockColor => Color.FromRgb(0x8E, 0x44, 0xAD);
    public override string Category => "Agilent 34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Number("Value", "Wartość (0-255)", 0),
        BlockPropertyDefinition.Text("ValueVariable", "Zmienna wartości (opcjonalne)", ""),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest aktywny.");

        if (driver is not Agilent34970ADriver daq)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest sterownikiem Agilent34970A.");

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        string valVar = GetPropStr("ValueVariable", "");

        byte value;
        if (!string.IsNullOrWhiteSpace(valVar))
        {
            double varVal = context.GetVariableAsDouble(valVar, 0);
            value = (byte)Math.Clamp((int)varVal, 0, 255);
        }
        else
        {
            double propVal = GetProp<double>("Value", 0);
            value = (byte)Math.Clamp((int)propVal, 0, 255);
        }

        try
        {
            context.Log?.Invoke($"[34970A] Digital OUT slot {slot}: 0x{value:X2} ({value})");
            await daq.SetDigitalOutputAsync(slot, value);
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
// 5. A34970A_ReadDigitalInput
// ─────────────────────────────────────────────────────────────────────────────

public class A34970A_ReadDigitalInput : SequenceBlockBase
{
    public override string BlockType => "A34970A_ReadDigitalInput";
    public override string DisplayName => "Cyfrowe wejście";
    public override string Description => "Odczytuje bajt wejścia cyfrowego z karty 34907A";
    public override Color BlockColor => Color.FromRgb(0x16, 0xA0, 0x85);
    public override string Category => "Agilent 34970A";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Instrument("InstrumentName"),
        BlockPropertyDefinition.Combo("Slot", "Slot", new() { "100", "200", "300" }, "100"),
        BlockPropertyDefinition.Variable("OutputVariable", "Zmienna wyjściowa", "digitalIn"),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string instrName = GetPropStr("InstrumentName");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest aktywny.");

        if (driver is not Agilent34970ADriver daq)
            return BlockExecutionResult.Fail($"Instrument '{instrName}' nie jest sterownikiem Agilent34970A.");

        int slot = int.TryParse(GetPropStr("Slot", "100"), out int s) ? s : 100;
        string outVar = GetPropStr("OutputVariable", "digitalIn");

        try
        {
            context.Log?.Invoke($"[34970A] Digital IN slot {slot}...");
            byte value = await daq.ReadDigitalInputAsync(slot);
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
// Registry helper
// ─────────────────────────────────────────────────────────────────────────────

public static class Agilent34970ABlocks
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        // Force static constructors of each block class to run,
        // which calls BlockRegistry.Register for each type.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(A34970A_ScanChannels).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(A34970A_MeasureChannel).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(A34970A_SetDAC).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(A34970A_SetDigitalOutput).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(A34970A_ReadDigitalInput).TypeHandle);
    }

    public static IEnumerable<ISequenceBlock> CreateAllBlocks() =>
    [
        new A34970A_ScanChannels(),
        new A34970A_MeasureChannel(),
        new A34970A_SetDAC(),
        new A34970A_SetDigitalOutput(),
        new A34970A_ReadDigitalInput(),
    ];
}
