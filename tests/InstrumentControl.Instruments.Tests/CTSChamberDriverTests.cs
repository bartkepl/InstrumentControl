using CTSChamber;

namespace InstrumentControl.Instruments.Tests;

public class CTSChamberDriverTests
{
    // CTS driver takes the SIMULATION fast-path when the connection reports ConnectionType "SIMULATION",
    // using it directly instead of swapping for the real COM-port provider.
    private static RecordingConnection SimConn() =>
        new("CTS,T-40,SN1,1.0") { ConnectionType = "SIMULATION", ResourceName = "COM3" };

    private static async Task<(CTSChamberDriver Driver, RecordingConnection Conn)> ConnectAsync(RecordingConnection? conn = null)
    {
        conn ??= SimConn();
        var driver = new CTSChamberDriver();
        await driver.ConnectAsync(conn);
        return (driver, conn);
    }

    [Fact]
    public void Identity_IsCorrect()
    {
        var driver = new CTSChamberDriver();
        Assert.Equal("CTSChamber", driver.DriverName);
        Assert.Equal("CTS", driver.Manufacturer);
        Assert.Equal(new[] { "COM?*" }, driver.SupportedResourcePatterns);
    }

    [Fact]
    public async Task Connect_SimulationPath_SetsConnectedInfo()
    {
        var (driver, _) = await ConnectAsync();
        Assert.True(driver.IsConnected);
        Assert.Equal("SIMULATION", driver.InstrumentInfo!.ConnectionType);
    }

    [Fact]
    public async Task ReadTemperature_ParsesActualAndSetpoint()
    {
        var conn = SimConn();
        conn.When("A0", "A0 23.50 80.00");
        var (driver, _) = await ConnectAsync(conn);

        var (actual, setpoint) = await driver.ReadTemperatureAsync();
        Assert.Equal(23.5, actual, 3);
        Assert.Equal(80.0, setpoint, 3);
    }

    [Fact]
    public async Task ReadTemperature_InvalidResponse_Throws()
    {
        var conn = SimConn();
        conn.When("A0", "A0 garbage");
        var (driver, _) = await ConnectAsync(conn);
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ReadTemperatureAsync());
    }

    [Fact]
    public async Task SetTemperature_InRange_WritesCommand()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetTemperatureAsync(80.0);
        Assert.Contains("a0 80.0", conn.Written);
    }

    [Theory]
    [InlineData(-100.0)]
    [InlineData(200.0)]
    public async Task SetTemperature_OutOfRange_Throws(double temp)
    {
        var (driver, _) = await ConnectAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => driver.SetTemperatureAsync(temp));
    }

    [Fact]
    public async Task SetRampUpDown_WriteCommands()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetRampUpAsync(3.0);
        await driver.SetRampDownAsync(2.5);
        Assert.Contains("u0 3.0", conn.Written);
        Assert.Contains("d0 2.5", conn.Written);
    }

    [Fact]
    public async Task SetRamp_TooSmall_Throws()
    {
        var (driver, _) = await ConnectAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => driver.SetRampUpAsync(0.0));
    }

    [Fact]
    public async Task ReadRampParams_ParsesFields()
    {
        var conn = SimConn();
        conn.When("R0", "R0 11 3.00 2.00 80.00");
        var (driver, _) = await ConnectAsync(conn);

        var (active, running, up, down, final) = await driver.ReadRampParamsAsync();
        Assert.True(active);
        Assert.True(running);
        Assert.Equal(3.0, up, 3);
        Assert.Equal(2.0, down, 3);
        Assert.Equal(80.0, final, 3);
    }

    [Fact]
    public async Task ChamberStartStopPauseResume_WriteCommands()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.ChamberStartAsync();
        await driver.ChamberStopAsync();
        await driver.ChamberPauseAsync();
        await driver.ChamberResumeAsync();
        Assert.Contains("s1 1", conn.Written);
        Assert.Contains("s1 0", conn.Written);
        Assert.Contains("s3 0", conn.Written);
        Assert.Contains("s3 1", conn.Written);
    }

    [Fact]
    public async Task ReadChamberState_DecodesFlags()
    {
        var conn = SimConn();
        conn.When("O", "O101");
        var (driver, _) = await ConnectAsync(conn);

        var (running, error, paused) = await driver.ReadChamberStateAsync();
        Assert.True(running);
        Assert.False(error);
        Assert.False(paused);
    }

    [Fact]
    public async Task GetIdentification_StripsPrefixAndDelimiters()
    {
        var conn = SimConn();
        conn.When("C", "C 2.10;1.20;001;");
        var (driver, _) = await ConnectAsync(conn);

        string idn = await driver.GetIdentificationAsync();
        Assert.Equal("2.10;1.20;001", idn);
    }

    [Fact]
    public async Task Reset_StopsChamber()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.ResetAsync();
        Assert.Contains("s1 0", conn.Written);
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsSevenBlocks()
    {
        var driver = new CTSChamberDriver();
        Assert.Equal(7, driver.GetAvailableBlocks().Count());
    }
}
