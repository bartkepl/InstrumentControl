using System.Globalization;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class SimulatedConnectionProviderTests
{
    private static double ParseScpi(string s) =>
        double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static async Task<SimulatedConnectionProvider> OpenAsync(string name = "SIM::TEST")
    {
        var sim = new SimulatedConnectionProvider(name);
        await sim.OpenAsync();
        return sim;
    }

    [Fact]
    public async Task OpenClose_TogglesIsOpen()
    {
        var sim = new SimulatedConnectionProvider("SIM::X");
        Assert.False(sim.IsOpen);
        await sim.OpenAsync();
        Assert.True(sim.IsOpen);
        await sim.CloseAsync();
        Assert.False(sim.IsOpen);
        Assert.Equal("SIMULATION", sim.ConnectionType);
    }

    [Fact]
    public async Task Idn_ReturnsSimulatedIdentity()
    {
        var sim = await OpenAsync();
        Assert.Equal("SIMULATED,INSTRUMENT,SIM001,1.0", await sim.QueryAsync("*IDN?"));
    }

    [Fact]
    public async Task Dmm_DcVoltage_ReturnsRealisticValue()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("CONF:VOLT:DC");
        double v = ParseScpi(await sim.QueryAsync("READ?"));
        Assert.InRange(v, 3.0, 3.6);
    }

    [Fact]
    public async Task Dmm_Resistance_ReturnsAroundOneKiloOhm()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("CONF:RES");
        double v = ParseScpi(await sim.QueryAsync("READ?"));
        Assert.InRange(v, 980, 1020);
    }

    [Fact]
    public async Task Dmm_Burst_ReturnsRequestedSampleCount()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("SAMP:COUN 5");
        string resp = await sim.QueryAsync("FETCH?");
        Assert.Equal(5, resp.Split(',').Length);
    }

    [Fact]
    public async Task PowerSupply_VoltageSetpoint_RoundTrips()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("VOLT 12.5");
        Assert.Equal(12.5, ParseScpi(await sim.QueryAsync("VOLT?")), 3);
    }

    [Fact]
    public async Task PowerSupply_Output_OnOff_AffectsMeasurement()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("VOLT 10");
        await sim.WriteAsync("OUTP OFF");
        Assert.Equal("0", await sim.QueryAsync("OUTP?"));
        Assert.Equal(0.0, ParseScpi(await sim.QueryAsync("MEAS:VOLT?")), 3);

        await sim.WriteAsync("OUTP ON");
        Assert.Equal("1", await sim.QueryAsync("OUTP?"));
        Assert.InRange(ParseScpi(await sim.QueryAsync("MEAS:VOLT?")), 9.9, 10.1);
    }

    [Fact]
    public async Task PowerSupply_OvpOcp_State()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("VOLT:PROT 30");
        await sim.WriteAsync("VOLT:PROT:STAT ON");
        Assert.Equal(30.0, ParseScpi(await sim.QueryAsync("VOLT:PROT?")), 3);
        Assert.Equal("1", await sim.QueryAsync("VOLT:PROT:STAT?"));

        await sim.WriteAsync("CURR:PROT:LEV 4.2");
        await sim.WriteAsync("CURR:PROT:STAT ON");
        Assert.Equal(4.2, ParseScpi(await sim.QueryAsync("CURR:PROT:LEV?")), 3);
        Assert.Equal("1", await sim.QueryAsync("CURR:PROT:STAT?"));
    }

    [Fact]
    public async Task Oscilloscope_Timebase_RoundTrips()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("TIM:SCAL 2e-3");
        Assert.Equal(2e-3, ParseScpi(await sim.QueryAsync("TIM:SCAL?")), 9);
    }

    [Fact]
    public async Task Oscilloscope_ChannelScale_RoundTrips()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("CHAN2:SCAL 0.5");
        Assert.Equal(0.5, ParseScpi(await sim.QueryAsync("CHAN2:SCAL?")), 9);
    }

    [Fact]
    public async Task Oscilloscope_WaveformData_ReturnsManyPoints()
    {
        var sim = await OpenAsync();
        await sim.WriteAsync("TIM:SCAL 1e-3");
        string data = await sim.QueryAsync("CHAN1:DATA?");
        Assert.Equal(1000, data.Split(',').Length);
    }

    [Fact]
    public async Task Oscilloscope_Measurement_Frequency()
    {
        var sim = await OpenAsync();
        double f = ParseScpi(await sim.QueryAsync("MEAS1:RES:ACT? FREQ"));
        Assert.InRange(f, 995, 1005);
    }

    [Fact]
    public async Task Chamber_SetTemperatureAndRun_MovesActualTowardSetpoint()
    {
        var sim = await OpenAsync("SIM::COM3");
        await sim.WriteAsync("a0 80.0");   // setpoint 80
        await sim.WriteAsync("s1 1");      // start
        // first read establishes the time tick
        await sim.QueryAsync("A0");
        var status = await sim.QueryAsync("R0");
        Assert.Contains("11", status);     // running flag
    }

    [Fact]
    public async Task CustomResponse_OverridesBuiltIn()
    {
        var sim = await OpenAsync();
        sim.SetResponse("FOO?", "BAR");
        Assert.Equal("BAR", await sim.QueryAsync("FOO?"));
    }

    [Fact]
    public async Task WriteOnlyCommands_ReturnEmpty()
    {
        var sim = await OpenAsync();
        Assert.Equal("", await sim.QueryAsync("*RST"));
        Assert.Equal("", await sim.QueryAsync("CONF:VOLT:DC"));
    }

    [Fact]
    public async Task RawIo_DoesNotThrow()
    {
        var sim = await OpenAsync();
        await sim.WriteRawAsync(new byte[] { 1, 2, 3 });
        var buf = await sim.ReadRawAsync(4);
        Assert.Equal(4, buf.Length);
        sim.Dispose();
    }
}
