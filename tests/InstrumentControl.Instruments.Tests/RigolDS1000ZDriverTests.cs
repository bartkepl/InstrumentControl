using RigolDS1000Z;

namespace InstrumentControl.Instruments.Tests;

public class RigolDS1000ZDriverTests
{
    private static async Task<(RigolDS1000ZDriver Driver, RecordingConnection Conn)> ConnectAsync()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        return (driver, conn);
    }

    [Fact]
    public void Identity_IsCorrect()
    {
        var driver = new RigolDS1000ZDriver();
        Assert.Equal("RigolDS1000Z", driver.DriverName);
        Assert.Equal("Rigol", driver.Manufacturer);
    }

    [Fact]
    public async Task SetChannelDisplay_UsesLongFormKeyword()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetChannelDisplayAsync(1, true);
        Assert.Contains(":CHANnel1:DISPlay ON", conn.Written);
    }

    [Fact]
    public async Task SetChannelScale_InvariantCulture()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetChannelScaleAsync(2, 0.05);
        Assert.Contains(":CHANnel2:SCALe 0.05", conn.Written);
    }

    [Fact]
    public async Task GetChannelDisplay_ParsesOnOrOne()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0").When(":CHANnel1:DISPlay?", "1");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        Assert.True(await driver.GetChannelDisplayAsync(1));
    }

    [Fact]
    public async Task Timebase_SetAndGet()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0")
            .When(":TIMebase:MAIN:SCALe?", "1E-3");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        await driver.SetTimebaseScaleAsync(1e-3);
        Assert.Contains(":TIMebase:MAIN:SCALe 0.001", conn.Written);
        Assert.Equal(1e-3, await driver.GetTimebaseScaleAsync(), 9);
    }

    [Fact]
    public async Task TriggerEdgeSource_NormalizesChShorthandToLongForm()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetTriggerEdgeSourceAsync("CH3");
        Assert.Contains(":TRIGger:EDGE:SOURce CHANnel3", conn.Written);
    }

    [Fact]
    public async Task TriggerEdgeSource_NonChannelTokenPassesThrough()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetTriggerEdgeSourceAsync("ext");
        Assert.Contains(":TRIGger:EDGE:SOURce EXT", conn.Written);
    }

    [Fact]
    public async Task AcquireType_SetAndGet()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0").When(":ACQuire:TYPE?", "NORM");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        await driver.SetAcquireTypeAsync("aver");
        Assert.Contains(":ACQuire:TYPE AVER", conn.Written);
        Assert.Equal("NORM", await driver.GetAcquireTypeAsync());
    }

    [Fact]
    public async Task AcquireAverages_ParsesIntWithFallback()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0").When(":ACQuire:AVERages?", "16");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        Assert.Equal(16, await driver.GetAcquireAveragesAsync());
    }

    [Fact]
    public async Task AcquisitionControls_SendKeywords()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.RunAsync();
        await driver.StopAsync();
        await driver.SingleAsync();
        await driver.AutoScaleAsync();
        Assert.Contains(":RUN", conn.Written);
        Assert.Contains(":STOP", conn.Written);
        Assert.Contains(":SINGle", conn.Written);
        Assert.Contains(":AUToset", conn.Written);
    }

    [Fact]
    public async Task MeasureVpp_SendsMeasureQuery_AndVoltUnit()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0")
            .When(":MEASure:VPP? CHANnel1", "+2.0E+00");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        InstrumentControl.Core.Models.MeasurementResult? got = null;
        driver.MeasurementReceived += (_, r) => got = r;

        double v = await driver.MeasureVppAsync(1);

        Assert.Equal(2.0, v, 3);
        Assert.NotNull(got);
        Assert.Equal("V", got!.Unit);
    }

    [Fact]
    public async Task MeasureFrequency_HasHzUnit()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0")
            .When(":MEASure:FREQuency? CHANnel2", "+1000.0E+00");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);
        InstrumentControl.Core.Models.MeasurementResult? got = null;
        driver.MeasurementReceived += (_, r) => got = r;

        await driver.MeasureFrequencyAsync(2);
        Assert.Equal("Hz", got!.Unit);
    }

    [Fact]
    public async Task MeasureDelayAndPhase_UseTwoChannelSources()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0")
            .When(":MEASure:DELay? CHANnel1,CHANnel2", "+1.0E-06")
            .When(":MEASure:PHASe? CHANnel1,CHANnel2", "+90.0E+00");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);

        Assert.Equal(1e-6, await driver.MeasureDelayAsync(1, 2), 9);
        Assert.Equal(90.0, await driver.MeasurePhaseAsync(1, 2), 3);
    }

    [Fact]
    public async Task ReadWaveformAscii_ConfiguresSourceModeFormat_AndParses()
    {
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0")
            .When(":WAVeform:DATA?", "0.1,0.2,0.3");
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);

        double[] data = await driver.ReadWaveformAsciiAsync(1);

        Assert.Equal(3, data.Length);
        Assert.Contains(":WAVeform:SOURce CHANnel1", conn.Written);
        Assert.Contains(":WAVeform:MODE NORMAL", conn.Written);
        Assert.Contains(":WAVeform:FORMat ASCII", conn.Written);
    }

    [Fact]
    public async Task SetMathOperation_AndFftWindow()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetMathOperationAsync("add");
        await driver.SetFFTWindowAsync("hann");
        Assert.Contains(":MATH:OPERate ADD", conn.Written);
        Assert.Contains(":MATH:FFT:WINDow HANN", conn.Written);
    }

    [Fact]
    public async Task TakeScreenshot_ParsesIeeeBlock()
    {
        // "#42000" header (4 length digits = "2000") but only 4 data bytes present -> returns remainder
        var payload = new byte[] { (byte)'#', (byte)'1', (byte)'4', 10, 20, 30, 40 };
        var conn = new RecordingConnection("Rigol Technologies,DS1054Z,SN1,1.0").WithRaw(payload);
        var driver = new RigolDS1000ZDriver();
        await driver.ConnectAsync(conn);

        byte[] img = await driver.TakeScreenshotAsync();
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, img);
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsThirteenBlocks()
    {
        var driver = new RigolDS1000ZDriver();
        Assert.Equal(13, driver.GetAvailableBlocks().Count());
    }
}
