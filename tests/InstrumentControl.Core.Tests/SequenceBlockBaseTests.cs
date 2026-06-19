using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class SequenceBlockBaseTests
{
    // Concrete block exposing protected GetProp helpers and a default-valued property.
    private sealed class SampleBlock : SequenceBlockBase
    {
        public override string BlockType => "SampleBlock";
        public override string DisplayName => "Sample";
        public override string Description => "test";
        public override Color BlockColor => Colors.Gray;
        public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
        [
            BlockPropertyDefinition.Number("Gain", "Gain", 2.0),
            BlockPropertyDefinition.Text("Label", "Label", "default"),
        ];
        public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context) =>
            Task.FromResult(BlockExecutionResult.Ok(NextBlockId));

        public double PublicGetGain() => GetProp<double>("Gain", 1.0);
        public string PublicGetLabel() => GetPropStr("Label", "fallback");
    }

    [Fact]
    public void NewBlock_HasGeneratedId()
    {
        var a = new SampleBlock();
        var b = new SampleBlock();
        Assert.False(string.IsNullOrEmpty(a.BlockId));
        Assert.NotEqual(a.BlockId, b.BlockId);
    }

    [Fact]
    public void GetProp_MissingKey_ReturnsDefault()
    {
        var b = new SampleBlock();
        Assert.Equal(1.0, b.PublicGetGain());
        Assert.Equal("fallback", b.PublicGetLabel());
    }

    [Fact]
    public void GetProp_FromJsonElement_Deserializes()
    {
        var b = new SampleBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "x",
            Properties = TestSupport.Props(("Gain", 5.5), ("Label", "hi"))
        });
        Assert.Equal(5.5, b.PublicGetGain());
        Assert.Equal("hi", b.PublicGetLabel());
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsCoreFields()
    {
        var b = new SampleBlock { X = 120, Y = 80, NextBlockId = "n9" };
        b.Properties["Gain"] = 3.0;

        var data = b.Serialize();
        Assert.Equal("SampleBlock", data.BlockType);
        Assert.Equal(120, data.X);

        var restored = new SampleBlock();
        restored.Deserialize(data);
        Assert.Equal(b.BlockId, restored.BlockId);
        Assert.Equal("n9", restored.NextBlockId);
        Assert.Equal(3.0, restored.PublicGetGain());
    }

    [Fact]
    public void Clone_GetsNewIdAndPrepopulatesDefaults()
    {
        var original = new SampleBlock();
        var clone = (SampleBlock)original.Clone();

        Assert.NotEqual(original.BlockId, clone.BlockId);
        // defaults pre-populated from PropertyDefinitions
        Assert.Equal(2.0, clone.PublicGetGain());
        Assert.Equal("default", clone.PublicGetLabel());
    }

    [Fact]
    public void Clone_DoesNotSharePropertiesDictionary()
    {
        var original = new SampleBlock();
        original.Properties["Gain"] = 10.0;
        var clone = (SampleBlock)original.Clone();
        clone.Properties["Gain"] = 99.0;
        Assert.Equal(10.0, original.PublicGetGain());
    }
}
