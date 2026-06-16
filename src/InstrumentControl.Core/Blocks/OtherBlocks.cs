using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class LogMessageBlock : SequenceBlockBase
{
    public override string BlockType => "LogMessageBlock";
    public override string DisplayName => "Loguj wiadomość";
    public override string Description => "Wyświetla wiadomość w logu sekwencji";
    public override Color BlockColor => Color.FromRgb(0xF3, 0x9C, 0x12);
    public override string Category => "General";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Text("Message", "Wiadomość", "Punkt kontrolny"),
    ];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string msg = GetPropStr("Message", "");
        context.Log?.Invoke($"[LOG] {msg}");
        return Task.FromResult(BlockExecutionResult.Ok(NextBlockId));
    }

    static LogMessageBlock() => BlockRegistry.Register("LogMessageBlock", () => new LogMessageBlock());
}

public class SetVariableBlock : SequenceBlockBase
{
    public override string BlockType => "SetVariableBlock";
    public override string DisplayName => "Ustaw zmienną";
    public override string Description => "Ustawia wartość zmiennej w kontekście sekwencji";
    public override Color BlockColor => Color.FromRgb(0x16, 0xA0, 0x85);
    public override string Category => "Data";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Variable("VariableName", "Nazwa zmiennej", "mojaZmienna"),
        BlockPropertyDefinition.Text("Value", "Wartość", "0"),
    ];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string name = GetPropStr("VariableName", "");
        string valueStr = GetPropStr("Value", "0");
        if (string.IsNullOrEmpty(name))
            return Task.FromResult(BlockExecutionResult.Fail("Nazwa zmiennej jest pusta"));

        object value = double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double d) ? (object)d : valueStr;

        context.SetVariable(name, value);
        context.Log?.Invoke($"Zmienna '{name}' = {value}");
        return Task.FromResult(BlockExecutionResult.Ok(NextBlockId));
    }

    static SetVariableBlock() => BlockRegistry.Register("SetVariableBlock", () => new SetVariableBlock());
}

public class ConditionBlock : SequenceBlockBase, IHasConditionOutputs
{
    public override string BlockType => "ConditionBlock";
    public override string DisplayName => "Warunek";
    public override string Description => "Rozgałęzienie: jeśli warunek prawdziwy → True, inaczej → False";
    public override Color BlockColor => Color.FromRgb(0xE7, 0x4C, 0x3C);
    public override string Category => "Control";

    public string? TrueBlockId { get; set; }
    public string? FalseBlockId { get; set; }

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Variable("Variable", "Zmienna", ""),
        BlockPropertyDefinition.Combo("Operator", "Operator", new() { ">", "<", ">=", "<=", "==", "!=" }, ">"),
        BlockPropertyDefinition.Number("CompareValue", "Wartość porównania", 0),
    ];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string varName = GetPropStr("Variable", "");
        string op = GetPropStr("Operator", ">");
        double compareVal = GetProp<double>("CompareValue", 0);

        double varVal = context.GetVariableAsDouble(varName, 0);

        bool condition = op switch
        {
            ">" => varVal > compareVal,
            "<" => varVal < compareVal,
            ">=" => varVal >= compareVal,
            "<=" => varVal <= compareVal,
            "==" => Math.Abs(varVal - compareVal) < 1e-12,
            "!=" => Math.Abs(varVal - compareVal) >= 1e-12,
            _ => false
        };

        string? next = condition ? TrueBlockId : FalseBlockId;
        context.Log?.Invoke($"Warunek: {varName}({varVal:G6}) {op} {compareVal} → {(condition ? "TRUE" : "FALSE")}");
        return Task.FromResult(BlockExecutionResult.Ok(next));
    }

    public override BlockData Serialize()
    {
        var data = base.Serialize();
        if (TrueBlockId != null)
            data.Properties["TrueBlockId"] = System.Text.Json.JsonSerializer.SerializeToElement(TrueBlockId);
        if (FalseBlockId != null)
            data.Properties["FalseBlockId"] = System.Text.Json.JsonSerializer.SerializeToElement(FalseBlockId);
        return data;
    }

    public override void Deserialize(BlockData data)
    {
        base.Deserialize(data);
        TrueBlockId = data.GetProperty<string>("TrueBlockId");
        FalseBlockId = data.GetProperty<string>("FalseBlockId");
        // Migrate from old format where TrueBlockId was stored as NextBlockId
        if (TrueBlockId == null && NextBlockId != null)
            TrueBlockId = NextBlockId;
        NextBlockId = null;
    }

    static ConditionBlock() => BlockRegistry.Register("ConditionBlock", () => new ConditionBlock());
}

public class EndLoopBlock : SequenceBlockBase
{
    public override string BlockType => "EndLoopBlock";
    public override string DisplayName => "Koniec pętli";
    public override string Description => "Oznacza koniec ciała pętli";
    public override Color BlockColor => Color.FromRgb(0xCA, 0x60, 0x00);
    public override string Category => "Control";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions => [];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
        => Task.FromResult(BlockExecutionResult.Ok(null));   // always terminates body chain

    static EndLoopBlock() => BlockRegistry.Register("EndLoopBlock", () => new EndLoopBlock());
}

