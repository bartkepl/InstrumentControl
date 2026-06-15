using System.Globalization;
using System.Windows;
using Agilent34970A.Cards;
using Agilent34970A.Views;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace Agilent34970A;

[InstrumentDriver]
public class Agilent34970ADriver : InstrumentDriverBase
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public override string DriverName => "Agilent34970A";
    public override string Manufacturer => "Agilent Technologies";
    public override string Model => "34970A";
    public override string Description => "Data Acquisition/Switch Unit";
    public override string[] SupportedResourcePatterns =>
        new[] { "GPIB?*::?*::INSTR", "USB?*::?*::INSTR" };

    // ── Card management ───────────────────────────────────────────────────────
    /// <summary>Slot number → Card object (Card34901A or Card34907A).</summary>
    public Dictionary<int, object> Cards { get; } = new();

    public void AddCard34901A(int slot)
    {
        Cards[slot] = new Card34901A(slot);
        RaiseStatus($"Skonfigurowano kartę 34901A w slocie {slot}");
    }

    public void AddCard34907A(int slot)
    {
        Cards[slot] = new Card34907A(slot);
        RaiseStatus($"Skonfigurowano kartę 34907A w slocie {slot}");
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Configure a list of channels in a single slot with a given function, scan them,
    /// and return one MeasurementResult per channel.
    /// </summary>
    public async Task<List<MeasurementResult>> ScanAsync(
        int slot,
        IEnumerable<int> channels,
        MuxFunction function,
        string range = "AUTO")
    {
        // Build the SCPI channel list using the card if registered, otherwise build manually.
        string channelList;
        string confCommand;

        var channelArray = channels.ToList();

        if (Cards.TryGetValue(slot, out var cardObj) && cardObj is Card34901A card)
        {
            channelList = card.GetChannelList(channelArray).TrimStart('@'); // "101,102,103"
            confCommand = card.BuildConfCommand(function, range, channelArray);
        }
        else
        {
            // Build manually
            var nums = channelArray.Select(ch => (slot + ch).ToString());
            channelList = string.Join(",", nums);
            confCommand = BuildConfCommandManual(function, range, channelList);
        }

        return await ExecuteScan(channelList, confCommand, function);
    }

    /// <summary>
    /// Scan an arbitrary set of channels specified as a SCPI channel list like "101,102,201:205".
    /// The function is always DC Voltage (intended as a quick multi-channel sweep).
    /// For typed scans use ScanAsync.
    /// </summary>
    public async Task<List<MeasurementResult>> ScanChannelListAsync(string scpiChannelList)
    {
        // Expand ranges like 201:205 → 201,202,203,204,205
        string expandedList = ExpandChannelList(scpiChannelList);
        string confCommand = $"CONF:VOLT:DC AUTO,DEF,(@{expandedList})";
        return await ExecuteScan(expandedList, confCommand, MuxFunction.VDC);
    }

    // ── DAC / Digital ─────────────────────────────────────────────────────────

    public async Task SetDacAsync(int slot, int dacChannel, double voltage)
    {
        var card = GetCard34907A(slot);
        string cmd = card.BuildSetDacCommand(dacChannel, voltage);
        await Write(cmd);
        RaiseStatus($"DAC{dacChannel} slot {slot}: {voltage:F4} V");
    }

    public async Task SetDigitalOutputAsync(int slot, byte value)
    {
        var card = GetCard34907A(slot);
        string cmd = card.BuildDigitalWriteCommand(value);
        await Write(cmd);
        RaiseStatus($"Digital OUT slot {slot}: 0x{value:X2}");
    }

    public async Task<byte> ReadDigitalInputAsync(int slot)
    {
        var card = GetCard34907A(slot);
        string cmd = card.BuildDigitalReadCommand();
        string response = await Query(cmd);
        if (byte.TryParse(response.Trim(), out byte result))
            return result;
        if (double.TryParse(response.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return (byte)(int)d;
        throw new InvalidOperationException($"Nieprawidłowa odpowiedź na Digital READ: '{response}'");
    }

    // ── Temperature TC scan ───────────────────────────────────────────────────

    public async Task<List<MeasurementResult>> MeasureTemperatureTCAsync(string channelList, string tcType = "K")
    {
        string expandedList = ExpandChannelList(channelList);
        string confCommand = $"CONF:TEMP TC,{tcType},(@{expandedList})";
        return await ExecuteScan(expandedList, confCommand, MuxFunction.TEMP_TC);
    }

    // ── Totalizer (34907A) ────────────────────────────────────────────────────

    public async Task<double> ReadTotalizerAsync(int slot)
    {
        string response = await Query($"MEAS:TOT? (@{slot}01)");
        return double.TryParse(response.Trim(), System.Globalization.NumberStyles.Float,
            CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
    }

    public async Task ResetTotalizerAsync(int slot)
    {
        await Write($"SENS:TOT:CLE (@{slot}01)");
        RaiseStatus($"Totalizer slot {slot} — reset");
    }

    // ── Plugin integration ────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() =>
        new Agilent34970AFrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() =>
        Agilent34970ABlocks.CreateAllBlocks();

    static Agilent34970ADriver()
    {
        Agilent34970ABlocks.RegisterAll();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<MeasurementResult>> ExecuteScan(
        string expandedChannelList,
        string confCommand,
        MuxFunction function)
    {
        // 1. Configure measurement
        await Write(confCommand);

        // 2. Set scan list
        await Write($"ROUT:SCAN (@{expandedChannelList})");

        // 3. Trigger a single sweep
        await Write("INIT");

        // 4. Wait for completion and fetch
        await Task.Delay(500); // allow instrument to settle
        string fetchResponse = await Query("FETCH?");

        // 5. Parse response
        var channelNumbers = ParseChannelNumbers(expandedChannelList);
        var values = ParseDoubleArray(fetchResponse);

        var results = new List<MeasurementResult>();
        for (int i = 0; i < Math.Min(channelNumbers.Count, values.Count); i++)
        {
            var r = new MeasurementResult
            {
                InstrumentName = DriverName,
                ChannelId = channelNumbers[i].ToString(),
                Function = function.ToString(),
                Value = values[i],
                Unit = GetUnit(function),
                IsValid = !double.IsNaN(values[i])
            };
            results.Add(r);
            RaiseMeasurement(r);
        }

        return results;
    }

    private static string BuildConfCommandManual(MuxFunction function, string range, string channelList) =>
        function switch
        {
            MuxFunction.VDC    => $"CONF:VOLT:DC {range},DEF,(@{channelList})",
            MuxFunction.VAC    => $"CONF:VOLT:AC {range},DEF,(@{channelList})",
            MuxFunction.OHM2W  => $"CONF:RES {range},DEF,(@{channelList})",
            MuxFunction.OHM4W  => $"CONF:FRES {range},DEF,(@{channelList})",
            MuxFunction.TEMP_TC  => $"CONF:TEMP TC,K,(@{channelList})",
            MuxFunction.TEMP_RTD => $"CONF:TEMP RTD,85,(@{channelList})",
            MuxFunction.FREQ   => $"CONF:FREQ {range},DEF,(@{channelList})",
            MuxFunction.PERIOD => $"CONF:PER {range},DEF,(@{channelList})",
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };

    private static string GetUnit(MuxFunction function) => function switch
    {
        MuxFunction.VDC or MuxFunction.VAC => "V",
        MuxFunction.OHM2W or MuxFunction.OHM4W => "Ω",
        MuxFunction.TEMP_TC or MuxFunction.TEMP_RTD => "°C",
        MuxFunction.FREQ => "Hz",
        MuxFunction.PERIOD => "s",
        _ => ""
    };

    private Card34907A GetCard34907A(int slot)
    {
        if (!Cards.TryGetValue(slot, out var cardObj) || cardObj is not Card34907A card)
            throw new InvalidOperationException(
                $"Slot {slot} nie zawiera karty 34907A. Użyj AddCard34907A(slot) najpierw.");
        return card;
    }

    /// <summary>
    /// Expands a channel list that may contain ranges (e.g. "101,201:205") to a flat comma-separated list.
    /// </summary>
    private static string ExpandChannelList(string input)
    {
        var result = new List<string>();
        foreach (var token in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            if (trimmed.Contains(':'))
            {
                var parts = trimmed.Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int start)
                    && int.TryParse(parts[1], out int end))
                {
                    for (int ch = start; ch <= end; ch++)
                        result.Add(ch.ToString());
                }
            }
            else
            {
                result.Add(trimmed);
            }
        }
        return string.Join(",", result);
    }

    private static List<int> ParseChannelNumbers(string expandedList)
    {
        var result = new List<int>();
        foreach (var token in expandedList.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token.Trim(), out int ch))
                result.Add(ch);
        }
        return result;
    }

    private static List<double> ParseDoubleArray(string response)
    {
        var result = new List<double>();
        foreach (var token in response.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            result.Add(double.TryParse(
                token.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double d) ? d : double.NaN);
        }
        return result;
    }
}
