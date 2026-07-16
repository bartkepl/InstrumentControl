using System.Globalization;
using System.IO;
using System.Text;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Services;

/// <summary>
/// Continuously appends every measurement raised by any instrument driver to a
/// daily CSV file, independent of whether a Live window is open or how much
/// history that window keeps on screen — nothing measured is ever lost.
/// Same row shape as <see cref="DataManager.ExportToCsv"/>, written as
/// measurements happen instead of on demand.
/// </summary>
public static class MeasurementLogService
{
    private static readonly string LogDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static readonly object Lock = new();
    private static StreamWriter? _writer;
    private static string _currentDateStr = "";

    public static void Log(MeasurementResult result)
    {
        lock (Lock)
        {
            EnsureFile(result.Timestamp);
            _writer!.WriteLine(string.Join(",",
                result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EscapeCsv(result.InstrumentName),
                EscapeCsv(result.ChannelId),
                EscapeCsv(result.ParameterName),
                result.Value.ToString("G10", CultureInfo.InvariantCulture),
                EscapeCsv(result.Unit),
                EscapeCsv(result.Function),
                result.IsValid.ToString()));
        }
    }

    private static void EnsureFile(DateTime timestamp)
    {
        var dateStr = timestamp.ToString("yyyy-MM-dd");
        if (dateStr == _currentDateStr && _writer != null) return;

        _writer?.Flush();
        _writer?.Dispose();

        Directory.CreateDirectory(LogDirectory);
        var path = Path.Combine(LogDirectory, $"measurements_{dateStr}.csv");
        bool existed = File.Exists(path);
        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        _currentDateStr = dateStr;

        if (!existed)
            _writer.WriteLine("Timestamp,Instrument,Channel,Parameter,Value,Unit,Function,Valid");
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
