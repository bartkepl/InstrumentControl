using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class AddToChartBlock : SequenceBlockBase
{
    public override string BlockType => "AddToChartBlock";
    public override string DisplayName => "Dodaj do wykresu";
    public override string Description => "Przekazuje wartość zmiennej do wykresu na żywo";
    public override Color BlockColor => Color.FromRgb(0x8E, 0x44, 0xAD);
    public override string Category => "Data";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Variable("ValueVariable", "Zmienna z wartością", "voltage"),
        BlockPropertyDefinition.Text("SeriesName", "Nazwa serii", "Pomiar 1"),
        BlockPropertyDefinition.Text("Unit", "Jednostka", "V"),
    ];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string varName = GetPropStr("ValueVariable", "");
        string seriesName = GetPropStr("SeriesName", "Pomiar");
        string unit = GetPropStr("Unit", "");

        if (string.IsNullOrEmpty(varName))
            return Task.FromResult(BlockExecutionResult.Fail("Nie podano nazwy zmiennej"));

        double value = context.GetVariableAsDouble(varName, double.NaN);

        var result = new MeasurementResult
        {
            ParameterName = seriesName,
            Value = value,
            Unit = unit,
            Function = "CHART",
            InstrumentName = "Chart",
            ChannelId = varName
        };
        context.AddResult(result);
        context.Log?.Invoke($"Wykres: {seriesName} = {value:G6} {unit}");
        return Task.FromResult(BlockExecutionResult.Ok(NextBlockId));
    }

    static AddToChartBlock() => BlockRegistry.Register("AddToChartBlock", () => new AddToChartBlock());
}
