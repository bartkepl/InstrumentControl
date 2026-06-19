using InstrumentControl.Core.Base;
using InstrumentControl.Core.Blocks;

namespace InstrumentControl.Core.Tests;

public class CoreBlocksMetadataTests
{
    public static IEnumerable<object[]> AllCoreBlocks() => new List<object[]>
    {
        new object[] { new StartBlock() },
        new object[] { new EndBlock() },
        new object[] { new EndLoopBlock() },
        new object[] { new WaitBlock() },
        new object[] { new LoopBlock() },
        new object[] { new LogMessageBlock() },
        new object[] { new SetVariableBlock() },
        new object[] { new ConditionBlock() },
        new object[] { new MathBlock() },
        new object[] { new AddToChartBlock() },
        new object[] { new SaveCsvBlock() },
    };

    [Theory]
    [MemberData(nameof(AllCoreBlocks))]
    public void Metadata_IsPopulated(SequenceBlockBase block)
    {
        Assert.False(string.IsNullOrWhiteSpace(block.BlockType));
        Assert.False(string.IsNullOrWhiteSpace(block.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(block.Description));
        Assert.False(string.IsNullOrWhiteSpace(block.Category));
        _ = block.BlockColor;                     // touch the color getter
        var defs = block.PropertyDefinitions.ToList();
        Assert.NotNull(defs);
        // Clone must produce a distinct instance with its own id
        var clone = block.Clone();
        Assert.NotEqual(block.BlockId, clone.BlockId);
        Assert.Equal(block.BlockType, clone.BlockType);
    }
}
