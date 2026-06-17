using System.Globalization;
using System.IO.Ports;
using System.Runtime.InteropServices;
using InstrumentControl.Core.Interfaces;

namespace InstrumentControl.Core.Services;

public class VisaConnectionProvider : IConnectionProvider
{
    private readonly IntPtr _session;
    private readonly IntPtr _rm;
    private bool _disposed;

    public string ResourceName { get; }
    public string ConnectionType => "VISA";
    public bool IsOpen { get; private set; }

    internal VisaConnectionProvider(string resourceName, IntPtr rm, IntPtr session)
    {
        ResourceName = resourceName;
        _rm = rm;
        _session = session;
        IsOpen = true;
    }

    public Task OpenAsync() { IsOpen = true; return Task.CompletedTask; }

    public Task CloseAsync()
    {
        if (IsOpen) NiVisa.viClose(_session);
        IsOpen = false;
        return Task.CompletedTask;
    }

    public async Task WriteAsync(string command)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(command + "\n");
        uint retCount = 0;
        int status = NiVisa.viWrite(_session, bytes, (uint)bytes.Length, ref retCount);
        if (status < 0)
            throw new InvalidOperationException(
                $"VISA write error: 0x{status:X8} na zasobie '{ResourceName}' (komenda: {command.TrimEnd()}).");
        await Task.CompletedTask;
    }

    public async Task<string> QueryAsync(string command, int timeoutMs = 5000)
    {
        await WriteAsync(command);
        return await ReadAsync(timeoutMs);
    }

    public Task<string> ReadAsync(int timeoutMs = 5000)
    {
        NiVisa.viSetAttribute(_session, NiVisa.VI_ATTR_TMO_VALUE, (uint)timeoutMs);
        var buf = new byte[4096];
        uint retCount = 0;
        int status = NiVisa.viRead(_session, buf, (uint)buf.Length, ref retCount);
        if (status < 0)
        {
            // VI_ERROR_TMO = 0xBFFF0015 — instrument did not respond within timeout
            if (status == unchecked((int)0xBFFF0015u))
                throw new TimeoutException(
                    $"Instrument nie odpowiada (timeout {timeoutMs} ms). " +
                    $"Sprawdź: kabel, zasilanie, adres VISA, ustawienia interfejsu.");
            throw new InvalidOperationException(
                $"VISA read error: 0x{status:X8} na zasobie '{ResourceName}'");
        }
        return Task.FromResult(System.Text.Encoding.ASCII.GetString(buf, 0, (int)retCount).TrimEnd('\n', '\r'));
    }

    public Task WriteRawAsync(byte[] data)
    {
        uint ret = 0;
        NiVisa.viWrite(_session, data, (uint)data.Length, ref ret);
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadRawAsync(int count)
    {
        var buf = new byte[count];
        uint ret = 0;
        NiVisa.viRead(_session, buf, (uint)count, ref ret);
        return Task.FromResult(buf[..(int)ret]);
    }

    public void Dispose()
    {
        if (!_disposed) { CloseAsync().Wait(); _disposed = true; }
    }
}

public class SimulatedConnectionProvider : IConnectionProvider
{
    private static readonly Random _rng = new();
    private string _lastCommand = string.Empty;
    private readonly Dictionary<string, string> _customResponses = new(StringComparer.OrdinalIgnoreCase);

    // DMM / DAQ state
    private string _confFunc = "VOLT:DC";
    private int _fetchCount = 1;

    // Power supply state
    private double _voltSet = 5.0;
    private double _currLim = 1.0;
    private bool _outpOn = false;
    private double _ovpLevel = 66.0;
    private double _ocpLevel = 5.5;
    private bool _ovpOn = false;
    private bool _ocpOn = false;

    // Environmental chamber state
    private double _tempSetpoint = 25.0;
    private double _tempActual = 23.5;
    private bool _chamberRunning = false;
    private double _rampUp = 3.0;    // K/min
    private double _rampDown = 3.0;  // K/min
    private DateTime _lastTempTick = DateTime.UtcNow;

    // Oscilloscope state
    private double _timScale = 1e-3;
    private readonly double[] _chanScale = { 1.0, 1.0, 1.0, 1.0 };

    public string ResourceName { get; }
    public string ConnectionType => "SIMULATION";
    public bool IsOpen { get; private set; }

    public SimulatedConnectionProvider(string resourceName)
    {
        ResourceName = resourceName;
        IsOpen = false;
    }

    public Task OpenAsync()  { IsOpen = true;  return Task.CompletedTask; }
    public Task CloseAsync() { IsOpen = false; return Task.CompletedTask; }

    public Task WriteAsync(string command)
    {
        _lastCommand = command.Trim();
        ProcessWrite(_lastCommand);
        return Task.CompletedTask;
    }

    public async Task<string> QueryAsync(string command, int timeoutMs = 5000)
    {
        await WriteAsync(command);
        return await ReadAsync(timeoutMs);
    }

    public Task<string> ReadAsync(int timeoutMs = 5000)
    {
        if (_customResponses.TryGetValue(_lastCommand, out var r)) return Task.FromResult(r);
        return Task.FromResult(BuildResponse(_lastCommand));
    }

    // ── Write parsing: track instrument state ────────────────────────────────

    private void ProcessWrite(string cmd)
    {
        var up = cmd.ToUpperInvariant();

        // CONF: — configure DMM/DAQ function, strip channel list and range args
        if (up.StartsWith("CONF:"))
        {
            var after = up[5..];
            int sep = after.IndexOfAny(new[] { ' ', ',', '(' });
            _confFunc = (sep >= 0 ? after[..sep] : after).Trim();
            return;
        }

        // SAMP:COUN N — burst count for DMMs
        if (up.StartsWith("SAMP:COUN ") &&
            int.TryParse(up[10..].Trim(), out int sc)) { _fetchCount = sc; return; }

        // ROUT:SCAN (@...) — count scanned channels for Agilent 34970A
        if (up.StartsWith("ROUT:SCAN "))
        {
            int n = CountScpiChannels(cmd);
            if (n > 0) _fetchCount = n;
            return;
        }

        // VOLT X — power supply voltage setpoint (not VOLT: prefixed commands)
        if (up.StartsWith("VOLT ") && !up.Contains(':'))
        { if (TryPD(up[5..], out double v)) _voltSet = v; return; }

        // CURR X — current limit
        if (up.StartsWith("CURR ") && !up.Contains(':'))
        { if (TryPD(up[5..], out double v)) _currLim = v; return; }

        // OUTP ON/OFF
        if (up.StartsWith("OUTP ") || up is "OUTP ON" or "OUTP OFF")
        { _outpOn = (up.Length > 5 ? up[5..].Trim() : "") is "ON" or "1"; return; }

        // OVP / OCP
        if (up.StartsWith("VOLT:PROT ") && !up.StartsWith("VOLT:PROT:"))
        { if (TryPD(up[10..], out double v)) _ovpLevel = v; return; }
        if (up.StartsWith("VOLT:PROT:STAT "))
        { _ovpOn = up[15..].Trim() is "ON" or "1"; return; }
        if (up.StartsWith("CURR:PROT:LEV "))
        { if (TryPD(up[14..], out double v)) _ocpLevel = v; return; }
        if (up.StartsWith("CURR:PROT:STAT "))
        { _ocpOn = up[15..].Trim() is "ON" or "1"; return; }

        // CTS chamber ASCII protocol (lowercase commands from driver)
        if (up.StartsWith("A0 ") && cmd.Length > 3)      // "a0 XX.X" → set temperature
        { if (TryPD(cmd[3..].Trim(), out double v)) { _tempSetpoint = v; _lastTempTick = DateTime.UtcNow; } return; }
        if (up.StartsWith("U0 ") && cmd.Length > 3)      // "u0 X" → ramp up K/min
        { if (TryPD(cmd[3..].Trim(), out double v)) _rampUp = v; return; }
        if (up.StartsWith("D0 ") && cmd.Length > 3)      // "d0 X" → ramp down K/min
        { if (TryPD(cmd[3..].Trim(), out double v)) _rampDown = v; return; }
        if (up.StartsWith("S1 ") && cmd.Length > 3)      // "s1 1"/"s1 0" → start/stop
        { _chamberRunning = cmd[3..].Trim() == "1"; return; }

        // Oscilloscope timebase / channel scale
        if (up.StartsWith("TIM:SCAL "))
        { if (TryPD(up[9..], out double v)) _timScale = v; return; }

        var m = System.Text.RegularExpressions.Regex.Match(up, @"CHAN(\d):SCAL\s+([\S]+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out int ch) && ch is >= 1 and <= 4)
            TryPD(m.Groups[2].Value, out _chanScale[ch - 1]);
    }

    // ── Response builder ─────────────────────────────────────────────────────

    private string BuildResponse(string original)
    {
        var up = original.ToUpperInvariant().Trim();

        // Universal
        if (up == "*IDN?") return "SIMULATED,INSTRUMENT,SIM001,1.0";

        // CTS chamber ASCII protocol
        if (up == "C") return "C 2.10;1.20;001;";
        if (up == "A0")
        {
            TickChamber();
            return $"A0 {_tempActual:F2} {_tempSetpoint:F2}";
        }
        if (up == "O")  return $"O{(_chamberRunning ? "1" : "0")}01";
        if (up == "R0") return $"R0 {(_chamberRunning ? "11" : "00")} {_rampUp:F2} {_rampDown:F2} {_tempSetpoint:F2}";

        // Power supply queries
        if (up == "VOLT?")           return S(_voltSet);
        if (up == "CURR?")           return S(_currLim);
        if (up == "OUTP?")           return _outpOn ? "1" : "0";
        if (up == "VOLT:PROT?")      return S(_ovpLevel);
        if (up == "VOLT:PROT:STAT?") return _ovpOn ? "1" : "0";
        if (up == "CURR:PROT:LEV?")  return S(_ocpLevel);
        if (up == "CURR:PROT:STAT?") return _ocpOn ? "1" : "0";
        if (up == "STAT:OPER:COND?") return "1"; // CV mode
        if (up == "MEAS:VOLT?")
            return S(_outpOn ? _voltSet + N(0.002) : 0.0);
        if (up == "MEAS:CURR?")
            return S(_outpOn ? Math.Max(0.0, _currLim * 0.08 + N(0.001)) : 0.0);
        if (up == "MEAS:POW?")
        {
            double v = _outpOn ? _voltSet + N(0.002) : 0.0;
            double i = _outpOn ? Math.Max(0.0, _currLim * 0.08 + N(0.001)) : 0.0;
            return S(v * i);
        }

        // Oscilloscope parameter queries
        if (up == "TIM:SCAL?") return S(_timScale);
        var scalM = System.Text.RegularExpressions.Regex.Match(up, @"CHAN(\d):SCAL\?");
        if (scalM.Success && int.TryParse(scalM.Groups[1].Value, out int c) && c is >= 1 and <= 4)
            return S(_chanScale[c - 1]);
        if (up.EndsWith(":STAT?")) return "1";
        if (up.EndsWith(":COUP?")) return "DC";
        if (up.EndsWith(":PROB?")) return S(1.0);
        if (up == "TRIG:A:SOUR?")  return "CH1";

        // Oscilloscope measurement results: MEAS1:RES:ACT? or MEAS1:MAIN?
        if (System.Text.RegularExpressions.Regex.IsMatch(up, @"MEAS\d?:RES:ACT\?") ||
            System.Text.RegularExpressions.Regex.IsMatch(up, @"MEAS\d?:MAIN\?"))
            return S(OsciMeas(up));

        // Waveform data header: CHANn:DATA:HEAD?
        if (up.Contains(":DATA:HEAD?"))
        {
            double half = _timScale * 5.0;
            return $"{S(-half)},{S(half)},1000,1";
        }

        // Waveform data: CHANn:DATA?
        if (up.Contains(":DATA?") && !up.Contains("HEAD"))
            return SineWaveform(1000, _timScale * 10.0);

        // DMM / DAQ: READ? or FETCH?
        if (up is "READ?" or "FETCH?")
        {
            int count = _fetchCount;
            _fetchCount = 1;
            return count > 1
                ? string.Join(",", Enumerable.Range(0, count).Select(_ => S(DmmValue())))
                : S(DmmValue());
        }

        // Commands that produce no response (write-only)
        if (up.StartsWith("CONF")  || up.StartsWith("SENS")  || up.StartsWith("FORM") ||
            up.StartsWith("ROUT")  || up.StartsWith("TRIG")  || up.StartsWith("INIT") ||
            up.StartsWith("SOUR")  || up.StartsWith("SAMP")  || up.StartsWith("ZERO") ||
            up.StartsWith("DISP")  || up.StartsWith("CALC")  || up.StartsWith("HCOP") ||
            up is "*RST" or "*CLS" or "RUN" or "STOP" or "SING" or "AUT" or "OUTP:PROT:CLE")
            return "";

        return "0";
    }

    // ── Simulation value generators ──────────────────────────────────────────

    private double DmmValue() => _confFunc.ToUpperInvariant() switch
    {
        "VOLT" or "VOLT:DC"  => 3.300 + N(0.001),
        "VOLT:AC"            => 1.500 + N(0.005),
        "CURR" or "CURR:DC"  => 0.125 + N(0.0002),
        "CURR:AC"            => 0.080 + N(0.0002),
        "RES"  or "FRES"     => 1000.0 + N(0.5),
        "FREQ"               => 50.00  + N(0.01),
        "PER"                => 0.0200 + N(1e-6),
        "DIOD"               => 0.652  + N(0.001),
        "CONT"               => 5.20   + N(0.10),
        "TEMP"               => 25.0   + N(0.10),
        _                    => N(0.1)
    };

    private static double OsciMeas(string up) => up switch
    {
        var c when c.Contains("FREQ") => 1000.0 + N2(0.5),
        var c when c.Contains("PERI") => 1e-3   + N2(1e-7),
        var c when c.Contains("AMPL") => 2.00   + N2(0.01),
        var c when c.Contains("PK2P") => 2.00   + N2(0.01),
        var c when c.Contains("RMS")  => 0.707  + N2(0.003),
        var c when c.Contains("MEAN") => 0.0    + N2(0.002),
        var c when c.Contains("VAVG") => 0.0    + N2(0.002),
        var c when c.Contains("RISE") => 1e-8   + N2(5e-10),
        var c when c.Contains("FALL") => 1e-8   + N2(5e-10),
        var c when c.Contains("DCYC") => 50.0   + N2(0.05),
        var c when c.Contains("PHAS") => 0.0    + N2(0.1),
        _                             => 0.0    + N2(0.01)
    };

    private static string SineWaveform(int n, double tSpan)
    {
        var sb = new System.Text.StringBuilder(n * 12);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(',');
            double t = tSpan * i / (n - 1);
            sb.Append((Math.Sin(2.0 * Math.PI * 1000.0 * t) + N2(0.005))
                .ToString("F6", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private void TickChamber()
    {
        double elapsed = (DateTime.UtcNow - _lastTempTick).TotalSeconds;
        _lastTempTick = DateTime.UtcNow;
        if (!_chamberRunning) { _tempActual += N(0.05); return; }
        double diff = _tempSetpoint - _tempActual;
        if (Math.Abs(diff) < 0.05) { _tempActual = _tempSetpoint + N(0.02); return; }
        double rate = (diff > 0 ? _rampUp : _rampDown) / 60.0;
        double step = Math.Sign(diff) * rate * elapsed;
        _tempActual += Math.Abs(step) >= Math.Abs(diff) ? diff : step;
        _tempActual += N(0.02);
    }

    private static int CountScpiChannels(string cmd)
    {
        var m = System.Text.RegularExpressions.Regex.Match(cmd, @"@([0-9,: ]+)\)");
        if (!m.Success) return 0;
        int count = 0;
        foreach (var part in m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            if (t.Contains(':'))
            {
                var b = t.Split(':');
                if (b.Length == 2 && int.TryParse(b[0], out int lo) && int.TryParse(b[1], out int hi))
                    count += Math.Max(0, hi - lo + 1);
            }
            else count++;
        }
        return count;
    }

    // Gaussian noise — Box-Muller transform
    private double N(double sigma)
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    // Static variant used in static methods (OsciMeas, SineWaveform)
    private static double N2(double sigma)
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    // Format as SCPI-like number with sign prefix
    private static string S(double v) =>
        (v >= 0.0 ? "+" : "") + v.ToString("G9", CultureInfo.InvariantCulture);

    private static bool TryPD(string s, out double v) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    public void SetResponse(string command, string response) => _customResponses[command] = response;

    public Task WriteRawAsync(byte[] data) => Task.CompletedTask;
    public Task<byte[]> ReadRawAsync(int count) => Task.FromResult(new byte[count]);
    public void Dispose() { }
}

public class SerialConnectionProvider : IConnectionProvider
{
    private readonly SerialPort _port;

    public string ResourceName { get; }
    public string ConnectionType => "COM";
    public bool IsOpen => _port.IsOpen;

    public SerialConnectionProvider(string portName, int baudRate = 9600, int dataBits = 8,
        Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        ResourceName = portName;
        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            ReadTimeout = 5000, WriteTimeout = 5000, NewLine = "\n"
        };
    }

    public Task OpenAsync() { _port.Open(); return Task.CompletedTask; }
    public Task CloseAsync() { if (_port.IsOpen) _port.Close(); return Task.CompletedTask; }

    public Task WriteAsync(string command)
    {
        _port.WriteLine(command);
        return Task.CompletedTask;
    }

    public async Task<string> QueryAsync(string command, int timeoutMs = 5000)
    {
        _port.ReadTimeout = timeoutMs;
        await WriteAsync(command);
        return await ReadAsync(timeoutMs);
    }

    public Task<string> ReadAsync(int timeoutMs = 5000)
    {
        _port.ReadTimeout = timeoutMs;
        return Task.FromResult(_port.ReadLine().TrimEnd('\r', '\n'));
    }

    public Task WriteRawAsync(byte[] data) { _port.Write(data, 0, data.Length); return Task.CompletedTask; }
    public Task<byte[]> ReadRawAsync(int count)
    {
        var buf = new byte[count];
        _port.Read(buf, 0, count);
        return Task.FromResult(buf);
    }

    public void Dispose() { if (_port.IsOpen) _port.Close(); _port.Dispose(); }
}

public class VisaService
{
    private IntPtr _rm = IntPtr.Zero;
    public bool IsSimulationMode { get; private set; }

    public void Initialize()
    {
        try
        {
            int status = NiVisa.viOpenDefaultRM(out _rm);
            IsSimulationMode = (status < 0);
        }
        catch
        {
            IsSimulationMode = true;
        }
    }

    public string[] FindResources(string pattern = "?*::INSTR")
    {
        if (IsSimulationMode)
            return new[]
            {
                "SIM::GPIB0::22::INSTR (HP34401A)",
                "SIM::GPIB0::10::INSTR (Keithley2000)",
                "SIM::GPIB0::09::INSTR (Agilent34970A)",
                "SIM::GPIB0::11::INSTR (ItechIT6922B)",
                "SIM::TCPIP0::192.168.0.100::INSTR (RTB2004)",
                "SIM::TCPIP0::192.168.0.101::INSTR (RigolDS1000Z)",
                "SIM::COM3 (CTSChamber)",
            };

        var allResults = new List<string>();

        // Try multiple sub-patterns to ensure all protocols are discovered
        var patterns = new[] { "USB?*::INSTR", "GPIB?*::INSTR", "TCPIP?*::INSTR", "ASRL?*::INSTR" };

        foreach (var p in patterns)
        {
            try
            {
                var descBuf = new byte[256];
                int status = NiVisa.viFindRsrc(_rm, p, out var list, out uint count, descBuf);
                if (status < 0 || count == 0) continue;

                // viFindRsrc fills descBuf with the FIRST resource; viFindNext returns the rest
                allResults.Add(System.Text.Encoding.ASCII.GetString(descBuf).TrimEnd('\0', ' '));

                var buf = new byte[256];
                for (uint i = 1; i < count; i++)
                {
                    NiVisa.viFindNext(list, buf);
                    allResults.Add(System.Text.Encoding.ASCII.GetString(buf).TrimEnd('\0', ' '));
                }
                NiVisa.viClose(list);
            }
            catch { /* pattern not supported by this VISA implementation — skip */ }
        }

        // Deduplicate and remove empty entries
        return allResults
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string[] GetComPorts() => SerialPort.GetPortNames();

    public IConnectionProvider OpenVisaSession(string resourceName, int timeoutMs = 5000)
    {
        if (IsSimulationMode || resourceName.StartsWith("SIM::"))
            return OpenSimulated(resourceName);

        int status = NiVisa.viOpen(_rm, resourceName, 0, (uint)timeoutMs, out var session);
        if (status < 0) throw new InvalidOperationException($"Cannot open VISA resource '{resourceName}': 0x{status:X8}");
        NiVisa.viSetAttribute(session, NiVisa.VI_ATTR_TMO_VALUE, (uint)timeoutMs);
        return new VisaConnectionProvider(resourceName, _rm, session);
    }

    public IConnectionProvider OpenComSession(string portName, int baudRate = 9600) =>
        new SerialConnectionProvider(portName, baudRate);

    public IConnectionProvider OpenSimulated(string resourceName) =>
        new SimulatedConnectionProvider(resourceName);

    public void Dispose()
    {
        if (_rm != IntPtr.Zero) { NiVisa.viClose(_rm); _rm = IntPtr.Zero; }
    }
}

internal static class NiVisa
{
    private const string DllName = "visa64.dll";

    public const uint VI_ATTR_TMO_VALUE = 0x3FFF001AU;

    [DllImport(DllName, EntryPoint = "viOpenDefaultRM")]
    public static extern int viOpenDefaultRM(out IntPtr sesn);

    [DllImport(DllName, EntryPoint = "viOpen")]
    public static extern int viOpen(IntPtr sesn, [MarshalAs(UnmanagedType.LPStr)] string rsrcName,
        uint accessMode, uint openTimeout, out IntPtr vi);

    [DllImport(DllName, EntryPoint = "viClose")]
    public static extern int viClose(IntPtr vi);

    [DllImport(DllName, EntryPoint = "viWrite")]
    public static extern int viWrite(IntPtr vi, byte[] buf, uint count, ref uint retCount);

    [DllImport(DllName, EntryPoint = "viRead")]
    public static extern int viRead(IntPtr vi, byte[] buf, uint count, ref uint retCount);

    [DllImport(DllName, EntryPoint = "viFindRsrc")]
    public static extern int viFindRsrc(IntPtr sesn, [MarshalAs(UnmanagedType.LPStr)] string expr,
        out IntPtr findList, out uint retcnt, [MarshalAs(UnmanagedType.LPArray, SizeConst = 256)] byte[] desc);

    [DllImport(DllName, EntryPoint = "viFindNext")]
    public static extern int viFindNext(IntPtr findList, [MarshalAs(UnmanagedType.LPArray, SizeConst = 256)] byte[] desc);

    [DllImport(DllName, EntryPoint = "viSetAttribute")]
    public static extern int viSetAttribute(IntPtr vi, uint attrName, uint attrValue);
}
