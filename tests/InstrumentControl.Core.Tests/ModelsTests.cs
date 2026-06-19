using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void BlockExecutionResult_Ok_SetsSuccessAndOutput()
    {
        var r = BlockExecutionResult.Ok("next", 42.0);
        Assert.True(r.Success);
        Assert.Equal("next", r.NextBlockId);
        Assert.Equal(42.0, r.OutputValue);
        Assert.Null(r.ErrorMessage);
    }

    [Fact]
    public void BlockExecutionResult_Fail_SetsError()
    {
        var r = BlockExecutionResult.Fail("boom");
        Assert.False(r.Success);
        Assert.Equal("boom", r.ErrorMessage);
    }

    [Fact]
    public void BlockPropertyDefinition_Factories_SetEditorTypes()
    {
        Assert.Equal(PropertyEditorType.TextBox, BlockPropertyDefinition.Text("a", "A").EditorType);
        Assert.Equal(PropertyEditorType.NumberBox, BlockPropertyDefinition.Number("a", "A").EditorType);
        Assert.Equal(PropertyEditorType.CheckBox, BlockPropertyDefinition.Check("a", "A", true).EditorType);
        Assert.Equal(PropertyEditorType.FilePath, BlockPropertyDefinition.FilePath("a", "A").EditorType);
        Assert.Equal(PropertyEditorType.VariableName, BlockPropertyDefinition.Variable("a", "A").EditorType);

        var instr = BlockPropertyDefinition.Instrument();
        Assert.Equal(PropertyEditorType.InstrumentSelector, instr.EditorType);
        Assert.True(instr.IsRequired);
    }

    [Fact]
    public void BlockPropertyDefinition_Combo_DefaultsToFirstOption()
    {
        var combo = BlockPropertyDefinition.Combo("a", "A", new() { "x", "y", "z" });
        Assert.Equal("x", combo.DefaultValue);
        Assert.Equal(3, combo.Options.Count);
    }

    [Fact]
    public void InstrumentInfo_DisplayName_UsesUserLabelWhenSet()
    {
        var info = new InstrumentInfo { Manufacturer = "HP", Model = "34401A" };
        Assert.Equal("HP 34401A", info.DisplayName);

        info.UserLabel = "Bench DMM";
        Assert.Equal("Bench DMM", info.DisplayName);
    }

    [Fact]
    public void LogEntry_Formatted_ContainsSourceAndMessage()
    {
        var entry = new LogEntry(new DateTime(2026, 1, 2, 13, 14, 15), LogSource.Visa, "hello");
        Assert.Contains("Visa", entry.Formatted);
        Assert.Contains("hello", entry.Formatted);
        Assert.Contains("13:14:15", entry.Formatted);
    }

    [Fact]
    public void MeasurementResult_ToString_IsHumanReadable()
    {
        var m = new MeasurementResult
        {
            InstrumentName = "DMM", ChannelId = "CH1", Function = "DCV", Value = 3.3, Unit = "V"
        };
        var s = m.ToString();
        Assert.Contains("DMM", s);
        Assert.Contains("DCV", s);
        Assert.Contains("V", s);
    }

    [Fact]
    public void SequenceDefinition_JsonRoundTrip_PreservesBlocks()
    {
        var def = new SequenceDefinition { Name = "MySeq" };
        def.Blocks.Add(new BlockData { BlockId = "s1", BlockType = "StartBlock", NextBlockId = "e1" });
        def.Blocks.Add(new BlockData { BlockId = "e1", BlockType = "EndBlock" });

        var json = def.ToJson();
        var restored = SequenceDefinition.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal("MySeq", restored!.Name);
        Assert.Equal(2, restored.Blocks.Count);
        Assert.Equal("StartBlock", restored.Blocks[0].BlockType);
    }

    [Fact]
    public void SequenceDefinition_FromJson_Invalid_ReturnsNull()
    {
        Assert.Null(SequenceDefinition.FromJson("{ not valid json"));
    }

    [Fact]
    public void BlockData_GetProperty_TypedAndDefault()
    {
        var data = new BlockData
        {
            Properties = TestSupport.Props(("count", 7.0), ("name", "abc"))
        };
        Assert.Equal(7.0, data.GetProperty<double>("count"));
        Assert.Equal("abc", data.GetProperty<string>("name"));
        Assert.Equal(99, data.GetProperty("missing", 99));
    }
}
