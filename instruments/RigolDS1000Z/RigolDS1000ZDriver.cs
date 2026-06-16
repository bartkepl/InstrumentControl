using System.Globalization;
using System.IO;
using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using RigolDS1000Z.Views;

namespace RigolDS1000Z;

[InstrumentDriver]
public class RigolDS1000ZDriver : InstrumentDriverBase
{
    public override string DriverName => "RigolDS1000Z";
    public override string Manufacturer => "Rigol";
    public override string Model => "DS1054Z/DS1104Z";
    public override string Description => "Oscyloskop cyfrowy 4-kanałowy 50/100 MHz";
    public override string[] SupportedResourcePatterns => new[]
    {
        "TCPIP?*::?*::INSTR",
        "TCPIP?*::hislip?*::INSTR",
        "USB?*::0x1AB1::?*::INSTR",
        "GPIB?*::?*::INSTR"
    };

    static RigolDS1000ZDriver()
    {
        RigolDS1000ZBlocks.RegisterAll();
    }

    // ── Channel configuration ────────────────────────────────────────────────

    public async Task SetChannelDisplayAsync(int ch, bool on) =>
        await Write($":CHANnel{ch}:DISPlay {(on ? "ON" : "OFF")}");

    public async Task<bool> GetChannelDisplayAsync(int ch)
    {
        string r = await Query($":CHANnel{ch}:DISPlay?");
        return r.Trim() is "1" or "ON";
    }

    public async Task SetChannelCouplingAsync(int ch, string coupling) =>
        await Write($":CHANnel{ch}:COUPling {coupling.ToUpperInvariant()}");

    public async Task<string> GetChannelCouplingAsync(int ch) =>
        (await Query($":CHANnel{ch}:COUPling?")).Trim();

    public async Task SetChannelScaleAsync(int ch, double vPerDiv) =>
        await Write($":CHANnel{ch}:SCALe {vPerDiv.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetChannelScaleAsync(int ch) =>
        await QueryDouble($":CHANnel{ch}:SCALe?");

    public async Task SetChannelOffsetAsync(int ch, double offset) =>
        await Write($":CHANnel{ch}:OFFSet {offset.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetChannelOffsetAsync(int ch) =>
        await QueryDouble($":CHANnel{ch}:OFFSet?");

    public async Task SetChannelBandwidthAsync(int ch, string bwLimit) =>
        await Write($":CHANnel{ch}:BWLimit {bwLimit.ToUpperInvariant()}");

    public async Task SetChannelInvertAsync(int ch, bool invert) =>
        await Write($":CHANnel{ch}:INVert {(invert ? "ON" : "OFF")}");

    public async Task<bool> GetChannelInvertAsync(int ch)
    {
        string r = await Query($":CHANnel{ch}:INVert?");
        return r.Trim() is "1" or "ON";
    }

    public async Task SetChannelProbeAsync(int ch, double ratio) =>
        await Write($":CHANnel{ch}:PROBe {ratio.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetChannelProbeAsync(int ch) =>
        await QueryDouble($":CHANnel{ch}:PROBe?");

    public async Task SetChannelUnitAsync(int ch, string unit) =>
        await Write($":CHANnel{ch}:UNIT {unit.ToUpperInvariant()}");

    public async Task<string> GetChannelUnitAsync(int ch) =>
        (await Query($":CHANnel{ch}:UNIT?")).Trim();

    public async Task SetChannelVernierAsync(int ch, bool on) =>
        await Write($":CHANnel{ch}:VERNier {(on ? "ON" : "OFF")}");

    // ── Timebase ─────────────────────────────────────────────────────────────

    public async Task SetTimebaseScaleAsync(double sPerDiv) =>
        await Write($":TIMebase:MAIN:SCALe {sPerDiv.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetTimebaseScaleAsync() =>
        await QueryDouble(":TIMebase:MAIN:SCALe?");

    public async Task SetTimebaseOffsetAsync(double offset) =>
        await Write($":TIMebase:MAIN:OFFSet {offset.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetTimebaseOffsetAsync() =>
        await QueryDouble(":TIMebase:MAIN:OFFSet?");

    public async Task SetTimebaseModeAsync(string mode) =>
        await Write($":TIMebase:MODE {mode.ToUpperInvariant()}");

    public async Task<string> GetTimebaseModeAsync() =>
        (await Query(":TIMebase:MODE?")).Trim();

    // ── Trigger ───────────────────────────────────────────────────────────────

    public async Task SetTriggerModeAsync(string mode) =>
        await Write($":TRIGger:MODE {mode.ToUpperInvariant()}");

    public async Task<string> GetTriggerModeAsync() =>
        (await Query(":TRIGger:MODE?")).Trim();

    public async Task SetTriggerSweepAsync(string sweep) =>
        await Write($":TRIGger:SWEep {sweep.ToUpperInvariant()}");

    public async Task<string> GetTriggerSweepAsync() =>
        (await Query(":TRIGger:SWEep?")).Trim();

    public async Task SetTriggerEdgeSourceAsync(string source) =>
        await Write($":TRIGger:EDGE:SOURce {NormalizeSource(source)}");

    public async Task<string> GetTriggerEdgeSourceAsync() =>
        (await Query(":TRIGger:EDGE:SOURce?")).Trim();

    public async Task SetTriggerEdgeSlopeAsync(string slope) =>
        await Write($":TRIGger:EDGE:SLOPe {slope.ToUpperInvariant()}");

    public async Task<string> GetTriggerEdgeSlopeAsync() =>
        (await Query(":TRIGger:EDGE:SLOPe?")).Trim();

    public async Task SetTriggerEdgeLevelAsync(double level) =>
        await Write($":TRIGger:EDGE:LEVel {level.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetTriggerEdgeLevelAsync() =>
        await QueryDouble(":TRIGger:EDGE:LEVel?");

    public async Task SetTriggerCouplingAsync(string coupling) =>
        await Write($":TRIGger:COUPling {coupling.ToUpperInvariant()}");

    public async Task ForceTriggerAsync() =>
        await Write(":TFORce");

    public async Task<string> GetTriggerStatusAsync() =>
        (await Query(":TRIGger:STATus?")).Trim();

    // Pulse trigger
    public async Task SetTriggerPulseSourceAsync(string source) =>
        await Write($":TRIGger:PULSe:SOURce {NormalizeSource(source)}");

    public async Task SetTriggerPulseSlopeAsync(string slope) =>
        await Write($":TRIGger:PULSe:SLOPe {slope.ToUpperInvariant()}");

    public async Task SetTriggerPulseWhenAsync(string when) =>
        await Write($":TRIGger:PULSe:WHEN {when.ToUpperInvariant()}");

    public async Task SetTriggerPulseWidthAsync(double width) =>
        await Write($":TRIGger:PULSe:WIDTh {width.ToString(CultureInfo.InvariantCulture)}");

    public async Task SetTriggerPulseLevelAsync(double level) =>
        await Write($":TRIGger:PULSe:LEVel {level.ToString(CultureInfo.InvariantCulture)}");

    // Slope trigger
    public async Task SetTriggerSlopeSlopeAsync(string slope) =>
        await Write($":TRIGger:SLOPe:SLOPe {slope.ToUpperInvariant()}");

    public async Task SetTriggerSlopeSourceAsync(string source) =>
        await Write($":TRIGger:SLOPe:SOURce {NormalizeSource(source)}");

    public async Task SetTriggerSlopeTimeAsync(double time) =>
        await Write($":TRIGger:SLOPe:TIME {time.ToString(CultureInfo.InvariantCulture)}");

    // ── Acquisition control ───────────────────────────────────────────────────

    public async Task RunAsync()
    {
        await Write(":RUN");
        RaiseStatus("Akwizycja: CIĄGŁA");
    }

    public async Task StopAsync()
    {
        await Write(":STOP");
        RaiseStatus("Akwizycja: STOP");
    }

    public async Task SingleAsync()
    {
        await Write(":SINGle");
        RaiseStatus("Akwizycja: SINGLE");
    }

    public async Task AutoScaleAsync()
    {
        await Write(":AUToset");
        RaiseStatus("Auto Scale wykonane");
    }

    public async Task ClearDisplayAsync() =>
        await Write(":CLEar");

    // ── Acquire settings ──────────────────────────────────────────────────────

    public async Task SetAcquireTypeAsync(string type) =>
        await Write($":ACQuire:TYPE {type.ToUpperInvariant()}");

    public async Task<string> GetAcquireTypeAsync() =>
        (await Query(":ACQuire:TYPE?")).Trim();

    public async Task SetAcquireAveragesAsync(int count) =>
        await Write($":ACQuire:AVERages {count}");

    public async Task<int> GetAcquireAveragesAsync()
    {
        string r = await Query(":ACQuire:AVERages?");
        return int.TryParse(r.Trim(), out int n) ? n : 2;
    }

    public async Task SetAcquireMemDepthAsync(string depth) =>
        await Write($":ACQuire:MDEPth {depth.ToUpperInvariant()}");

    public async Task<string> GetAcquireMemDepthAsync() =>
        (await Query(":ACQuire:MDEPth?")).Trim();

    public async Task<double> GetAcquireSampleRateAsync() =>
        await QueryDouble(":ACQuire:SRATe?");

    // ── Measurements ──────────────────────────────────────────────────────────

    private static string ChannelSource(int ch) => $"CHANnel{ch}";

    // Rigol SCPI requires CHANnel<n> (min abbreviation "CHAN") for trigger/math sources;
    // the UI and sequence blocks use the short "CH1".."CH4" tokens. Non-channel tokens
    // (AC, FX, EXT, D0..D15) pass through unchanged.
    private static string NormalizeSource(string source)
    {
        string s = source.Trim();
        return s.Length == 3 && (s[0] is 'C' or 'c') && (s[1] is 'H' or 'h') && char.IsDigit(s[2])
            ? $"CHANnel{s[2]}"
            : s.ToUpperInvariant();
    }

    public async Task<double> MeasureAsync(string param, int ch)
    {
        string src = ChannelSource(ch);
        double result = await QueryDouble($":MEASure:{param}? {src}");
        RaiseMeasurement(new MeasurementResult
        {
            Function      = param,
            Unit          = GetMeasUnit(param),
            Value         = result,
            InstrumentName = DriverName,
            ChannelId     = $"CH{ch}",
            ParameterName = $"{param.ToLower()}_ch{ch}",
        });
        return result;
    }

    public async Task<double> MeasureVmaxAsync(int ch)   => await MeasureAsync("VMAX",        ch);
    public async Task<double> MeasureVminAsync(int ch)   => await MeasureAsync("VMIN",        ch);
    public async Task<double> MeasureVppAsync(int ch)    => await MeasureAsync("VPP",         ch);
    public async Task<double> MeasureVtopAsync(int ch)   => await MeasureAsync("VTOP",        ch);
    public async Task<double> MeasureVbaseAsync(int ch)  => await MeasureAsync("VBASe",       ch);
    public async Task<double> MeasureVampAsync(int ch)   => await MeasureAsync("VAMP",        ch);
    public async Task<double> MeasureVavgAsync(int ch)   => await MeasureAsync("VAVG",        ch);
    public async Task<double> MeasureVrmsAsync(int ch)   => await MeasureAsync("VRMS",        ch);
    public async Task<double> MeasureOvershootAsync(int ch)  => await MeasureAsync("OVERshoot", ch);
    public async Task<double> MeasurePreshootAsync(int ch)   => await MeasureAsync("PREShoot",  ch);
    public async Task<double> MeasureFrequencyAsync(int ch)  => await MeasureAsync("FREQuency", ch);
    public async Task<double> MeasurePeriodAsync(int ch)     => await MeasureAsync("PERiod",    ch);
    public async Task<double> MeasureRiseTimeAsync(int ch)   => await MeasureAsync("RISetime",  ch);
    public async Task<double> MeasureFallTimeAsync(int ch)   => await MeasureAsync("FALLtime",  ch);
    public async Task<double> MeasurePWidthAsync(int ch)     => await MeasureAsync("PWIDth",    ch);
    public async Task<double> MeasureNWidthAsync(int ch)     => await MeasureAsync("NWIDth",    ch);
    public async Task<double> MeasurePDutyCycleAsync(int ch) => await MeasureAsync("PDUTycycle",ch);
    public async Task<double> MeasureNDutyCycleAsync(int ch) => await MeasureAsync("NDUTycycle",ch);

    public async Task<double> MeasureDelayAsync(int ch1, int ch2)
    {
        string s1 = ChannelSource(ch1), s2 = ChannelSource(ch2);
        return await QueryDouble($":MEASure:DELay? {s1},{s2}");
    }

    public async Task<double> MeasurePhaseAsync(int ch1, int ch2)
    {
        string s1 = ChannelSource(ch1), s2 = ChannelSource(ch2);
        return await QueryDouble($":MEASure:PHASe? {s1},{s2}");
    }

    public async Task ClearMeasurementsAsync() =>
        await Write(":MEASure:CLEar ALL");

    public async Task<double> GetMeasureStatisticAsync(string stat, string param, int ch)
    {
        string src = ChannelSource(ch);
        return await QueryDouble($":MEASure:STATistic:ITEM? {stat},{param},{src}");
    }

    // ── Waveform readout ──────────────────────────────────────────────────────

    public async Task SetWaveformSourceAsync(int ch) =>
        await Write($":WAVeform:SOURce CHANnel{ch}");

    public async Task SetWaveformMathSourceAsync() =>
        await Write(":WAVeform:SOURce MATH");

    public async Task SetWaveformModeAsync(string mode) =>
        await Write($":WAVeform:MODE {mode.ToUpperInvariant()}");

    public async Task SetWaveformFormatAsync(string format) =>
        await Write($":WAVeform:FORMat {format.ToUpperInvariant()}");

    public async Task SetWaveformPointsAsync(int points) =>
        await Write($":WAVeform:POINts {points}");

    public record WaveformParams(double XInc, double XOrigin, double XRef,
                                  double YInc, double YOrigin, double YRef);

    public async Task<WaveformParams> GetWaveformParamsAsync()
    {
        double xInc    = await QueryDouble(":WAVeform:XINCrement?");
        double xOrigin = await QueryDouble(":WAVeform:XORigin?");
        double xRef    = await QueryDouble(":WAVeform:XREFerence?");
        double yInc    = await QueryDouble(":WAVeform:YINCrement?");
        double yOrigin = await QueryDouble(":WAVeform:YORigin?");
        double yRef    = await QueryDouble(":WAVeform:YREFerence?");
        return new WaveformParams(xInc, xOrigin, xRef, yInc, yOrigin, yRef);
    }

    public async Task<double[]> ReadWaveformAsciiAsync(int ch)
    {
        await SetWaveformSourceAsync(ch);
        await SetWaveformModeAsync("NORMal");
        await SetWaveformFormatAsync("ASCii");

        string raw = await Query(":WAVeform:DATA?");
        var parts  = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<double>(parts.Length);
        foreach (var p in parts)
            if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                result.Add(d);
        return result.ToArray();
    }

    public async Task<double[]> ReadWaveformByteAsync(int ch)
    {
        await SetWaveformSourceAsync(ch);
        await SetWaveformModeAsync("NORMal");
        await SetWaveformFormatAsync("BYTE");

        var prm  = await GetWaveformParamsAsync();
        if (Connection == null) return Array.Empty<double>();

        await Connection.WriteAsync(":WAVeform:DATA?");
        byte[] raw = await Connection.ReadRawAsync(2_000_000);
        byte[] data = ParseIeeeBinaryBlock(raw);

        var voltages = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
            voltages[i] = (data[i] - prm.YRef) * prm.YInc + prm.YOrigin;
        return voltages;
    }

    public async Task<(double[] Voltages, double XStart, double XIncrement)> ReadWaveformAsync(int ch)
    {
        await SetWaveformSourceAsync(ch);
        await SetWaveformModeAsync("NORMal");
        await SetWaveformFormatAsync("ASCii");

        var prm    = await GetWaveformParamsAsync();
        string raw = await Query(":WAVeform:DATA?");
        var parts  = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var volts  = new List<double>(parts.Length);
        foreach (var p in parts)
            if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                volts.Add(d);

        double xStart = prm.XOrigin + prm.XRef * prm.XInc;
        return (volts.ToArray(), xStart, prm.XInc);
    }

    public async Task SaveWaveformToCsvAsync(int ch, string filePath)
    {
        var (voltages, xStart, xInc) = await ReadWaveformAsync(ch);
        var lines = new List<string>(voltages.Length + 1) { "Czas[s],Napięcie[V]" };
        for (int i = 0; i < voltages.Length; i++)
            lines.Add($"{(xStart + i * xInc).ToString(CultureInfo.InvariantCulture)},{voltages[i].ToString(CultureInfo.InvariantCulture)}");
        await File.WriteAllLinesAsync(filePath, lines);
        RaiseStatus($"Przebieg CH{ch} zapisany: {filePath}");
    }

    // ── Math channel ──────────────────────────────────────────────────────────

    public async Task SetMathOperationAsync(string op) =>
        await Write($":MATH:OPERate {op.ToUpperInvariant()}");

    public async Task SetMathSource1Async(string source) =>
        await Write($":MATH:SOURce1 {NormalizeSource(source)}");

    public async Task SetMathSource2Async(string source) =>
        await Write($":MATH:SOURce2 {NormalizeSource(source)}");

    public async Task SetMathScaleAsync(double scale) =>
        await Write($":MATH:SCALe {scale.ToString(CultureInfo.InvariantCulture)}");

    public async Task SetMathOffsetAsync(double offset) =>
        await Write($":MATH:OFFSet {offset.ToString(CultureInfo.InvariantCulture)}");

    public async Task SetMathDisplayAsync(bool on) =>
        await Write($":MATH:DISPlay {(on ? "ON" : "OFF")}");

    // FFT math
    public async Task SetFFTSourceAsync(string source) =>
        await Write($":MATH:FFT:SOURce {NormalizeSource(source)}");

    public async Task SetFFTWindowAsync(string window) =>
        await Write($":MATH:FFT:WINDow {window.ToUpperInvariant()}");

    public async Task SetFFTSplitAsync(bool split) =>
        await Write($":MATH:FFT:SPLit {(split ? "ON" : "OFF")}");

    public async Task SetFFTUnitAsync(string unit) =>
        await Write($":MATH:FFT:UNIT {unit.ToUpperInvariant()}");

    public async Task SetFFTHScaleAsync(double hzPerDiv) =>
        await Write($":MATH:FFT:HSCALe {hzPerDiv.ToString(CultureInfo.InvariantCulture)}");

    public async Task SetFFTHCenterAsync(double freqHz) =>
        await Write($":MATH:FFT:HCENter {freqHz.ToString(CultureInfo.InvariantCulture)}");

    // ── Display ───────────────────────────────────────────────────────────────

    public async Task SetDisplayTypeAsync(string type) =>
        await Write($":DISPlay:TYPE {type.ToUpperInvariant()}");

    public async Task SetDisplayGridAsync(string mode) =>
        await Write($":DISPlay:GRATing {mode.ToUpperInvariant()}");

    public async Task SetWaveformBrightnessAsync(int brightness) =>
        await Write($":DISPlay:INTensity:WBRightness {brightness}");

    public async Task SetGridBrightnessAsync(int brightness) =>
        await Write($":DISPlay:INTensity:GBRightness {brightness}");

    // ── Cursor ────────────────────────────────────────────────────────────────

    public async Task SetCursorModeAsync(string mode) =>
        await Write($":CURSor:MODE {mode.ToUpperInvariant()}");

    public async Task SetCursorTypeAsync(string type) =>
        await Write($":CURSor:MANual:TYPE {type.ToUpperInvariant()}");

    public async Task SetCursorSourceAsync(int ch) =>
        await Write($":CURSor:MANual:SOURce CHANnel{ch}");

    public async Task SetCursorAXAsync(int steps) =>
        await Write($":CURSor:MANual:AX {steps}");

    public async Task SetCursorBXAsync(int steps) =>
        await Write($":CURSor:MANual:BX {steps}");

    public async Task SetCursorAYAsync(int steps) =>
        await Write($":CURSor:MANual:AY {steps}");

    public async Task SetCursorBYAsync(int steps) =>
        await Write($":CURSor:MANual:BY {steps}");

    public async Task<double> GetCursorAXValueAsync() => await QueryDouble(":CURSor:MANual:AXValue?");
    public async Task<double> GetCursorBXValueAsync() => await QueryDouble(":CURSor:MANual:BXValue?");
    public async Task<double> GetCursorXDeltaAsync()  => await QueryDouble(":CURSor:MANual:XDELta?");
    public async Task<double> GetCursorAYValueAsync() => await QueryDouble(":CURSor:MANual:AYValue?");
    public async Task<double> GetCursorBYValueAsync() => await QueryDouble(":CURSor:MANual:BYValue?");
    public async Task<double> GetCursorYDeltaAsync()  => await QueryDouble(":CURSor:MANual:YDELta?");

    // ── Screenshot ────────────────────────────────────────────────────────────

    public async Task<byte[]> TakeScreenshotAsync()
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        await Connection.WriteAsync(":DISPlay:DATA?");
        byte[] raw = await Connection.ReadRawAsync(5_000_000);
        return ParseIeeeBinaryBlock(raw);
    }

    // ── Plugin interface ──────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() =>
        new RigolDS1000ZFrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => new ISequenceBlock[]
    {
        new RigolDS1000Z_SetChannel(),
        new RigolDS1000Z_SetTimebase(),
        new RigolDS1000Z_SetTrigger(),
        new RigolDS1000Z_SetAcquire(),
        new RigolDS1000Z_Run(),
        new RigolDS1000Z_Stop(),
        new RigolDS1000Z_Single(),
        new RigolDS1000Z_AutoScale(),
        new RigolDS1000Z_MeasureVoltage(),
        new RigolDS1000Z_MeasureTime(),
        new RigolDS1000Z_MeasureDuty(),
        new RigolDS1000Z_ReadWaveform(),
        new RigolDS1000Z_MathSetup(),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] ParseIeeeBinaryBlock(byte[] raw)
    {
        if (raw.Length < 3 || raw[0] != (byte)'#') return raw;
        int nDigits = raw[1] - '0';
        if (nDigits <= 0 || nDigits > 9 || raw.Length < 2 + nDigits) return raw;
        string lenStr = System.Text.Encoding.ASCII.GetString(raw, 2, nDigits);
        if (!int.TryParse(lenStr, out int dataLen)) return raw;
        int dataStart = 2 + nDigits;
        if (raw.Length < dataStart + dataLen) return raw[dataStart..];
        return raw[dataStart..(dataStart + dataLen)];
    }

    internal static string GetMeasUnit(string param) => param.ToUpperInvariant() switch
    {
        "VMAX" or "VMIN" or "VPP" or "VTOP" or "VBASE"
        or "VAMP" or "VAVG" or "VRMS"        => "V",
        "OVERSHOOT" or "PRESHOOT"
        or "PDUTY" or "NDUTY"
        or "PDUTYC" or "NDUTYC"
        or "PDUTY CYCLE" or "NDUTY CYCLE"    => "%",
        "FREQUENCY" or "FREQ"                 => "Hz",
        "PERIOD" or "PER"
        or "RISETIME" or "RISE"
        or "FALLTIME" or "FALL"
        or "PWIDTH" or "PWID"
        or "NWIDTH" or "NWID"
        or "DELAY"                            => "s",
        "PHASE"                               => "°",
        _                                     => "",
    };
}
