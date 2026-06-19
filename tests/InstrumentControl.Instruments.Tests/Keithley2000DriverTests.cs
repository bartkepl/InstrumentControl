using Keithley2000;

namespace InstrumentControl.Instruments.Tests;

public class Keithley2000DriverTests
{
    private static async Task<(Keithley2000Driver Driver, RecordingConnection Conn)> ConnectAsync(
        string read = "+1.00000000E+00")
    {
        var conn = new RecordingConnection("Keithley,2000,SN1,1.0").When("READ?", read);
        var driver = new Keithley2000Driver();
        await driver.ConnectAsync(conn);
        return (driver, conn);
    }

    [Fact]
    public void Identity_IsCorrect()
    {
        var driver = new Keithley2000Driver();
        Assert.Equal("Keithley2000", driver.DriverName);
        Assert.Equal("Keithley", driver.Manufacturer);
        Assert.Equal("2000", driver.Model);
    }

    [Fact]
    public async Task MeasureDCV_SendsConfAndNplc()
    {
        var (driver, conn) = await ConnectAsync("+5.0E+00");
        double v = await driver.MeasureDCV("DEF", 1.0);
        Assert.Equal(5.0, v, 6);
        Assert.Contains("CONF:VOLT:DC DEF,DEF", conn.Written);
        Assert.Contains("SENS:VOLT:DC:NPLC 1", conn.Written);
    }

    [Fact]
    public async Task MeasureTemperature_UsesThermocoupleConf()
    {
        var (driver, conn) = await ConnectAsync("+25.5E+00");
        await driver.MeasureTemperature("K", 1.0);
        Assert.Contains("CONF:TEMP TC,K", conn.Written);
        Assert.Contains("SENS:TEMP:NPLC 1", conn.Written);
    }

    [Fact]
    public async Task MeasureFrequencyAndPeriod_SendNplc()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureFrequency("AUTO", 1.0);
        await driver.MeasurePeriod("AUTO", 1.0);
        Assert.Contains("CONF:FREQ", conn.Written);
        Assert.Contains("SENS:FREQ:NPLC 1", conn.Written);
        Assert.Contains("CONF:PER", conn.Written);
        Assert.Contains("SENS:PER:NPLC 1", conn.Written);
    }

    [Fact]
    public async Task MeasureResistance4W_UsesFres()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.MeasureResistance4W("DEF", 1.0);
        Assert.Contains("CONF:FRES DEF,DEF", conn.Written);
    }

    [Fact]
    public async Task SetMathMode_Keithley_UsesCalc2()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetMathMode("MXB");
        Assert.Contains("CALC2:FORM MXB", conn.Written);
        Assert.Contains("CALC2:STAT ON", conn.Written);

        await driver.SetMathMode("OFF");
        Assert.Contains("CALC2:STAT OFF", conn.Written);
    }

    [Fact]
    public async Task SetDisplayEnabled_UsesDispEnab()
    {
        var (driver, conn) = await ConnectAsync();
        await driver.SetDisplayEnabled(true);
        await driver.SetDisplayEnabled(false);
        Assert.Contains("DISP:ENAB ON", conn.Written);
        Assert.Contains("DISP:ENAB OFF", conn.Written);
    }

    [Fact]
    public async Task LimitTest_UsesCalc2()
    {
        var conn = new RecordingConnection("Keithley,2000,SN1,1.0").When("CALC2:LIM:FAIL?", "0");
        var driver = new Keithley2000Driver();
        await driver.ConnectAsync(conn);

        await driver.SetLimitTestAsync(0.5, 1.5);
        bool fail = await driver.GetLimitFailAsync();

        Assert.Contains("CALC2:LIM:LOW 0.5", conn.Written);
        Assert.Contains("CALC2:LIM:UPP 1.5", conn.Written);
        Assert.Contains("CALC2:LIM:STAT ON", conn.Written);
        Assert.False(fail);
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsNineBlocks()
    {
        var driver = new Keithley2000Driver();
        Assert.Equal(9, driver.GetAvailableBlocks().Count());
    }
}
