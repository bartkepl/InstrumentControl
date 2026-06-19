using InstrumentControl.Core.Interfaces;

namespace InstrumentControl.Instruments.Tests;

/// <summary>
/// Połączenie testowe rejestrujące wszystkie wysłane komendy i zwracające
/// zaprogramowane odpowiedzi na zapytania. Domyślnie odpowiada na *IDN?.
/// Pozwala weryfikować dokładne komendy SCPI generowane przez drivery.
/// </summary>
internal sealed class RecordingConnection : IConnectionProvider
{
    private readonly Queue<string> _responses = new();
    private readonly Dictionary<string, string> _canned = new(StringComparer.OrdinalIgnoreCase);
    private string _defaultResponse;
    private byte[]? _rawResponse;

    public RecordingConnection(string idn = "ACME,Model123,SN42,1.0", string defaultResponse = "+1.23456E+00")
    {
        _canned["*IDN?"] = idn;
        _defaultResponse = defaultResponse;
    }

    public List<string> Written { get; } = new();
    public List<string> Queried { get; } = new();
    public string ResourceName { get; init; } = "REC::INSTR";
    public string ConnectionType { get; init; } = "REC";
    public bool IsOpen { get; private set; }

    public RecordingConnection When(string command, string response)
    {
        _canned[command] = response;
        return this;
    }

    public RecordingConnection Enqueue(params string[] responses)
    {
        foreach (var r in responses) _responses.Enqueue(r);
        return this;
    }

    /// <summary>Zaprogramuj surową odpowiedź binarną zwracaną przez ReadRawAsync (np. zrzut ekranu).</summary>
    public RecordingConnection WithRaw(byte[] raw)
    {
        _rawResponse = raw;
        return this;
    }

    /// <summary>Czy wśród wysłanych komend jest dokładnie ta podana?</summary>
    public bool Sent(string command) => Written.Contains(command);

    public Task OpenAsync() { IsOpen = true; return Task.CompletedTask; }
    public Task CloseAsync() { IsOpen = false; return Task.CompletedTask; }

    public Task WriteAsync(string command) { Written.Add(command); return Task.CompletedTask; }

    public Task<string> QueryAsync(string command, int timeoutMs = 5000)
    {
        Written.Add(command);
        Queried.Add(command);
        if (_canned.TryGetValue(command, out var r)) return Task.FromResult(r);
        if (_responses.Count > 0) return Task.FromResult(_responses.Dequeue());
        return Task.FromResult(_defaultResponse);
    }

    public Task<string> ReadAsync(int timeoutMs = 5000) =>
        Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : _defaultResponse);

    public Task WriteRawAsync(byte[] data) { Written.Add($"<raw:{data.Length}>"); return Task.CompletedTask; }
    public Task<byte[]> ReadRawAsync(int count) => Task.FromResult(_rawResponse ?? new byte[count]);
    public void Dispose() { }
}
