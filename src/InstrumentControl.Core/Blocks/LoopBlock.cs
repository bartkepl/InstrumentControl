using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class LoopBlock : SequenceBlockBase, IHasBodyOutput
{
    public override string BlockType => "LoopBlock";
    public override string DisplayName => "Pętla";
    public override string Description => "Powtarza ciało pętli N razy, następnie przechodzi do kolejnego bloku";
    public override Color BlockColor => Color.FromRgb(0xE6, 0x7E, 0x22);
    public override string Category => "Sterowanie";

    public string? BodyBlockId { get; set; }

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Number("Iterations", "Liczba powtórzeń", 10),
        BlockPropertyDefinition.Variable("CounterVariable", "Zmienna licznika (opcjonalna)", ""),
        BlockPropertyDefinition.Number("DelayBetweenMs", "Opóźnienie między iteracjami [ms]", 0),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        int count = (int)GetProp<double>("Iterations", 10);
        string counterVar = GetPropStr("CounterVariable", "");
        double delayMs = GetProp<double>("DelayBetweenMs", 0);

        context.Log?.Invoke($"Pętla: {count} iteracji");

        for (int i = 0; i < count; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(counterVar))
                context.SetVariable(counterVar, (double)i);

            context.Log?.Invoke($"Iteracja {i + 1}/{count}");

            if (BodyBlockId != null && context.AllBlocks.Count > 0)
            {
                string? bodyId = BodyBlockId;
                int bodySafety = 0;
                while (bodyId != null && !context.CancellationToken.IsCancellationRequested)
                {
                    if (++bodySafety > 10000) break;
                    if (!context.AllBlocks.TryGetValue(bodyId, out var bodyBlock)) break;
                    var bodyResult = await bodyBlock.ExecuteAsync(context);
                    if (!bodyResult.Success)
                        return BlockExecutionResult.Fail($"Błąd w ciele pętli (iter. {i + 1}): {bodyResult.ErrorMessage}");
                    bodyId = bodyResult.NextBlockId;
                }
            }

            if (delayMs > 0)
                await Task.Delay((int)delayMs, context.CancellationToken);
        }

        return BlockExecutionResult.Ok(NextBlockId);
    }

    public override BlockData Serialize()
    {
        var data = base.Serialize();
        if (BodyBlockId != null)
            data.Properties["BodyBlockId"] = System.Text.Json.JsonSerializer.SerializeToElement(BodyBlockId);
        return data;
    }

    public override void Deserialize(BlockData data)
    {
        base.Deserialize(data);
        BodyBlockId = data.GetProperty<string>("BodyBlockId");
    }

    static LoopBlock() => BlockRegistry.Register("LoopBlock", () => new LoopBlock());
}
