using RTB2004;

namespace InstrumentControl.Instruments.Tests;

public class RTB2004DriverTests
{
    private static async Task<(RTB2004Driver Driver, RecordingConnection Conn)> ConnectAsync()
    {
        var conn = new RecordingConnection("Rohde&Schwarz,RTB2004,SN1,1.0");
        var driver = new RTB2004Driver();
        await driver.ConnectAsync(conn);
        return (driver, conn);
    }

    [Fact]
    public void Identity_IsCorrect()
    {
        var driver = new RTB2004Driver();
        Assert.Equal("RTB2004", driver.DriverName);
        Assert.Equal("Rohde & Schwarz", driver.Manufacturer);
    }

    [Fact]
    public async Task SetChannelEnabled_OnOff()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetChannelEnabledAsync(2, true);
        await driver.SetChannelEnabledAsync(3, false);
        Assert.Contains("CHAN2:STAT ON", conn.Written);
        Assert.Contains("CHAN3:STAT OFF", conn.Written);
    }

    [Fact]
    public async Task SetChannelScale_UsesInvariantCulture()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetChannelScaleAsync(1, 0.5);
        Assert.Contains("CHAN1:SCAL 0.5", conn.Written);
    }

    [Fact]
    public async Task GetChannelScale_ParsesResponse()
    {
        var conn = new RecordingConnection("Rohde&Schwarz,RTB2004,SN1,1.0").When("CHAN1:SCAL?", "0.2");
        var driver = new RTB2004Driver();
        await driver.ConnectAsync(conn);
        Assert.Equal(0.2, await driver.GetChannelScaleAsync(1), 6);
    }

    [Fact]
    public async Task SetChannelCoupling_UppercasesValue()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetChannelCouplingAsync(1, "dc");
        Assert.Contains("CHAN1:COUP DC", conn.Written);
    }

    [Fact]
    public async Task Timebase_SetAndGet()
    {
        var conn = new RecordingConnection("Rohde&Schwarz,RTB2004,SN1,1.0").When("TIM:SCAL?", "1E-3");
        var driver = new RTB2004Driver();
        await driver.ConnectAsync(conn);
        await driver.SetTimescaleAsync(1e-3);
        Assert.Contains("TIM:SCAL 0.001", conn.Written);
        Assert.Equal(1e-3, await driver.GetTimescaleAsync(), 9);
    }

    [Fact]
    public async Task Trigger_SourceLevelSlope()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetTriggerSourceAsync("ch1");
        await driver.SetTriggerLevelAsync(1, 1.5);
        await driver.SetTriggerSlopeAsync("pos");
        Assert.Contains("TRIG:A:SOUR CH1", conn.Written);
        Assert.Contains("TRIG:A:LEV1 1.5", conn.Written);
        Assert.Contains("TRIG:A:EDGE:SLOP POS", conn.Written);
    }

    [Fact]
    public async Task AcquisitionControls_SendKeywords()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.RunAsync();
        await driver.StopAsync();
        await driver.SingleAsync();
        await driver.AutoscaleAsync();
        Assert.Contains("RUN", conn.Written);
        Assert.Contains("STOP", conn.Written);
        Assert.Contains("SING", conn.Written);
        Assert.Contains("AUT", conn.Written);
    }

    [Fact]
    public async Task SetMeasurement_ConfiguresSlot()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetMeasurementAsync(1, 2, "freq");
        Assert.Contains("MEAS1:SOUR CH2", conn.Written);
        Assert.Contains("MEAS1:MAIN FREQ", conn.Written);
        Assert.Contains("MEAS1:ENAB ON", conn.Written);
    }

    [Fact]
    public async Task MeasureChannel_RaisesMeasurementWithUnit()
    {
        var conn = new RecordingConnection("Rohde&Schwarz,RTB2004,SN1,1.0").When("MEAS1:RES:ACT?", "1000");
        var driver = new RTB2004Driver();
        await driver.ConnectAsync(conn);
        InstrumentControl.Core.Models.MeasurementResult? got = null;
        driver.MeasurementReceived += (_, r) => got = r;

        double f = await driver.MeasureChannelAsync(1, "FREQ");

        Assert.Equal(1000, f, 3);
        Assert.NotNull(got);
        Assert.Equal("Hz", got!.Unit);
    }

    [Fact]
    public async Task ReadWaveform_ParsesHeaderAndData()
    {
        var conn = new RecordingConnection("Rohde&Schwarz,RTB2004,SN1,1.0")
            .When("CHAN1:DATA:HEAD?", "-0.005,0.005,5,1")
            .When("CHAN1:DATA?", "0.1,0.2,0.3,0.4,0.5");
        var driver = new RTB2004Driver();
        await driver.ConnectAsync(conn);

        var (voltages, xStart, xInc) = await driver.ReadWaveformAsync(1);

        Assert.Equal(5, voltages.Length);
        Assert.Equal(-0.005, xStart, 6);
        Assert.Equal((0.005 - -0.005) / 4, xInc, 9);
        Assert.Contains("FORM ASC", conn.Written);
    }

    [Fact]
    public async Task TakeScreenshot_ParsesIeeeBinaryBlock()
    {
        // IEEE block: "#3005" + 5 data bytes
        var payload = new byte[] { (byte)'#', (byte)'3', (byte)'0', (byte)'0', (byte)'5', 1, 2, 3, 4, 5 };
        var conn = new RecordingConnection("Rohde&Schwarz,RTB2004,SN1,1.0").WithRaw(payload);
        var driver = new RTB2004Driver();
        await driver.ConnectAsync(conn);

        byte[] img = await driver.TakeScreenshotAsync();

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, img);
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsNineBlocks()
    {
        var driver = new RTB2004Driver();
        Assert.Equal(9, driver.GetAvailableBlocks().Count());
    }
}
