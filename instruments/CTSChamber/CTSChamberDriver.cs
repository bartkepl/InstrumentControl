using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using CTSChamber.Views;

namespace CTSChamber;

[InstrumentDriver]
public class CTSChamberDriver : InstrumentDriverBase
{
    public override string DriverName   => "CTSChamber";
    public override string Manufacturer => "CTS";
    public override string Model        => "T-40/50";
    public override string Description  => "Komora środowiskowa CTS — protokół ASCII RS-232 (19200/O/8/1)";
    public override string[] SupportedResourcePatterns => new[] { "COM?*" };

    static CTSChamberDriver()
    {
        CTSChamberBlocks.RegisterAll();
    }

    // ── Connection override ────────────────────────────────────────────────────

    public override async Task ConnectAsync(IConnectionProvider connection)
    {
        // The app passes a generic provider; swap for CTS-specific binary-protocol one
        string portName = connection.ResourceName;
        connection.Dispose();

        var ctsConn = new CTSSerialConnectionProvider(portName);
        Connection = ctsConn;
        await ctsConn.OpenAsync();

        string firmware;
        try { firmware = await GetIdentificationAsync(); }
        catch { firmware = "nieznana"; }

        InstrumentInfo = new InstrumentInfo
        {
            ResourceName    = portName,
            DriverName      = DriverName,
            Manufacturer    = Manufacturer,
            Model           = Model,
            ConnectionType  = ctsConn.ConnectionType,
            FirmwareVersion = firmware,
            Status          = ConnectionStatus.Connected,
            ConnectedAt     = DateTime.Now,
        };
        RaiseStatus($"Połączono z komorą CTS na {portName}");
    }

    // ── Identification ─────────────────────────────────────────────────────────

    public override async Task<string> GetIdentificationAsync()
    {
        // Command 'C' returns PLCVersion;ITCVersion;PLCNumber;
        string r = await Query("C");
        return r.TrimStart('C').Trim(';', ' ');
    }

    public override async Task ResetAsync()
    {
        await ChamberStopAsync();
        RaiseStatus("Reset: komora zatrzymana");
    }

    // ── Temperature ────────────────────────────────────────────────────────────

    /// <summary>Returns (Actual °C, Setpoint °C).</summary>
    public async Task<(double Actual, double Setpoint)> ReadTemperatureAsync()
    {
        string r = await Query("A0");
        var nums = Regex.Matches(r, @"[+-]?\d+\.\d+");
        if (nums.Count < 2)
            throw new InvalidOperationException($"CTS: nieprawidłowa odpowiedź A0: '{r}'");
        double actual   = double.Parse(nums[0].Value, CultureInfo.InvariantCulture);
        double setpoint = double.Parse(nums[1].Value, CultureInfo.InvariantCulture);
        RaiseMeasurement(new MeasurementResult
        {
            Function      = "TEMP",
            Unit          = "°C",
            Value         = actual,
            InstrumentName = DriverName,
            ChannelId     = "CH0",
            ParameterName = "temp_actual",
        });
        return (actual, setpoint);
    }

    /// <summary>Sets temperature setpoint (−75 … 185 °C).</summary>
    public async Task SetTemperatureAsync(double temperature)
    {
        if (temperature < -75.0 || temperature > 185.0)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperatura poza zakresem (−75 … 185 °C)");
        await Write($"a0 {temperature.ToString("F1", CultureInfo.InvariantCulture)}");
        RaiseStatus($"Temperatura zadana: {temperature:F1} °C");
    }

    // ── Ramp gradients ─────────────────────────────────────────────────────────

    /// <summary>Sets ramp-up gradient (K/min).</summary>
    public async Task SetRampUpAsync(double kPerMin)
    {
        if (kPerMin < 0.01)
            throw new ArgumentOutOfRangeException(nameof(kPerMin), "Gradient wzrostu musi być ≥ 0.01 K/min");
        await Write($"u0 {kPerMin.ToString("F1", CultureInfo.InvariantCulture)}");
        RaiseStatus($"Gradient wzrostu: {kPerMin:F1} K/min");
    }

    /// <summary>Sets ramp-down gradient (K/min).</summary>
    public async Task SetRampDownAsync(double kPerMin)
    {
        if (kPerMin < 0.01)
            throw new ArgumentOutOfRangeException(nameof(kPerMin), "Gradient spadku musi być ≥ 0.01 K/min");
        await Write($"d0 {kPerMin.ToString("F1", CultureInfo.InvariantCulture)}");
        RaiseStatus($"Gradient spadku: {kPerMin:F1} K/min");
    }

    /// <summary>Reads ramp parameters: (Active, Running, RampUp K/min, RampDown K/min, FinalValue °C).</summary>
    public async Task<(bool Active, bool Running, double RampUp, double RampDown, double FinalValue)> ReadRampParamsAsync()
    {
        string r = await Query("R0");
        // Response: R0 ab xxxx.xx yyyy.yy zzzz.zz
        var parts = r.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string ab    = parts.Length > 1 ? parts[1] : "00";
        bool active  = ab.Length > 0 && ab[0] == '1';
        bool running = ab.Length > 1 && ab[1] == '1';
        double rampUp   = ParseDoubleAt(parts, 2);
        double rampDown = ParseDoubleAt(parts, 3);
        double finalVal = ParseDoubleAt(parts, 4);
        return (active, running, rampUp, rampDown, finalVal);
    }

    // ── Chamber start / stop ───────────────────────────────────────────────────

    public async Task ChamberStartAsync()  { await Write("s1 1"); RaiseStatus("Komora: URUCHOMIONA"); }
    public async Task ChamberStopAsync()   { await Write("s1 0"); RaiseStatus("Komora: ZATRZYMANA"); }
    public async Task ChamberPauseAsync()  { await Write("s3 0"); RaiseStatus("Komora: PAUZA"); }
    public async Task ChamberResumeAsync() { await Write("s3 1"); RaiseStatus("Komora: WZNOWIONA"); }

    // ── Chamber state ──────────────────────────────────────────────────────────

    /// <summary>Returns (Running, Error, Paused) from status command 'O'.</summary>
    public async Task<(bool Running, bool Error, bool Paused)> ReadChamberStateAsync()
    {
        string r = await Query("O");
        // Response: Oxyz  x=running(0/1), y=error(0/1), z=0→interrupted/paused, z=1→running
        bool running = r.Length > 1 && r[1] == '1';
        bool error   = r.Length > 2 && r[2] == '1';
        bool paused  = r.Length > 3 && r[3] == '0';
        return (running, error, paused);
    }

    // ── Plugin interface ───────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() => new CTSChamberFrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() =>
    [
        new CTS_SetTemperature(),
        new CTS_SetRamp(),
        new CTS_ChamberStart(),
        new CTS_ChamberStop(),
        new CTS_ChamberPause(),
        new CTS_ReadTemperature(),
        new CTS_WaitForTemperature(),
    ];

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static double ParseDoubleAt(string[] parts, int idx) =>
        parts.Length > idx && double.TryParse(parts[idx], NumberStyles.Float,
            CultureInfo.InvariantCulture, out double v) ? v : 0.0;
}
