using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class StartBlock : SequenceBlockBase, INoInputPort
{
    public override string BlockType => "StartBlock";
    public override string DisplayName => "Start";
    public override string Description => "Punkt startowy sekwencji";
    public override Color BlockColor => Color.FromRgb(0x27, 0xAE, 0x60);
    public override string Category => "Control";
    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions => Array.Empty<BlockPropertyDefinition>();

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context) =>
        Task.FromResult(BlockExecutionResult.Ok(NextBlockId));

    static StartBlock() => BlockRegistry.Register("StartBlock", () => new StartBlock());
}

public class EndBlock : SequenceBlockBase, INoOutputPort
{
    public override string BlockType => "EndBlock";
    public override string DisplayName => "Koniec";
    public override string Description => "KoĹ„czy sekwencjÄ™";
    public override Color BlockColor => Color.FromRgb(0x2C, 0x3E, 0x50);
    public override string Category => "Control";
    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions => Array.Empty<BlockPropertyDefinition>();

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context) =>
        Task.FromResult(BlockExecutionResult.Ok(null));

    static EndBlock() => BlockRegistry.Register("EndBlock", () => new EndBlock());
}

