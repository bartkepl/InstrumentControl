using HP34401A;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Instruments.Tests;

public class HP34401ADriverTests
{
    private static async Task<(HP34401ADriver Driver, RecordingConnection Conn)> ConnectAsync(
        string read = "+3.30000000E+00")
    {
        var conn = new RecordingConnection("Hewlett-Packard,34401A,SN1,1.0").When("READ?", read);
        var driver = new HP34401ADriver();
        await driver.ConnectAsync(conn);
        return (driver, conn);
    }

    [Fact]
    public async Task Identity_IsCorrect()
    {
        var driver = new HP34401ADriver();
        Assert.Equal("HP34401A", driver.DriverName);
        Assert.Equal("Hewlett-Packard", driver.Manufacturer);
        Assert.Equal("34401A", driver.Model);
        Assert.NotEmpty(driver.SupportedResourcePatterns);
    }

    [Fact]
    public async Task MeasureDCV_SendsConfAndNplc_AndReturnsValue()
    {
        var (driver, conn) = await ConnectAsync("+3.30000000E+00");
        double v = await driver.MeasureDCV("DEF", 1.0);

        Assert.Equal(3.3, v, 6);
        Assert.Contains("CONF:VOLT:DC DEF,DEF", conn.Written);
        Assert.Contains("SENS:VOLT:DC:NPLC 1", conn.Written);
        Assert.Contains("READ?", conn.Written);
    }

    [Fact]
    public async Task MeasureDCV_RaisesMeasurementEvent()
    {
        var (driver, _) = await ConnectAsync("+1.50000000E+00");
        MeasurementResult? got = null;
        driver.MeasurementReceived += (_, r) => got = r;

        await driver.MeasureDCV();

        Assert.NotNull(got);
        Assert.Equal("DCV", got!.Function);
        Assert.Equal("V", got.Unit);
    }

    [Fact]
    public async Task MeasureACV_UsesAcConf()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureACV("10", 0.2);
        Assert.Contains("CONF:VOLT:AC 10,DEF", conn.Written);
        Assert.Contains("SENS:VOLT:AC:NPLC 0.2", conn.Written);
    }

    [Fact]
    public async Task MeasureResistance2W_And4W_UseDifferentConf()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureResistance2W("1E3", 1.0);
        await driver.MeasureResistance4W("1E3", 1.0);
        Assert.Contains("CONF:RES 1E3,DEF", conn.Written);
        Assert.Contains("CONF:FRES 1E3,DEF", conn.Written);
    }

    [Fact]
    public async Task MeasureCurrent_DcAndAc()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureDCI("1", 1.0);
        await driver.MeasureACI("1", 1.0);
        Assert.Contains("CONF:CURR:DC 1,DEF", conn.Written);
        Assert.Contains("CONF:CURR:AC 1,DEF", conn.Written);
    }

    [Fact]
    public async Task MeasureFrequencyPeriodDiodeContinuity_UseCorrectConf()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureFrequency();
        await driver.MeasurePeriod();
        await driver.MeasureDiode();
        await driver.MeasureContinuity();
        Assert.Contains("CONF:FREQ", conn.Written);
        Assert.Contains("CONF:PER", conn.Written);
        Assert.Contains("CONF:DIOD", conn.Written);
        Assert.Contains("CONF:CONT", conn.Written);
    }

    [Fact]
    public async Task ConfigCache_SuppressesRepeatedConf()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureDCV("DEF", 1.0);
        await driver.MeasureDCV("DEF", 1.0); // identical -> no second CONF

        int confCount = conn.Written.Count(c => c == "CONF:VOLT:DC DEF,DEF");
        Assert.Equal(1, confCount);
        // but READ? happens every time
        Assert.Equal(2, conn.Written.Count(c => c == "READ?"));
    }

    [Fact]
    public async Task InvalidateConfigCache_ForcesReconfigure()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureDCV("DEF", 1.0);
        driver.InvalidateConfigCache();
        await driver.MeasureDCV("DEF", 1.0);
        Assert.Equal(2, conn.Written.Count(c => c == "CONF:VOLT:DC DEF,DEF"));
    }

    [Theory]
    [InlineData("OFF", "CALC:STAT OFF")]
    [InlineData("DB", "CALC:FUNC DB")]
    public async Task SetMathMode_SendsExpectedCommand(string mode, string expected)
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetMathMode(mode);
        Assert.Contains(expected, conn.Written);
    }

    [Fact]
    public async Task SetMathMode_MinMax_UsesAverage()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetMathMode("MINMAX");
        Assert.Contains("CALC:FUNC AVER", conn.Written);
        Assert.Contains("CALC:STAT ON", conn.Written);
    }

    [Fact]
    public async Task SetAutoZero_OnOff()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetAutoZero(true);
        await driver.SetAutoZero(false);
        Assert.Contains("ZERO:AUTO ON", conn.Written);
        Assert.Contains("ZERO:AUTO OFF", conn.Written);
    }

    [Fact]
    public async Task LimitTest_SetEnableQueryDisable()
    {
        var conn = new RecordingConnection("Hewlett-Packard,34401A,SN1,1.0").When("CALC:LIM:FAIL?", "1");
        var driver = new HP34401ADriver();
        await driver.ConnectAsync(conn);

        await driver.SetLimitTestAsync(1.0, 2.0);
        bool fail = await driver.GetLimitFailAsync();
        await driver.DisableLimitTestAsync();

        Assert.Contains("CALC:LIM:LOW 1", conn.Written);
        Assert.Contains("CALC:LIM:UPP 2", conn.Written);
        Assert.Contains("CALC:LIM:STAT ON", conn.Written);
        Assert.Contains("CALC:LIM:STAT OFF", conn.Written);
        Assert.True(fail);
    }

    [Fact]
    public async Task Display_TextClearEnable()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.DisplayText("HELLO");
        await driver.ClearDisplay();
        await driver.SetDisplayEnabled(false);
        Assert.Contains("DISP:TEXT \"HELLO\"", conn.Written);
        Assert.Contains("DISP:TEXT:CLE", conn.Written);
        Assert.Contains("DISP OFF", conn.Written);
    }

    [Fact]
    public async Task BurstMeasure_ParsesCommaSeparatedResponse()
    {
        var conn = new RecordingConnection("Hewlett-Packard,34401A,SN1,1.0")
            .When("FETCH?", "+1.0E+00,+2.0E+00,+3.0E+00");
        var driver = new HP34401ADriver();
        await driver.ConnectAsync(conn);

        double[] vals = await driver.BurstMeasureAsync(3);

        Assert.Equal(3, vals.Length);
        Assert.Contains("SAMP:COUN 3", conn.Written);
        Assert.Contains("INIT", conn.Written);
        Assert.Contains("SAMP:COUN 1", conn.Written); // restored
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsEightBlocks()
    {
        var driver = new HP34401ADriver();
        Assert.Equal(8, driver.GetAvailableBlocks().Count());
    }
}
