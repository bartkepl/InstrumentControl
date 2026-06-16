using System.IO.Ports;
using InstrumentControl.Core.Interfaces;

namespace CTSChamber;

public sealed class CTSSerialConnectionProvider : IConnectionProvider
{
    private readonly SerialPort _port;
    private readonly byte _address;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const byte STX = 0x02;
    private const byte ETX = 0x03;

    public string ResourceName { get; }
    public string ConnectionType => "COM-CTS";
    public bool IsOpen => _port.IsOpen;

    public CTSSerialConnectionProvider(string portName, byte address = 0x81)
    {
        ResourceName = portName;
        _address = address;
        _port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One)
        {
            ReadTimeout  = 3000,
            WriteTimeout = 3000,
        };
    }

    public Task OpenAsync()  { _port.Open();  return Task.CompletedTask; }
    public Task CloseAsync() { if (_port.IsOpen) _port.Close(); return Task.CompletedTask; }

    // ── CTS binary framing ─────────────────────────────────────────────────────

    private byte[] BuildFrame(string command)
    {
        // Frame: STX | ADR | (each char | 0x80) | CHK | ETX
        // CHK = XOR(ADR, all data bytes) | 0x80
        var data = new List<byte> { _address };
        foreach (char c in command)
            data.Add((byte)(c | 0x80));

        byte chk = 0;
        foreach (byte b in data) chk ^= b;
        chk |= 0x80;

        var frame = new byte[data.Count + 3];
        frame[0] = STX;
        data.CopyTo(frame, 1);
        frame[^2] = chk;
        frame[^1] = ETX;
        return frame;
    }

    private byte[] ReadFrame()
    {
        var buf = new List<byte>(64);

        // Sync to STX — discard any stale bytes
        int b;
        do { b = _port.ReadByte(); }
        while (b != STX && b >= 0);

        if (b < 0) throw new InvalidOperationException("CTS: koniec strumienia przed STX");
        buf.Add(STX);

        // Read until ETX; data bytes and CHK all have MSB set so 0x03 only appears as ETX
        while (true)
        {
            b = _port.ReadByte();
            if (b < 0) throw new InvalidOperationException("CTS: koniec strumienia w trakcie ramki");
            buf.Add((byte)b);
            if (b == ETX) break;
            if (buf.Count > 512) throw new InvalidOperationException("CTS: przepełnienie bufora ramki");
        }
        return [.. buf];
    }

    private static string DecodeFrame(byte[] frame)
    {
        // frame: STX(0) | ADR(1) | payload(2..^2) | CHK(^2) | ETX(^1)
        if (frame.Length < 4 || frame[0] != STX || frame[^1] != ETX)
            throw new InvalidOperationException($"CTS: nieprawidłowa ramka ({frame.Length} bajtów)");

        // Validate checksum
        byte chk = 0;
        for (int i = 1; i < frame.Length - 2; i++)
            chk ^= frame[i];
        chk |= 0x80;
        if (chk != frame[^2])
            throw new InvalidOperationException($"CTS: błąd sumy kontrolnej (oczekiwano 0x{chk:X2}, odczytano 0x{frame[^2]:X2})");

        // Decode payload — strip MSB from each byte to recover ASCII
        var payload = frame[2..^2];
        return string.Concat(payload.Select(b => (char)(b & 0x7F)));
    }

    // ── IConnectionProvider ────────────────────────────────────────────────────

    public async Task<string> QueryAsync(string command, int timeoutMs = 5000)
    {
        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                _port.ReadTimeout = timeoutMs;
                _port.DiscardInBuffer();
                var frame = BuildFrame(command);
                _port.Write(frame, 0, frame.Length);
                Thread.Sleep(200);
                return DecodeFrame(ReadFrame());
            });
        }
        finally { _lock.Release(); }
    }

    public async Task WriteAsync(string command)
    {
        await _lock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                _port.ReadTimeout = 2000;
                _port.DiscardInBuffer();
                var frame = BuildFrame(command);
                _port.Write(frame, 0, frame.Length);
                Thread.Sleep(200);
                try { ReadFrame(); } catch { }  // discard ACK, ignore errors
            });
        }
        finally { _lock.Release(); }
    }

    public Task<string> ReadAsync(int timeoutMs = 5000) => Task.FromResult(string.Empty);

    public Task WriteRawAsync(byte[] data)
    {
        _port.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadRawAsync(int count)
    {
        var buf = new byte[count];
        int n = _port.Read(buf, 0, count);
        return Task.FromResult(buf[..n]);
    }

    public void Dispose()
    {
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
        _lock.Dispose();
    }
}
