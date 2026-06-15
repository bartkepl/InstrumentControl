using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class WaitBlock : SequenceBlockBase
{
    public override string BlockType => "WaitBlock";
    public override string DisplayName => "Czekaj";
    public override string Description => "Czeka podany czas przed przejściem do następnego bloku";
    public override Color BlockColor => Color.FromRgb(0x95, 0xA5, 0xA6);
    public override string Category => "Ogólne";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Number("DelayMs", "Czas oczekiwania [ms]", 1000),
    ];

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        double delay = GetProp<double>("DelayMs", 1000);
        context.Log?.Invoke($"Czekam {delay} ms...");
        await Task.Delay((int)Math.Max(0, delay), context.CancellationToken);
        return BlockExecutionResult.Ok(NextBlockId);
    }

    static WaitBlock() => BlockRegistry.Register("WaitBlock", () => new WaitBlock());
}
