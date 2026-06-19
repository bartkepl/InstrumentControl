using System.IO;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class DataManagerTests
{
    private static MeasurementResult R(string param, double value, string instr = "DMM", string unit = "V") =>
        new() { ParameterName = param, Value = value, InstrumentName = instr, Unit = unit };

    [Fact]
    public void AddResult_RaisesEvent_AndStores()
    {
        var dm = new DataManager();
        MeasurementResult? raised = null;
        dm.ResultAdded += (_, r) => raised = r;

        dm.AddResult(R("voltage", 1.0));

        Assert.Single(dm.Results);
        Assert.NotNull(raised);
        Assert.Equal(1.0, raised!.Value);
    }

    [Fact]
    public void AddResults_AddsAll()
    {
        var dm = new DataManager();
        dm.AddResults(new[] { R("v", 1), R("v", 2), R("v", 3) });
        Assert.Equal(3, dm.Results.Count);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var dm = new DataManager();
        dm.AddResult(R("v", 1));
        dm.Clear();
        Assert.Empty(dm.Results);
    }

    [Fact]
    public void GetSeries_IsCaseInsensitive()
    {
        var dm = new DataManager();
        dm.AddResult(R("Voltage", 1));
        dm.AddResult(R("voltage", 2));
        dm.AddResult(R("current", 3));
        Assert.Equal(2, dm.GetSeries("VOLTAGE").Count());
    }

    [Fact]
    public void GetParameterNames_DistinctSorted()
    {
        var dm = new DataManager();
        dm.AddResult(R("voltage", 1));
        dm.AddResult(R("current", 2));
        dm.AddResult(R("voltage", 3));
        Assert.Equal(new[] { "current", "voltage" }, dm.GetParameterNames().ToArray());
    }

    [Fact]
    public void ExportToCsv_WritesHeaderAndRows()
    {
        var dm = new DataManager();
        dm.AddResult(R("voltage", 1.5));
        string path = Path.Combine(Path.GetTempPath(), $"dm_{Guid.NewGuid():N}.csv");
        try
        {
            dm.ExportToCsv(path);
            var lines = File.ReadAllLines(path);
            Assert.StartsWith("Timestamp,", lines[0]);
            Assert.Equal(2, lines.Length);
            Assert.Contains("voltage", lines[1]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportToCsv_AppendMode_AddsWithoutDuplicatingHeader()
    {
        var dm = new DataManager();
        string path = Path.Combine(Path.GetTempPath(), $"dm_{Guid.NewGuid():N}.csv");
        try
        {
            dm.ExportToCsv(path, new[] { R("v", 1) }, append: false);
            dm.ExportToCsv(path, new[] { R("v", 2) }, append: true);
            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length); // 1 header + 2 rows
            Assert.Single(lines.Where(l => l.StartsWith("Timestamp")));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportToCsv_EscapesCommasAndQuotes()
    {
        var dm = new DataManager();
        dm.AddResult(new MeasurementResult { ParameterName = "a,b", InstrumentName = "x\"y", Value = 1 });
        string path = Path.Combine(Path.GetTempPath(), $"dm_{Guid.NewGuid():N}.csv");
        try
        {
            dm.ExportToCsv(path);
            string content = File.ReadAllText(path);
            Assert.Contains("\"a,b\"", content);
            Assert.Contains("\"x\"\"y\"", content);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
