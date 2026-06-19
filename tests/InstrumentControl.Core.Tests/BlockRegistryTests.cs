using InstrumentControl.Core.Blocks;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class BlockRegistryTests
{
    static BlockRegistryTests()
    {
        // Touch built-in blocks so their static constructors run and register them.
        _ = new StartBlock();
        _ = new EndBlock();
        _ = new WaitBlock();
        _ = new LoopBlock();
        _ = new MathBlock();
    }

    [Fact]
    public void Create_KnownType_ReturnsInstance()
    {
        var block = BlockRegistry.Create("StartBlock");
        Assert.NotNull(block);
        Assert.Equal("StartBlock", block!.BlockType);
    }

    [Fact]
    public void Create_UnknownType_ReturnsNull()
    {
        Assert.Null(BlockRegistry.Create("NoSuchBlock_xyz"));
    }

    [Fact]
    public void GetRegisteredTypes_ContainsBuiltIns()
    {
        var types = BlockRegistry.GetRegisteredTypes().ToList();
        Assert.Contains("StartBlock", types);
        Assert.Contains("EndBlock", types);
        Assert.Contains("WaitBlock", types);
        Assert.Contains("LoopBlock", types);
        Assert.Contains("MathBlock", types);
    }

    [Fact]
    public void Register_CustomFactory_Overrides()
    {
        BlockRegistry.Register("CustomTestBlock", () => new WaitBlock());
        var b = BlockRegistry.Create("CustomTestBlock");
        Assert.IsType<WaitBlock>(b);
    }
}
