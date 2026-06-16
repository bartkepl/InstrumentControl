using System.Globalization;
using System.IO;
using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using RTB2004.Views;

namespace RTB2004;

[InstrumentDriver]
public class RTB2004Driver : InstrumentDriverBase
{
    public override string DriverName => "RTB2004";
    public override string Manufacturer => "Rohde & Schwarz";
    public override string Model => "RTB2004";
    public override string Description => "Oscyloskop cyfrowy 4-kanałowy 300 MHz";
    public override string[] SupportedResourcePatterns => new[]
    {
        "TCPIP?*::?*::INSTR",
        "TCPIP?*::hislip?*::INSTR",
        "USB?*::?*::INSTR",
        "GPIB?*::?*::INSTR"
    };

    static RTB2004Driver()
    {
        RTB2004Blocks.RegisterAll();
    }

    // ── Channel configuration ────────────────────────────────────────────────

    public async Task SetChannelEnabledAsync(int channel, bool on) =>
        await Write($"CHAN{channel}:STAT {(on ? "ON" : "OFF")}");

    public async Task<bool> GetChannelEnabledAsync(int channel)
    {
        string r = await Query($"CHAN{channel}:STAT?");
        return r.Trim() is "1" or "ON";
    }

    public async Task SetChannelScaleAsync(int channel, double voltsPerDiv) =>
        await Write($"CHAN{channel}:SCAL {voltsPerDiv.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetChannelScaleAsync(int channel) =>
        await QueryDouble($"CHAN{channel}:SCAL?");

    public async Task SetChannelOffsetAsync(int channel, double offset) =>
        await Write($"CHAN{channel}:OFFS {offset.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetChannelOffsetAsync(int channel) =>
        await QueryDouble($"CHAN{channel}:OFFS?");

    public async Task SetChannelCouplingAsync(int channel, string coupling) =>
        await Write($"CHAN{channel}:COUP {coupling.ToUpperInvariant()}");

    public async Task<string> GetChannelCouplingAsync(int channel) =>
        (await Query($"CHAN{channel}:COUP?")).Trim();

    public async Task SetChannelProbeAsync(int channel, double ratio) =>
        await Write($"CHAN{channel}:PROB {ratio.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetChannelProbeAsync(int channel) =>
        await QueryDouble($"CHAN{channel}:PROB?");

    public async Task SetChannelPolarityAsync(int channel, bool inverted) =>
        await Write($"CHAN{channel}:POL {(inverted ? "INV" : "NORM")}");

    public async Task SetChannelBandwidthAsync(int channel, bool limit200MHz) =>
        await Write($"CHAN{channel}:BAND {(limit200MHz ? "B200" : "FULL")}");

    // ── Timebase ─────────────────────────────────────────────────────────────

    public async Task SetTimescaleAsync(double secsPerDiv) =>
        await Write($"TIM:SCAL {secsPerDiv.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetTimescaleAsync() =>
        await QueryDouble("TIM:SCAL?");

    public async Task SetTimePositionAsync(double position) =>
        await Write($"TIM:POS {position.ToString(CultureInfo.InvariantCulture)}");

    // ── Trigger ───────────────────────────────────────────────────────────────

    public async Task SetTriggerSourceAsync(string source) =>
        await Write($"TRIG:A:SOUR {source.ToUpperInvariant()}");

    public async Task<string> GetTriggerSourceAsync() =>
        (await Query("TRIG:A:SOUR?")).Trim();

    public async Task SetTriggerLevelAsync(int channel, double level) =>
        await Write($"TRIG:A:LEV{channel} {level.ToString(CultureInfo.InvariantCulture)}");

    public async Task SetTriggerSlopeAsync(string slope) =>
        await Write($"TRIG:A:EDGE:SLOP {slope.ToUpperInvariant()}");

    public async Task SetTriggerModeAsync(string mode) =>
        await Write($"TRIG:A:MODE {mode.ToUpperInvariant()}");

    public async Task SetTriggerCouplingAsync(string coupling) =>
        await Write($"TRIG:A:EDGE:COUP {coupling.ToUpperInvariant()}");

    // ── Acquisition control ───────────────────────────────────────────────────

    public async Task RunAsync()
    {
        await Write("RUN");
        RaiseStatus("Akwizycja: CIĄGŁA");
    }

    public async Task StopAsync()
    {
        await Write("STOP");
        RaiseStatus("Akwizycja: STOP");
    }

    public async Task SingleAsync()
    {
        await Write("SING");
        RaiseStatus("Akwizycja: SINGLE");
    }

    public async Task AutoscaleAsync()
    {
        await Write("AUT");
        RaiseStatus("Autoskalowanie wykonane");
    }

    // ── Measurements ──────────────────────────────────────────────────────────

    public async Task SetMeasurementAsync(int slot, int channel, string measType)
    {
        string ch = $"CH{channel}";
        await Write($"MEAS{slot}:SOUR {ch}");
        await Write($"MEAS{slot}:MAIN {measType.ToUpperInvariant()}");
        await Write($"MEAS{slot}:ENAB ON");
    }

    public async Task<double> GetMeasurementResultAsync(int slot) =>
        await QueryDouble($"MEAS{slot}:RES:ACT?");

    public async Task<double> MeasureChannelAsync(int channel, string measType)
    {
        await SetMeasurementAsync(1, channel, measType);
        await Task.Delay(150);  // allow one acquisition cycle
        double result = await GetMeasurementResultAsync(1);
        RaiseMeasurement(new MeasurementResult
        {
            Function = measType, Unit = GetMeasUnit(measType), Value = result,
            InstrumentName = DriverName,
            ChannelId = $"CH{channel}",
            ParameterName = $"{measType.ToLower()}_ch{channel}"
        });
        return result;
    }

    // ── Waveform readout ──────────────────────────────────────────────────────

    public async Task<(double[] Voltages, double XStart, double XIncrement)> ReadWaveformAsync(int channel)
    {
        await Write("FORM ASC");
        await Write($"CHAN{channel}:DATA:POIN MAX");

        string header = await Query($"CHAN{channel}:DATA:HEAD?");
        (double xStart, double xInc) = ParseWaveformHeader(header);

        string raw = await Query($"CHAN{channel}:DATA?");
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var voltages = new List<double>(parts.Length);
        foreach (var p in parts)
        {
            if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                voltages.Add(d);
        }
        return (voltages.ToArray(), xStart, xInc);
    }

    private static (double XStart, double XIncrement) ParseWaveformHeader(string header)
    {
        var parts = header.Split(',', StringSplitOptions.RemoveEmptyEntries);
        double xStart = parts.Length > 0 && double.TryParse(parts[0].Trim(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : 0.0;
        double xStop  = parts.Length > 1 && double.TryParse(parts[1].Trim(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out double e) ? e : 1e-3;
        int    nPts   = parts.Length > 2 && int.TryParse(parts[2].Trim(), out int n) ? n : 1000;
        double xInc   = nPts > 1 ? (xStop - xStart) / (nPts - 1) : 1e-6;
        return (xStart, xInc);
    }

    public async Task SaveWaveformToCsvAsync(int channel, string filePath)
    {
        var (voltages, xStart, xInc) = await ReadWaveformAsync(channel);
        var lines = new List<string>(voltages.Length + 1) { "Time[s],Voltage[V]" };
        for (int i = 0; i < voltages.Length; i++)
            lines.Add($"{(xStart + i * xInc).ToString(CultureInfo.InvariantCulture)},{voltages[i].ToString(CultureInfo.InvariantCulture)}");
        await File.WriteAllLinesAsync(filePath, lines);
        RaiseStatus($"Przebiegi CH{channel} zapisane: {filePath}");
    }

    // ── Plugin interface ──────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() =>
        new RTB2004FrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => new ISequenceBlock[]
    {
        new RTB2004_SetChannel(),
        new RTB2004_SetTimebase(),
        new RTB2004_SetTrigger(),
        new RTB2004_Run(),
        new RTB2004_Stop(),
        new RTB2004_Single(),
        new RTB2004_Autoscale(),
        new RTB2004_Measure(),
        new RTB2004_ReadWaveform(),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetMeasUnit(string measType) => measType.ToUpperInvariant() switch
    {
        "FREQ" or "FREQUENCY" => "Hz",
        "PERI" or "PERIOD"    => "s",
        "RMS"                 => "V",
        "MEAN"                => "V",
        "AMPL" or "AMP"       => "V",
        "PK2PK"               => "V",
        "PHAS" or "PHASE"     => "°",
        "DEL"  or "DELAY"     => "s",
        "RISE" or "CRISE"     => "s",
        "FALL" or "FFALL"     => "s",
        "PWID" or "POSPULSE"  => "s",
        "NWID" or "NEGPULSE"  => "s",
        "DCYC" or "POSDUTY"   => "%",
        _                     => "",
    };
}
