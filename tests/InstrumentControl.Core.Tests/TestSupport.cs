using System.Text.Json;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

/// <summary>Wspólne pomocniki dla testów Core.</summary>
internal static class TestSupport
{
    /// <summary>Tworzy kontekst sekwencji zbierający logi do podanej listy.</summary>
    public static SequenceContext MakeContext(List<string>? log = null, CancellationToken ct = default) =>
        new()
        {
            CancellationToken = ct,
            Log = log != null ? log.Add : null,
        };

    /// <summary>Pakuje surowe wartości właściwości bloku w JsonElement (format BlockData).</summary>
    public static Dictionary<string, JsonElement> Props(params (string Key, object? Value)[] items)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (key, value) in items)
            dict[key] = JsonSerializer.SerializeToElement(value);
        return dict;
    }
}

/// <summary>
/// Połączenie testowe rejestrujące wszystkie wysłane komendy i zwracające
/// zaprogramowane odpowiedzi na zapytania. Domyślnie odpowiada na *IDN?.
/// </summary>
internal sealed class RecordingConnection : IConnectionProvider
{
    private readonly Queue<string> _responses = new();
    private readonly Dictionary<string, string> _canned = new(StringComparer.OrdinalIgnoreCase);
    private string _defaultResponse;

    public RecordingConnection(string idn = "ACME,Model123,SN42,1.0", string defaultResponse = "+1.23456E+00")
    {
        _canned["*IDN?"] = idn;
        _defaultResponse = defaultResponse;
    }

    public List<string> Written { get; } = new();
    public List<string> Queried { get; } = new();
    public string ResourceName { get; init; } = "REC::INSTR";
    public string ConnectionType => "REC";
    public bool IsOpen { get; private set; }
    public int OpenCount { get; private set; }
    public int CloseCount { get; private set; }

    /// <summary>Zaprogramuj stałą odpowiedź na konkretną komendę.</summary>
    public RecordingConnection When(string command, string response)
    {
        _canned[command] = response;
        return this;
    }

    /// <summary>Dodaj odpowiedź do kolejki (zwracana w kolejności dla zapytań bez wpisu w When).</summary>
    public RecordingConnection Enqueue(params string[] responses)
    {
        foreach (var r in responses) _responses.Enqueue(r);
        return this;
    }

    public void SetDefault(string response) => _defaultResponse = response;

    public Task OpenAsync() { IsOpen = true; OpenCount++; return Task.CompletedTask; }
    public Task CloseAsync() { IsOpen = false; CloseCount++; return Task.CompletedTask; }

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
    public Task<byte[]> ReadRawAsync(int count) => Task.FromResult(new byte[count]);
    public void Dispose() { }
}
