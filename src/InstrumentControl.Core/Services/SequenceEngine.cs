using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Services;

public enum SequenceState { Idle, Running, Paused, Completed, Error }

public class SequenceEngine
{
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _pauseTcs;
    private bool _paused;

    public SequenceState State { get; private set; } = SequenceState.Idle;
    public string CurrentBlockId { get; private set; } = string.Empty;

    public event EventHandler<string>? BlockExecuting;
    public event EventHandler<(string BlockId, BlockExecutionResult Result)>? BlockCompleted;
    public event EventHandler<string>? LogMessage;
    public event EventHandler<SequenceState>? StateChanged;
    public event EventHandler<Exception>? Error;

    public async Task RunAsync(
        SequenceDefinition definition,
        Dictionary<string, IInstrumentDriver> instruments,
        DataManager dataManager,
        Action<MeasurementResult>? onMeasurement = null)
    {
        if (State == SequenceState.Running) return;

        _cts = new CancellationTokenSource();
        _paused = false;

        var blocks = BuildBlockDictionary(definition);
        if (blocks.Count == 0)
        {
            LogMessage?.Invoke(this, "Brak bloków do wykonania.");
            return;
        }

        var startBlock = blocks.Values.FirstOrDefault(b => b.BlockType == "StartBlock");
        if (startBlock == null)
        {
            LogMessage?.Invoke(this, "Brak bloku Start.");
            return;
        }

        var context = new SequenceContext
        {
            CancellationToken = _cts.Token,
            Log = msg => LogMessage?.Invoke(this, msg),
            OnMeasurement = r => { dataManager.AddResult(r); onMeasurement?.Invoke(r); }
        };
        foreach (var inst in instruments) context.Instruments[inst.Key] = inst.Value;
        context.AllBlocks = blocks;

        SetState(SequenceState.Running);
        LogMessage?.Invoke(this, "Sekwencja uruchomiona.");

        try
        {
            string? currentId = startBlock.BlockId;
            int safetyCounter = 0;
            const int maxIterations = 100000;

            while (currentId != null && !_cts.Token.IsCancellationRequested)
            {
                if (++safetyCounter > maxIterations)
                    throw new InvalidOperationException("Przekroczono limit iteracji — możliwa nieskończona pętla.");

                if (_paused)
                {
                    _pauseTcs = new TaskCompletionSource();
                    LogMessage?.Invoke(this, "Sekwencja wstrzymana.");
                    SetState(SequenceState.Paused);
                    await _pauseTcs.Task;
                    SetState(SequenceState.Running);
                    LogMessage?.Invoke(this, "Sekwencja wznowiona.");
                }

                if (!blocks.TryGetValue(currentId, out var block))
                {
                    LogMessage?.Invoke(this, $"Nie znaleziono bloku: {currentId}");
                    break;
                }

                CurrentBlockId = currentId;
                BlockExecuting?.Invoke(this, currentId);
                LogMessage?.Invoke(this, $"Wykonuję: {block.DisplayName}");

                var result = await block.ExecuteAsync(context);
                BlockCompleted?.Invoke(this, (currentId, result));

                if (!result.Success)
                {
                    LogMessage?.Invoke(this, $"Błąd bloku {block.DisplayName}: {result.ErrorMessage}");
                    SetState(SequenceState.Error);
                    return;
                }

                currentId = result.NextBlockId;
            }

            SetState(SequenceState.Completed);
            LogMessage?.Invoke(this, "Sekwencja zakończona.");
        }
        catch (OperationCanceledException)
        {
            SetState(SequenceState.Idle);
            LogMessage?.Invoke(this, "Sekwencja zatrzymana.");
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            SetState(SequenceState.Error);
            LogMessage?.Invoke(this, $"Błąd: {ex.Message}");
        }
    }

    public void Pause() => _paused = true;

    public void Resume()
    {
        _paused = false;
        _pauseTcs?.TrySetResult();
    }

    public void Stop() => _cts?.Cancel();

    private void SetState(SequenceState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static Dictionary<string, ISequenceBlock> BuildBlockDictionary(SequenceDefinition def)
    {
        var result = new Dictionary<string, ISequenceBlock>();
        foreach (var bd in def.Blocks)
        {
            var block = BlockRegistry.Create(bd.BlockType);
            if (block == null) continue;
            block.Deserialize(bd);
            result[block.BlockId] = block;
        }
        return result;
    }
}

public static class BlockRegistry
{
    private static readonly Dictionary<string, Func<ISequenceBlock>> _factories = new();

    public static void Register(string blockType, Func<ISequenceBlock> factory) =>
        _factories[blockType] = factory;

    public static ISequenceBlock? Create(string blockType) =>
        _factories.TryGetValue(blockType, out var f) ? f() : null;

    public static IEnumerable<string> GetRegisteredTypes() => _factories.Keys;
}
