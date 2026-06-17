using InstrumentControl.Core.Enums;

namespace InstrumentControl.Core.Models;

public record LogEntry(DateTime Timestamp, LogSource Source, string Message)
{
    public string Formatted =>
        $"({Timestamp:HH:mm:ss.fff}) [{Source,-10}] {Message}";
}
