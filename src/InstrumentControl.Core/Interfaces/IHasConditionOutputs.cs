namespace InstrumentControl.Core.Interfaces;

public interface IHasConditionOutputs
{
    string? TrueBlockId { get; set; }
    string? FalseBlockId { get; set; }
}
