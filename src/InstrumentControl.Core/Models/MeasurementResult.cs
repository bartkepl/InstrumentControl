namespace InstrumentControl.Core.Models;

public class MeasurementResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string InstrumentName { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Function { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}] {InstrumentName}/{ChannelId} {Function}: {Value:G6} {Unit}";
}
