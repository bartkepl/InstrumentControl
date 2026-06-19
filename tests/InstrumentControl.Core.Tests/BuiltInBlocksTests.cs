using System.IO;
using InstrumentControl.Core.Blocks;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class BuiltInBlocksTests
{
    [Fact]
    public async Task StartBlock_ReturnsNext()
    {
        var b = new StartBlock { NextBlockId = "n1" };
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.True(r.Success);
        Assert.Equal("n1", r.NextBlockId);
    }

    [Fact]
    public async Task EndBlock_TerminatesWithNullNext()
    {
        var b = new EndBlock { NextBlockId = "ignored" };
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.True(r.Success);
        Assert.Null(r.NextBlockId);
    }

    [Fact]
    public async Task EndLoopBlock_AlwaysTerminatesBodyChain()
    {
        var b = new EndLoopBlock { NextBlockId = "x" };
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.True(r.Success);
        Assert.Null(r.NextBlockId);
    }

    [Fact]
    public async Task WaitBlock_DelaysAndContinues()
    {
        var b = new WaitBlock();
        b.Deserialize(new BlockData { BlockId = "w", NextBlockId = "after", Properties = TestSupport.Props(("DelayMs", 5.0)) });
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.True(r.Success);
        Assert.Equal("after", r.NextBlockId);
    }

    [Fact]
    public async Task WaitBlock_NegativeDelay_ClampedToZero()
    {
        var b = new WaitBlock();
        b.Deserialize(new BlockData { BlockId = "w", Properties = TestSupport.Props(("DelayMs", -50.0)) });
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.True(r.Success);
    }

    [Fact]
    public async Task WaitBlock_CancelledToken_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var b = new WaitBlock();
        b.Deserialize(new BlockData { BlockId = "w", Properties = TestSupport.Props(("DelayMs", 1000.0)) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => b.ExecuteAsync(TestSupport.MakeContext(ct: cts.Token)));
    }

    [Fact]
    public async Task LogMessageBlock_WritesToLog()
    {
        var log = new List<string>();
        var b = new LogMessageBlock { NextBlockId = "n" };
        b.Deserialize(new BlockData { BlockId = "l", Properties = TestSupport.Props(("Message", "hello")) });
        var r = await b.ExecuteAsync(TestSupport.MakeContext(log));
        Assert.True(r.Success);
        Assert.Contains(log, m => m.Contains("hello"));
    }

    [Fact]
    public async Task SetVariableBlock_NumericValue_StoredAsDouble()
    {
        var b = new SetVariableBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "sv",
            Properties = TestSupport.Props(("VariableName", "v"), ("Value", "3.14"))
        });
        var ctx = TestSupport.MakeContext();
        var r = await b.ExecuteAsync(ctx);
        Assert.True(r.Success);
        Assert.Equal(3.14, ctx.GetVariableAsDouble("v"), 5);
    }

    [Fact]
    public async Task SetVariableBlock_NonNumeric_StoredAsString()
    {
        var b = new SetVariableBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "sv",
            Properties = TestSupport.Props(("VariableName", "name"), ("Value", "abc"))
        });
        var ctx = TestSupport.MakeContext();
        await b.ExecuteAsync(ctx);
        Assert.Equal("abc", ctx.GetVariable<string>("name"));
    }

    [Fact]
    public async Task SetVariableBlock_EmptyName_Fails()
    {
        var b = new SetVariableBlock();
        b.Deserialize(new BlockData { BlockId = "sv", Properties = TestSupport.Props(("VariableName", ""), ("Value", "1")) });
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.False(r.Success);
    }

    [Theory]
    [InlineData(">", 5.0, 3.0, true)]
    [InlineData("<", 5.0, 3.0, false)]
    [InlineData(">=", 3.0, 3.0, true)]
    [InlineData("<=", 3.0, 3.0, true)]
    [InlineData("==", 3.0, 3.0, true)]
    [InlineData("!=", 3.0, 3.0, false)]
    public async Task ConditionBlock_RoutesToTrueOrFalse(string op, double varVal, double cmp, bool expectTrue)
    {
        var b = new ConditionBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "c",
            Properties = TestSupport.Props(
                ("Variable", "x"), ("Operator", op), ("CompareValue", cmp),
                ("TrueBlockId", "T"), ("FalseBlockId", "F"))
        });
        var ctx = TestSupport.MakeContext();
        ctx.SetVariable("x", varVal);

        var r = await b.ExecuteAsync(ctx);
        Assert.True(r.Success);
        Assert.Equal(expectTrue ? "T" : "F", r.NextBlockId);
    }

    [Fact]
    public void ConditionBlock_SerializeDeserialize_RoundTripsBranchIds()
    {
        var b = new ConditionBlock { TrueBlockId = "yes", FalseBlockId = "no" };
        var data = b.Serialize();

        var clone = new ConditionBlock();
        clone.Deserialize(data);

        Assert.Equal("yes", clone.TrueBlockId);
        Assert.Equal("no", clone.FalseBlockId);
        Assert.Null(clone.NextBlockId);
    }

    [Fact]
    public void ConditionBlock_Deserialize_MigratesLegacyNextBlockId()
    {
        // Old format stored the "true" branch in NextBlockId.
        var clone = new ConditionBlock();
        clone.Deserialize(new BlockData { BlockId = "c", NextBlockId = "legacyTrue" });
        Assert.Equal("legacyTrue", clone.TrueBlockId);
        Assert.Null(clone.NextBlockId);
    }

    [Fact]
    public async Task AddToChartBlock_AddsMeasurementResult()
    {
        var b = new AddToChartBlock { NextBlockId = "n" };
        b.Deserialize(new BlockData
        {
            BlockId = "a",
            Properties = TestSupport.Props(("ValueVariable", "v"), ("SeriesName", "S1"), ("Unit", "V"))
        });
        MeasurementResult? received = null;
        var ctx = new SequenceContext { CancellationToken = default, OnMeasurement = m => received = m };
        ctx.SetVariable("v", 2.5);

        var r = await b.ExecuteAsync(ctx);

        Assert.True(r.Success);
        Assert.Single(ctx.Results);
        Assert.NotNull(received);
        Assert.Equal("S1", received!.ParameterName);
        Assert.Equal(2.5, received.Value);
        Assert.Equal("V", received.Unit);
    }

    [Fact]
    public async Task AddToChartBlock_EmptyVariableName_Fails()
    {
        var b = new AddToChartBlock();
        b.Deserialize(new BlockData { BlockId = "a", Properties = TestSupport.Props(("ValueVariable", "")) });
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.False(r.Success);
    }

    [Fact]
    public async Task SaveCsvBlock_WritesNewResultsToFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ic_test_{Guid.NewGuid():N}.csv");
        try
        {
            var b = new SaveCsvBlock { BlockId = "csv1" };
            b.Deserialize(new BlockData
            {
                BlockId = "csv1",
                Properties = TestSupport.Props(("FilePath", path), ("AppendMode", false), ("SaveAll", true))
            });
            var ctx = TestSupport.MakeContext();
            ctx.AddResult(new MeasurementResult { InstrumentName = "DMM", Value = 1.0, Unit = "V", ParameterName = "voltage" });
            ctx.AddResult(new MeasurementResult { InstrumentName = "DMM", Value = 2.0, Unit = "V", ParameterName = "voltage" });

            var r = await b.ExecuteAsync(ctx);

            Assert.True(r.Success);
            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length); // header + 2 rows
            Assert.Contains("Timestamp", lines[0]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveCsvBlock_EmptyPath_Fails()
    {
        var b = new SaveCsvBlock();
        b.Deserialize(new BlockData { BlockId = "csv", Properties = TestSupport.Props(("FilePath", "   ")) });
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.False(r.Success);
    }

    [Fact]
    public async Task SaveCsvBlock_OnlyNewResults_TracksIndexBetweenRuns()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ic_test_{Guid.NewGuid():N}.csv");
        try
        {
            var b = new SaveCsvBlock { BlockId = "csvinc" };
            b.Deserialize(new BlockData
            {
                BlockId = "csvinc",
                Properties = TestSupport.Props(("FilePath", path), ("AppendMode", true), ("SaveAll", false))
            });
            var ctx = TestSupport.MakeContext();
            ctx.AddResult(new MeasurementResult { Value = 1.0 });
            await b.ExecuteAsync(ctx);          // saves row 1
            ctx.AddResult(new MeasurementResult { Value = 2.0 });
            await b.ExecuteAsync(ctx);          // saves only the new row 2

            var lines = File.ReadAllLines(path);
            // header + 2 data rows total, no duplicate of row 1
            Assert.Equal(3, lines.Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
