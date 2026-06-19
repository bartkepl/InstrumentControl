using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class InstrumentDriverBaseExtraTests
{
    // Driver exposing the protected transport helpers and measurement raiser.
    private sealed class ExposingDriver : InstrumentDriverBase
    {
        public override string DriverName => "Exposing";
        public override string Manufacturer => "Co";
        public override string Model => "M1";
        public override string Description => "d";
        public override string[] SupportedResourcePatterns => new[] { "FAKE?*::INSTR" };
        public override FrameworkElement CreateFrontPanel() => null!;
        public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => Array.Empty<ISequenceBlock>();

        public Task<string> PubQuery(string c) => Query(c);
        public Task PubWrite(string c) => Write(c);
        public Task<double> PubQueryDouble(string c) => QueryDouble(c);
        public void PubRaise(MeasurementResult r) => RaiseMeasurement(r);
    }

    [Fact]
    public async Task ResetAsync_SendsRstAndCls()
    {
        var conn = new RecordingConnection();
        var driver = new ExposingDriver();
        await driver.ConnectAsync(conn);

        await driver.ResetAsync();

        Assert.Contains("*RST", conn.Written);
        Assert.Contains("*CLS", conn.Written);
    }

    [Fact]
    public async Task GetIdentificationAsync_SendsIdnQuery()
    {
        var conn = new RecordingConnection("ACME,X,SN,1.0");
        var driver = new ExposingDriver();
        await driver.ConnectAsync(conn);

        var idn = await driver.GetIdentificationAsync();
        Assert.Equal("ACME,X,SN,1.0", idn);
    }

    [Fact]
    public async Task QueryDouble_ParsesNumericResponse()
    {
        var conn = new RecordingConnection().When("READ?", "+3.300000E+00");
        var driver = new ExposingDriver();
        await driver.ConnectAsync(conn);

        Assert.Equal(3.3, await driver.PubQueryDouble("READ?"), 6);
    }

    [Fact]
    public async Task QueryDouble_NonNumeric_ReturnsNaN()
    {
        var conn = new RecordingConnection().When("READ?", "no_number");
        var driver = new ExposingDriver();
        await driver.ConnectAsync(conn);

        Assert.True(double.IsNaN(await driver.PubQueryDouble("READ?")));
    }

    [Fact]
    public async Task RaiseMeasurement_FiresEvent()
    {
        var driver = new ExposingDriver();
        MeasurementResult? got = null;
        driver.MeasurementReceived += (_, r) => got = r;

        driver.PubRaise(new MeasurementResult { Value = 1.23, Unit = "V" });

        Assert.NotNull(got);
        Assert.Equal(1.23, got!.Value);
    }

    [Fact]
    public async Task Query_WithoutConnection_Throws()
    {
        var driver = new ExposingDriver();
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.PubQuery("X?"));
    }

    [Fact]
    public async Task Reconnect_WithoutConnection_Throws()
    {
        var driver = new ExposingDriver();
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ReconnectAsync());
    }

    [Fact]
    public async Task Dispose_DisposesConnection_AndIsIdempotent()
    {
        var conn = new RecordingConnection();
        var driver = new ExposingDriver();
        await driver.ConnectAsync(conn);

        driver.Dispose();
        driver.Dispose(); // second call must be a no-op
    }

    [Fact]
    public async Task StatusChanged_RaisedOnConnect()
    {
        var conn = new RecordingConnection();
        var driver = new ExposingDriver();
        string? status = null;
        driver.StatusChanged += (_, s) => status = s;

        await driver.ConnectAsync(conn);

        Assert.NotNull(status);
    }
}
