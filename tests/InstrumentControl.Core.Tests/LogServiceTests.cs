using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class LogServiceTests
{
    [Fact]
    public void Log_RaisesEntryAdded_WithSourceAndMessage()
    {
        using var svc = new LogService();
        LogEntry? captured = null;
        svc.EntryAdded += (_, e) => captured = e;

        svc.Log(LogSource.Sequence, "test message");

        Assert.NotNull(captured);
        Assert.Equal(LogSource.Sequence, captured!.Source);
        Assert.Equal("test message", captured.Message);
    }

    [Fact]
    public void Log_EmptyOrWhitespace_Ignored()
    {
        using var svc = new LogService();
        int count = 0;
        svc.EntryAdded += (_, _) => count++;

        svc.Log(LogSource.System, "");
        svc.Log(LogSource.System, "   ");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Log_TrimsTrailingWhitespace()
    {
        using var svc = new LogService();
        LogEntry? captured = null;
        svc.EntryAdded += (_, e) => captured = e;

        svc.Log(LogSource.Instrument, "value   ");

        Assert.Equal("value", captured!.Message);
    }

    [Fact]
    public void MultipleLogs_AllRaiseEvents()
    {
        using var svc = new LogService();
        int count = 0;
        svc.EntryAdded += (_, _) => count++;

        svc.Log(LogSource.Visa, "a");
        svc.Log(LogSource.Serial, "b");
        svc.Log(LogSource.Debug, "c");

        Assert.Equal(3, count);
    }
}
