using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using Keithley2000.Views;

namespace Keithley2000;

[InstrumentDriver]
public class Keithley2000Driver : InstrumentDriverBase
{
    public override string DriverName => "Keithley2000";
    public override string Manufacturer => "Keithley";
    public override string Model => "2000";
    public override string Description => "6½-cyfrowy multimetr cyfrowy z pomiarem temperatury TC";
    public override string[] SupportedResourcePatterns => new[]
    {
        "GPIB?*::?*::INSTR",
        "USB?*::?*::INSTR",
        "ASRL?*::?*::INSTR"
    };

    static Keithley2000Driver()
    {
        Keithley2000Blocks.RegisterAll();
    }

    // ── Configuration cache ────────────────────────────────────────────────

    private (string confCmd, string? nplcCmd)? _lastConfig;

    private async Task EnsureConfigured(string confCmd, string? nplcCmd)
    {
        var desired = (confCmd, nplcCmd);
        if (_lastConfig == desired) return;
        await Write(confCmd);
        if (nplcCmd != null)
            await Write(nplcCmd);
        _lastConfig = desired;
    }

    public void InvalidateConfigCache() => _lastConfig = null;

    public override async Task ConnectAsync(IConnectionProvider connection)
    {
        InvalidateConfigCache();
        await base.ConnectAsync(connection);
    }

    public override async Task ResetAsync()
    {
        InvalidateConfigCache();
        await base.ResetAsync();
    }

    public override async Task ReconnectAsync()
    {
        InvalidateConfigCache();
        await base.ReconnectAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatRange(string range) =>
        range.Equals("AUTO", StringComparison.OrdinalIgnoreCase) ? "DEF" : range;

    private static string NplcStr(double nplc) =>
        nplc.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // ── Public measurement methods ───────────────────────────────────────────

    public async Task<double> MeasureDCV(string range = "AUTO", double nplc = 1.0)
    {
        string r = FormatRange(range);
        await EnsureConfigured($"CONF:VOLT:DC {r},DEF", $"SENS:VOLT:DC:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "DCV", Unit = "V", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "voltage"
        });
        return v;
    }

    public async Task<double> MeasureACV(string range = "AUTO", double nplc = 1.0)
    {
        string r = FormatRange(range);
        await EnsureConfigured($"CONF:VOLT:AC {r},DEF", $"SENS:VOLT:AC:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "ACV", Unit = "V AC", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "voltage_ac"
        });
        return v;
    }

    public async Task<double> MeasureDCI(string range = "AUTO", double nplc = 1.0)
    {
        string r = FormatRange(range);
        await EnsureConfigured($"CONF:CURR:DC {r},DEF", $"SENS:CURR:DC:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "DCI", Unit = "A", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "current"
        });
        return v;
    }

    public async Task<double> MeasureACI(string range = "AUTO", double nplc = 1.0)
    {
        string r = FormatRange(range);
        await EnsureConfigured($"CONF:CURR:AC {r},DEF", $"SENS:CURR:AC:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "ACI", Unit = "A AC", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "current_ac"
        });
        return v;
    }

    public async Task<double> MeasureResistance2W(string range = "AUTO", double nplc = 1.0)
    {
        string r = FormatRange(range);
        await EnsureConfigured($"CONF:RES {r},DEF", $"SENS:RES:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "RES2W", Unit = "Ω", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "resistance"
        });
        return v;
    }

    public async Task<double> MeasureResistance4W(string range = "AUTO", double nplc = 1.0)
    {
        string r = FormatRange(range);
        await EnsureConfigured($"CONF:FRES {r},DEF", $"SENS:FRES:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "RES4W", Unit = "Ω", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "resistance_4w"
        });
        return v;
    }

    public async Task<double> MeasureFrequency(string range = "AUTO", double nplc = 1.0)
    {
        await EnsureConfigured("CONF:FREQ", $"SENS:FREQ:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "FREQ", Unit = "Hz", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "frequency"
        });
        return v;
    }

    public async Task<double> MeasurePeriod(string range = "AUTO", double nplc = 1.0)
    {
        await EnsureConfigured("CONF:PER", $"SENS:PER:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "PERIOD", Unit = "s", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "period"
        });
        return v;
    }

    public async Task<double> MeasureDiode()
    {
        await EnsureConfigured("CONF:DIOD", null);
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "DIODE", Unit = "V", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "diode"
        });
        return v;
    }

    public async Task<double> MeasureContinuity()
    {
        await EnsureConfigured("CONF:CONT", null);
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "CONT", Unit = "Ω", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "continuity"
        });
        return v;
    }

    public async Task<double> MeasureTemperature(string tcType = "K", double nplc = 1.0)
    {
        await EnsureConfigured($"CONF:TEMP TC,{tcType}", $"SENS:TEMP:NPLC {NplcStr(nplc)}");
        double v = await QueryDouble("READ?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "TEMP", Unit = "°C", Value = v,
            InstrumentName = DriverName, ChannelId = "CH1", ParameterName = "temperature"
        });
        return v;
    }

    // ── MATH functions ───────────────────────────────────────────────────────

    public async Task SetMathMode(string mode)
    {
        // mode: "NULL" (REL), "MXB", "PCT", "DBM", "OFF"
        if (mode == "OFF")
        {
            await Write("CALC2:STAT OFF");
        }
        else
        {
            await Write($"CALC2:FORM {mode}");
            await Write("CALC2:STAT ON");
        }
    }

    // ── Auto-zero ────────────────────────────────────────────────────────────

    public async Task SetAutoZero(bool on)
    {
        await Write(on ? "ZERO:AUTO ON" : "ZERO:AUTO OFF");
    }

    // ── Burst measurement ────────────────────────────────────────────────────

    public async Task<double[]> BurstMeasureAsync(int count)
    {
        await Write($"SAMP:COUN {count}");
        await Write("INIT");
        await Task.Delay(Math.Max(300, count * 60));
        string response = await Query("FETCH?");
        await Write("SAMP:COUN 1");
        var results = new List<double>();
        foreach (var p in response.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(p.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
                results.Add(d);
        }
        return results.ToArray();
    }

    // ── LIMIT test ───────────────────────────────────────────────────────────

    public async Task SetLimitTestAsync(double low, double high)
    {
        await Write($"CALC2:LIM:LOW {low.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        await Write($"CALC2:LIM:UPP {high.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        await Write("CALC2:LIM:STAT ON");
    }

    public async Task DisableLimitTestAsync()
    {
        await Write("CALC2:LIM:STAT OFF");
    }

    public async Task<bool> GetLimitFailAsync()
    {
        string r = await Query("CALC2:LIM:FAIL?");
        return r.Trim() == "1";
    }

    // ── Display helpers ──────────────────────────────────────────────────────

    public async Task DisplayText(string message)
    {
        await Write($"DISP:TEXT \"{message}\"");
    }

    public async Task ClearDisplay()
    {
        await Write("DISP:TEXT:CLE");
    }

    public async Task SetDisplayEnabled(bool on)
    {
        await Write(on ? "DISP:ENAB ON" : "DISP:ENAB OFF");
    }

    // ── Plugin interface ─────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() =>
        new Keithley2000FrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => new ISequenceBlock[]
    {
        new K2000_MeasureDCV(),
        new K2000_MeasureACV(),
        new K2000_MeasureResistance(),
        new K2000_MeasureCurrent(),
        new K2000_MeasureFrequency(),
        new K2000_MeasurePeriod(),
        new K2000_MeasureDiode(),
        new K2000_MeasureContinuity(),
        new K2000_MeasureTemperature(),
    };
}
