namespace InstrumentControl.Core.Models;

public class BlockExecutionResult
{
    public bool Success { get; set; }
    public string? NextBlockId { get; set; }
    public string? ErrorMessage { get; set; }
    public object? OutputValue { get; set; }

    public static BlockExecutionResult Ok(string? nextBlockId = null, object? output = null) =>
        new() { Success = true, NextBlockId = nextBlockId, OutputValue = output };

    public static BlockExecutionResult Fail(string error, string? nextBlockId = null) =>
        new() { Success = false, ErrorMessage = error, NextBlockId = nextBlockId };
}
