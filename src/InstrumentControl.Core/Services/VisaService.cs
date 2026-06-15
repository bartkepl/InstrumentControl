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
    private readonly Random _rng = new();
    private string _lastCommand = string.Empty;
    private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);

    public string ResourceName { get; }
    public string ConnectionType => "SIMULATION";
    public bool IsOpen { get; private set; }

    public SimulatedConnectionProvider(string resourceName)
    {
        ResourceName = resourceName;
        IsOpen = false;
    }

    public Task OpenAsync() { IsOpen = true; return Task.CompletedTask; }
    public Task CloseAsync() { IsOpen = false; return Task.CompletedTask; }

    public Task WriteAsync(string command)
    {
        _lastCommand = command.Trim();
        return Task.CompletedTask;
    }

    public async Task<string> QueryAsync(string command, int timeoutMs = 5000)
    {
        await WriteAsync(command);
        return await ReadAsync(timeoutMs);
    }

    public Task<string> ReadAsync(int timeoutMs = 5000)
    {
        if (_responses.TryGetValue(_lastCommand, out var resp)) return Task.FromResult(resp);

        var cmd = _lastCommand.ToUpperInvariant();
        if (cmd == "*IDN?") return Task.FromResult("SIMULATED,INSTRUMENT,SIM001,1.0");
        if (cmd.StartsWith("MEAS") || cmd.StartsWith("READ") || cmd == "FETCH?")
            return Task.FromResult($"+{(_rng.NextDouble() * 10):F6}");
        if (cmd.StartsWith("CONF")) return Task.FromResult("");
        return Task.FromResult("0");
    }

    public void SetResponse(string command, string response) => _responses[command] = response;

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
            return new[] { "SIM::GPIB0::22::INSTR (HP34401A)", "SIM::GPIB0::09::INSTR (Agilent34970A)" };

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
