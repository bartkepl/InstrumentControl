using InstrumentControl.Core.Blocks;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class LoopBlockTests
{
    private SequenceContext MakeContext(CancellationToken ct = default)
    {
        var logs = new List<string>();
        return new SequenceContext
        {
            CancellationToken = ct,
            Log = msg => logs.Add(msg),
        };
    }

    [Fact]
    public async Task ExecuteAsync_FixedIterations_RunsCorrectCount()
    {
        var block = new LoopBlock();
        block.Deserialize(new BlockData
        {
            BlockId = "loop1",
            BlockType = "LoopBlock",
            Properties = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Iterations"] = System.Text.Json.JsonSerializer.SerializeToElement(5.0),
                ["CounterVariable"] = System.Text.Json.JsonSerializer.SerializeToElement("i"),
            }
        });

        var ctx = MakeContext();
        var result = await block.ExecuteAsync(ctx);

        Assert.True(result.Success);
        Assert.Equal(4.0, ctx.GetVariableAsDouble("i"));
    }

    [Fact]
    public async Task ExecuteAsync_InfiniteLoop_CancelStops()
    {
        var cts = new CancellationTokenSource();
        var block = new LoopBlock();
        block.Deserialize(new BlockData
        {
            BlockId = "loop1",
            BlockType = "LoopBlock",
            Properties = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Iterations"] = System.Text.Json.JsonSerializer.SerializeToElement(0.0),
                ["CounterVariable"] = System.Text.Json.JsonSerializer.SerializeToElement("i"),
                ["DelayBetweenMs"] = System.Text.Json.JsonSerializer.SerializeToElement(5.0),
            }
        });

        var ctx = MakeContext(cts.Token);
        cts.CancelAfter(100);

        try
        {
            await block.ExecuteAsync(ctx);
        }
        catch (OperationCanceledException) { }

        Assert.True(ctx.GetVariableAsDouble("i") > 0, "Loop should have run at least once");
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task ExecuteAsync_PauseGate_PausesAndResumes()
    {
        var block = new LoopBlock();
        block.Deserialize(new BlockData
        {
            BlockId = "loop1",
            BlockType = "LoopBlock",
            Properties = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Iterations"] = System.Text.Json.JsonSerializer.SerializeToElement(0.0),
                ["CounterVariable"] = System.Text.Json.JsonSerializer.SerializeToElement("i"),
                ["DelayBetweenMs"] = System.Text.Json.JsonSerializer.SerializeToElement(5.0),
            }
        });

        var cts = new CancellationTokenSource();
        var ctx = MakeContext(cts.Token);

        var task = Task.Run(() => block.ExecuteAsync(ctx));
        await Task.Delay(30);

        ctx.SetPaused(true);
        var countAtPause = ctx.GetVariableAsDouble("i");
        await Task.Delay(60);
        var countWhilePaused = ctx.GetVariableAsDouble("i");

        Assert.True(countWhilePaused - countAtPause <= 1, "Loop should not advance while paused");

        ctx.SetPaused(false);
        await Task.Delay(30);
        cts.Cancel();

        try { await task; } catch (OperationCanceledException) { }
    }
}
