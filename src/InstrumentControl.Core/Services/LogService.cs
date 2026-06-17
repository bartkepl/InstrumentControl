using System.IO;
using System.Text;
using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Services;

public sealed class LogService : ILogService, IDisposable
{
    private readonly string _logDirectory;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string _currentDateStr = "";

    public event EventHandler<LogEntry>? EntryAdded;

    public LogService()
    {
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public void Log(LogSource source, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var entry = new LogEntry(DateTime.Now, source, message.TrimEnd());
        lock (_lock)
        {
            EnsureFile(entry.Timestamp);
            _writer?.WriteLine(entry.Formatted);
        }
        EntryAdded?.Invoke(this, entry);
    }

    private void EnsureFile(DateTime timestamp)
    {
        var dateStr = timestamp.ToString("yyyy-MM-dd");
        if (dateStr == _currentDateStr) return;

        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        var path = Path.Combine(_logDirectory, $"log_{dateStr}.log");
        bool existed = File.Exists(path);
        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        _currentDateStr = dateStr;

        // Write session separator so multiple sessions in one file are distinguishable
        if (existed)
            _writer.WriteLine($"\n--- Nowa sesja: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
