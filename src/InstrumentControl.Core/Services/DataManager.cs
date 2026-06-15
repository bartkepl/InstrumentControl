using System.IO;
using System.Text;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Services;

public class DataManager
{
    private readonly List<MeasurementResult> _results = new();
    private readonly object _lock = new();

    public IReadOnlyList<MeasurementResult> Results
    {
        get { lock (_lock) return _results.ToList(); }
    }

    public event EventHandler<MeasurementResult>? ResultAdded;

    public void AddResult(MeasurementResult result)
    {
        lock (_lock) _results.Add(result);
        ResultAdded?.Invoke(this, result);
    }

    public void AddResults(IEnumerable<MeasurementResult> results)
    {
        foreach (var r in results) AddResult(r);
    }

    public void Clear()
    {
        lock (_lock) _results.Clear();
    }

    public void ExportToCsv(string filePath, IEnumerable<MeasurementResult>? subset = null, bool append = false)
    {
        var data = subset ?? Results;
        var sb = new StringBuilder();
        bool fileExists = File.Exists(filePath);

        if (!append || !fileExists)
            sb.AppendLine("Timestamp,Instrument,Channel,Parameter,Value,Unit,Function,Valid");

        foreach (var r in data)
        {
            sb.AppendLine(string.Join(",",
                r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EscapeCsv(r.InstrumentName),
                EscapeCsv(r.ChannelId),
                EscapeCsv(r.ParameterName),
                r.Value.ToString("G10", System.Globalization.CultureInfo.InvariantCulture),
                EscapeCsv(r.Unit),
                EscapeCsv(r.Function),
                r.IsValid.ToString()
            ));
        }

        if (append && fileExists)
            File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
        else
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public IEnumerable<MeasurementResult> GetSeries(string parameterName) =>
        Results.Where(r => r.ParameterName.Equals(parameterName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> GetParameterNames() =>
        Results.Select(r => r.ParameterName).Distinct().OrderBy(x => x);

    private static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
