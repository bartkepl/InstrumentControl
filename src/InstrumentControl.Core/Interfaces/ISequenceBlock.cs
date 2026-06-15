using System.Windows.Media;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Interfaces;

public interface ISequenceBlock
{
    string BlockId { get; set; }
    string BlockType { get; }
    string DisplayName { get; }
    string Description { get; }
    Color BlockColor { get; }
    string Category { get; }

    double X { get; set; }
    double Y { get; set; }

    string? NextBlockId { get; set; }

    Dictionary<string, object?> Properties { get; set; }
    IEnumerable<BlockPropertyDefinition> PropertyDefinitions { get; }

    Task<BlockExecutionResult> ExecuteAsync(SequenceContext context);

    BlockData Serialize();
    void Deserialize(BlockData data);

    ISequenceBlock Clone();
}
