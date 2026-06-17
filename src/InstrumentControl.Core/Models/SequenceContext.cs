using InstrumentControl.Core.Interfaces;

namespace InstrumentControl.Core.Models;

public class SequenceContext
{
    public Dictionary<string, IInstrumentDriver> Instruments { get; } = new();
    public Dictionary<string, object?> Variables { get; } = new();
    public Dictionary<string, ISequenceBlock> AllBlocks { get; set; } = new();
    public List<MeasurementResult> Results { get; } = new();
    public CancellationToken CancellationToken { get; init; }
    public Action<string>? Log { get; init; }
    public Action<MeasurementResult>? OnMeasurement { get; init; }

    // ── Pause gate ──────────────────────────────────────────────────────────
    private volatile bool _isPaused;
    private TaskCompletionSource? _pauseTcs;
    private readonly object _pauseLock = new();

    public bool IsPaused => _isPaused;

    public void SetPaused(bool paused)
    {
        lock (_pauseLock)
        {
            if (paused && !_isPaused)
            {
                _isPaused = true;
                _pauseTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            else if (!paused && _isPaused)
            {
                _isPaused = false;
                _pauseTcs?.TrySetResult();
            }
        }
    }

    public async Task WaitIfPausedAsync()
    {
        if (!_isPaused) return;
        TaskCompletionSource? tcs;
        lock (_pauseLock)
        {
            if (!_isPaused) return;
            tcs = _pauseTcs;
        }
        if (tcs != null)
        {
            using var reg = CancellationToken.Register(() => tcs.TrySetCanceled());
            await tcs.Task;
        }
    }

    public void SetVariable(string name, object? value) => Variables[name] = value;

    public T? GetVariable<T>(string name)
    {
        if (!Variables.TryGetValue(name, out var val)) return default;
        if (val is T typed) return typed;
        try { return (T)Convert.ChangeType(val, typeof(T))!; }
        catch { return default; }
    }

    public double GetVariableAsDouble(string name, double defaultValue = 0)
    {
        if (!Variables.TryGetValue(name, out var val)) return defaultValue;
        return val switch
        {
            double d => d,
            float f => f,
            int i => i,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => defaultValue
        };
    }

    public void AddResult(MeasurementResult result)
    {
        Results.Add(result);
        OnMeasurement?.Invoke(result);
    }
}
