using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Interfaces;

public interface ILogService
{
    event EventHandler<LogEntry>? EntryAdded;
    void Log(LogSource source, string message);
}
