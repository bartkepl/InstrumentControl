using InstrumentControl.Core.Blocks;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class SequenceEngineTests
{
    static SequenceEngineTests()
    {
        _ = new StartBlock();
        _ = new EndBlock();
        _ = new LoopBlock();
    }

    private static SequenceDefinition MakeMinimalSequence()
    {
        var def = new SequenceDefinition { Name = "Test" };
        def.Blocks.Add(new BlockData { BlockId = "s1", BlockType = "StartBlock", NextBlockId = "e1" });
        def.Blocks.Add(new BlockData { BlockId = "e1", BlockType = "EndBlock" });
        return def;
    }

    [Fact]
    public async Task RunAsync_EmptyBlocks_DoesNotThrow()
    {
        var engine = new SequenceEngine();
        var def = new SequenceDefinition { Name = "Empty" };
        await engine.RunAsync(def, new(), new DataManager());
        Assert.Equal(SequenceState.Idle, engine.State);
    }

    [Fact]
    public async Task RunAsync_MinimalSequence_CompletesSuccessfully()
    {
        var engine = new SequenceEngine();
        var states = new List<SequenceState>();
        engine.StateChanged += (_, s) => states.Add(s);

        await engine.RunAsync(MakeMinimalSequence(), new(), new DataManager());

        Assert.Equal(SequenceState.Completed, engine.State);
        Assert.Contains(SequenceState.Running, states);
        Assert.Contains(SequenceState.Completed, states);
    }

    [Fact]
    public async Task Stop_CancelsExecution()
    {
        var engine = new SequenceEngine();
        var def = new SequenceDefinition { Name = "Loop" };
        def.Blocks.Add(new BlockData
        {
            BlockId = "s1", BlockType = "StartBlock", NextBlockId = "loop1"
        });
        def.Blocks.Add(new BlockData
        {
            BlockId = "loop1", BlockType = "LoopBlock",
            Properties = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Iterations"] = System.Text.Json.JsonSerializer.SerializeToElement(0.0),
                ["DelayBetweenMs"] = System.Text.Json.JsonSerializer.SerializeToElement(10.0),
            },
            NextBlockId = "e1"
        });
        def.Blocks.Add(new BlockData { BlockId = "e1", BlockType = "EndBlock" });

        var task = engine.RunAsync(def, new(), new DataManager());
        await Task.Delay(100);
        engine.Stop();
        await task;

        Assert.Equal(SequenceState.Idle, engine.State);
    }

    [Fact]
    public async Task PauseResume_WorksDuringLoop()
    {
        var engine = new SequenceEngine();
        var def = new SequenceDefinition { Name = "PauseTest" };
        def.Blocks.Add(new BlockData
        {
            BlockId = "s1", BlockType = "StartBlock", NextBlockId = "loop1"
        });
        def.Blocks.Add(new BlockData
        {
            BlockId = "loop1", BlockType = "LoopBlock",
            Properties = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["Iterations"] = System.Text.Json.JsonSerializer.SerializeToElement(0.0),
                ["DelayBetweenMs"] = System.Text.Json.JsonSerializer.SerializeToElement(10.0),
            },
            NextBlockId = "e1"
        });
        def.Blocks.Add(new BlockData { BlockId = "e1", BlockType = "EndBlock" });

        var task = engine.RunAsync(def, new(), new DataManager());
        await Task.Delay(50);

        engine.Pause();
        Assert.Equal(SequenceState.Paused, engine.State);

        await Task.Delay(50);
        engine.Resume();
        Assert.Equal(SequenceState.Running, engine.State);

        await Task.Delay(50);
        engine.Stop();
        await task;
    }
}
