using ItechIT6922B;

namespace InstrumentControl.Instruments.Tests;

public class ItechIT6922BDriverTests
{
    private static async Task<(ItechIT6922BDriver Driver, RecordingConnection Conn)> ConnectAsync()
    {
        var conn = new RecordingConnection("ITECH,IT6922B,SN1,1.0");
        var driver = new ItechIT6922BDriver();
        await driver.ConnectAsync(conn);
        return (driver, conn);
    }

    [Fact]
    public void Identity_IsCorrect()
    {
        var driver = new ItechIT6922BDriver();
        Assert.Equal("ItechIT6922B", driver.DriverName);
        Assert.Equal("ITECH", driver.Manufacturer);
    }

    [Fact]
    public async Task SetVoltage_UsesInvariantCulture()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetVoltageAsync(12.5);
        Assert.Contains("VOLT 12.5", conn.Written);
    }

    [Fact]
    public async Task SetCurrentLimit_SendsCurr()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetCurrentLimitAsync(1.25);
        Assert.Contains("CURR 1.25", conn.Written);
    }

    [Fact]
    public async Task SetOutputEnabled_OnOff()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetOutputEnabledAsync(true);
        await driver.SetOutputEnabledAsync(false);
        Assert.Contains("OUTP ON", conn.Written);
        Assert.Contains("OUTP OFF", conn.Written);
    }

    [Fact]
    public async Task GetOutputEnabled_ParsesOnOrOne()
    {
        var conn = new RecordingConnection("ITECH,IT6922B,SN1,1.0").When("OUTP?", "1");
        var driver = new ItechIT6922BDriver();
        await driver.ConnectAsync(conn);
        Assert.True(await driver.GetOutputEnabledAsync());
    }

    [Fact]
    public async Task MeasureVoltageCurrentPower_SendMeasCommands()
    {
        var conn = new RecordingConnection("ITECH,IT6922B,SN1,1.0")
            .When("MEAS:VOLT?", "+12.0E+00")
            .When("MEAS:CURR?", "+0.5E+00")
            .When("MEAS:POW?", "+6.0E+00");
        var driver = new ItechIT6922BDriver();
        await driver.ConnectAsync(conn);

        var (v, i, p) = await driver.MeasureAllAsync();

        Assert.Equal(12.0, v, 3);
        Assert.Equal(0.5, i, 3);
        Assert.Equal(6.0, p, 3);
        Assert.Contains("MEAS:VOLT?", conn.Written);
        Assert.Contains("MEAS:CURR?", conn.Written);
        Assert.Contains("MEAS:POW?", conn.Written);
    }

    [Fact]
    public async Task Ovp_SetLevelAndEnable()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetOvpLevelAsync(30.0);
        await driver.SetOvpEnabledAsync(true);
        Assert.Contains("VOLT:PROT 30", conn.Written);
        Assert.Contains("VOLT:PROT:STAT ON", conn.Written);
    }

    [Fact]
    public async Task Ocp_SetLevelAndEnable()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetOcpLevelAsync(4.5);
        await driver.SetOcpEnabledAsync(true);
        Assert.Contains("CURR:PROT:LEV 4.5", conn.Written);
        Assert.Contains("CURR:PROT:STAT ON", conn.Written);
    }

    [Fact]
    public async Task ClearProtection_SendsProtClear()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.ClearProtectionAsync();
        Assert.Contains("OUTP:PROT:CLE", conn.Written);
    }

    [Theory]
    [InlineData("1", "CV")]
    [InlineData("2", "CC")]
    [InlineData("0", "UNREG")]
    public async Task GetOperatingMode_DecodesStatusBits(string cond, string expected)
    {
        var conn = new RecordingConnection("ITECH,IT6922B,SN1,1.0").When("STAT:OPER:COND?", cond);
        var driver = new ItechIT6922BDriver();
        await driver.ConnectAsync(conn);
        Assert.Equal(expected, await driver.GetOperatingModeAsync());
    }

    [Fact]
    public async Task ReadSetpoints_ReturnsVoltageCurrentOutput()
    {
        var conn = new RecordingConnection("ITECH,IT6922B,SN1,1.0")
            .When("VOLT?", "+10.0E+00")
            .When("CURR?", "+2.0E+00")
            .When("OUTP?", "1");
        var driver = new ItechIT6922BDriver();
        await driver.ConnectAsync(conn);

        var (v, i, on) = await driver.ReadSetpointsAsync();
        Assert.Equal(10.0, v, 3);
        Assert.Equal(2.0, i, 3);
        Assert.True(on);
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsEightBlocks()
    {
        var driver = new ItechIT6922BDriver();
        Assert.Equal(8, driver.GetAvailableBlocks().Count());
    }
}
