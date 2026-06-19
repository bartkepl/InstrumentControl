using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

public class VisaServiceTests
{
    [Fact]
    public void Initialize_DoesNotThrow_WhenVisaDllMissing()
    {
        var svc = new VisaService();
        svc.Initialize(); // visa64.dll absent on CI -> simulation mode, must not throw
        svc.Dispose();
    }

    [Fact]
    public void OpenSimulated_ReturnsSimulationProvider()
    {
        var svc = new VisaService();
        var conn = svc.OpenSimulated("SIM::FOO");
        Assert.Equal("SIMULATION", conn.ConnectionType);
        Assert.Equal("SIM::FOO", conn.ResourceName);
    }

    [Fact]
    public void OpenVisaSession_WithSimPrefix_RoutesToSimulation()
    {
        var svc = new VisaService();
        // "SIM::" prefix forces the simulated path regardless of VISA availability
        var conn = svc.OpenVisaSession("SIM::GPIB0::22::INSTR");
        Assert.Equal("SIMULATION", conn.ConnectionType);
    }

    [Fact]
    public void OpenComSession_BuildsSerialProvider()
    {
        var svc = new VisaService();
        var conn = svc.OpenComSession("COM99");
        Assert.Equal("COM", conn.ConnectionType);
        Assert.Equal("COM99", conn.ResourceName);
        Assert.False(conn.IsOpen); // not opened yet
    }

    [Fact]
    public void GetComPorts_ReturnsNonNullArray()
    {
        var svc = new VisaService();
        Assert.NotNull(svc.GetComPorts());
    }

    [Fact]
    public void FindResources_InSimulationMode_ReturnsSimEntries()
    {
        var svc = new VisaService();
        svc.Initialize();
        var resources = svc.FindResources();
        Assert.NotNull(resources);
        if (svc.IsSimulationMode)
        {
            Assert.NotEmpty(resources);
            Assert.All(resources, r => Assert.Contains("SIM::", r));
        }
    }
}
