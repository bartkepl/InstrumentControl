using System.Globalization;
using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using ItechIT6922B.Views;

namespace ItechIT6922B;

[InstrumentDriver]
public class ItechIT6922BDriver : InstrumentDriverBase
{
    public override string DriverName => "ItechIT6922B";
    public override string Manufacturer => "ITECH";
    public override string Model => "IT6922B";
    public override string Description => "Programowalny zasilacz DC 60V/5A";
    public override string[] SupportedResourcePatterns => new[]
    {
        "GPIB?*::?*::INSTR",
        "USB?*::?*::INSTR",
        "ASRL?*::INSTR"
    };

    static ItechIT6922BDriver()
    {
        ItechIT6922BBlocks.RegisterAll();
    }

    // ── Voltage setpoint ─────────────────────────────────────────────────────

    public async Task SetVoltageAsync(double voltage)
    {
        await Write($"VOLT {voltage.ToString(CultureInfo.InvariantCulture)}");
        RaiseStatus($"Napięcie ustawione: {voltage:F3} V");
    }

    public async Task<double> GetVoltageSetpointAsync() =>
        await QueryDouble("VOLT?");

    // ── Current limit ────────────────────────────────────────────────────────

    public async Task SetCurrentLimitAsync(double current)
    {
        await Write($"CURR {current.ToString(CultureInfo.InvariantCulture)}");
        RaiseStatus($"Limit prądu: {current:F3} A");
    }

    public async Task<double> GetCurrentLimitAsync() =>
        await QueryDouble("CURR?");

    // ── Output enable ────────────────────────────────────────────────────────

    public async Task SetOutputEnabledAsync(bool on)
    {
        await Write(on ? "OUTP ON" : "OUTP OFF");
        RaiseStatus(on ? "Wyjście włączone" : "Wyjście wyłączone");
    }

    public async Task<bool> GetOutputEnabledAsync()
    {
        string r = await Query("OUTP?");
        return r.Trim() is "1" or "ON";
    }

    // ── Measurements ─────────────────────────────────────────────────────────

    public async Task<double> MeasureVoltageAsync()
    {
        double v = await QueryDouble("MEAS:VOLT?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "VOLT", Unit = "V", Value = v,
            InstrumentName = DriverName, ChannelId = "OUT1", ParameterName = "voltage"
        });
        return v;
    }

    public async Task<double> MeasureCurrentAsync()
    {
        double v = await QueryDouble("MEAS:CURR?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "CURR", Unit = "A", Value = v,
            InstrumentName = DriverName, ChannelId = "OUT1", ParameterName = "current"
        });
        return v;
    }

    public async Task<double> MeasurePowerAsync()
    {
        double v = await QueryDouble("MEAS:POW?");
        RaiseMeasurement(new MeasurementResult
        {
            Function = "POW", Unit = "W", Value = v,
            InstrumentName = DriverName, ChannelId = "OUT1", ParameterName = "power"
        });
        return v;
    }

    // convenience: measure all three in one call
    public async Task<(double V, double I, double P)> MeasureAllAsync()
    {
        double v = await MeasureVoltageAsync();
        double i = await MeasureCurrentAsync();
        double p = await MeasurePowerAsync();
        return (v, i, p);
    }

    // ── OVP ──────────────────────────────────────────────────────────────────

    public async Task SetOvpLevelAsync(double voltage) =>
        await Write($"VOLT:PROT {voltage.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetOvpLevelAsync() =>
        await QueryDouble("VOLT:PROT?");

    public async Task SetOvpEnabledAsync(bool on) =>
        await Write(on ? "VOLT:PROT:STAT ON" : "VOLT:PROT:STAT OFF");

    public async Task<bool> GetOvpEnabledAsync()
    {
        string r = await Query("VOLT:PROT:STAT?");
        return r.Trim() is "1" or "ON";
    }

    // ── OCP ──────────────────────────────────────────────────────────────────

    public async Task SetOcpLevelAsync(double current) =>
        await Write($"CURR:PROT:LEV {current.ToString(CultureInfo.InvariantCulture)}");

    public async Task<double> GetOcpLevelAsync() =>
        await QueryDouble("CURR:PROT:LEV?");

    public async Task SetOcpEnabledAsync(bool on) =>
        await Write(on ? "CURR:PROT:STAT ON" : "CURR:PROT:STAT OFF");

    public async Task<bool> GetOcpEnabledAsync()
    {
        string r = await Query("CURR:PROT:STAT?");
        return r.Trim() is "1" or "ON";
    }

    // ── Clear protection ─────────────────────────────────────────────────────

    public async Task ClearProtectionAsync()
    {
        await Write("OUTP:PROT:CLE");
        RaiseStatus("Ochrona wyczyszczona");
    }

    // ── Operating mode (CV or CC) ────────────────────────────────────────────

    public async Task<string> GetOperatingModeAsync()
    {
        string r = await Query("STAT:OPER:COND?");
        if (int.TryParse(r.Trim(), out int cond))
        {
            if ((cond & 0x02) != 0) return "CC";
            if ((cond & 0x01) != 0) return "CV";
        }
        return "UNREG";
    }

    // ── Read all setpoints at once ───────────────────────────────────────────

    public async Task<(double Voltage, double Current, bool OutputOn)> ReadSetpointsAsync()
    {
        double v   = await GetVoltageSetpointAsync();
        double i   = await GetCurrentLimitAsync();
        bool   on  = await GetOutputEnabledAsync();
        return (v, i, on);
    }

    // ── Plugin interface ─────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() =>
        new ItechIT6922BFrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => new ISequenceBlock[]
    {
        new IT6922B_SetVoltage(),
        new IT6922B_SetCurrent(),
        new IT6922B_SetOutput(),
        new IT6922B_MeasureVoltage(),
        new IT6922B_MeasureCurrent(),
        new IT6922B_MeasurePower(),
        new IT6922B_SetOVP(),
        new IT6922B_SetOCP(),
    };
}
