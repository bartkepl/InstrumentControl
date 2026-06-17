using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class InstrumentDriverBaseTests
{
    // Minimal in-memory connection provider that records open/close calls
    // and returns a canned *IDN? response.
    private sealed class FakeConnection : IConnectionProvider
    {
        private readonly string _idn;
        public FakeConnection(string idn) => _idn = idn;

        public string ResourceName => "FAKE::INSTR";
        public string ConnectionType => "FAKE";
        public bool IsOpen { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }

        public Task OpenAsync() { IsOpen = true; OpenCount++; return Task.CompletedTask; }
        public Task CloseAsync() { IsOpen = false; CloseCount++; return Task.CompletedTask; }
        public Task<string> QueryAsync(string command, int timeoutMs = 5000) => Task.FromResult(_idn);
        public Task<string> ReadAsync(int timeoutMs = 5000) => Task.FromResult(_idn);
        public Task WriteAsync(string command) => Task.CompletedTask;
        public Task WriteRawAsync(byte[] data) => Task.CompletedTask;
        public Task<byte[]> ReadRawAsync(int count) => Task.FromResult(new byte[count]);
        public void Dispose() { }
    }

    private sealed class TestDriver : InstrumentDriverBase
    {
        public override string DriverName => "TestDriver";
        public override string Manufacturer => "TestCo";
        public override string Model => "T1000";
        public override string Description => "test";
        public override string[] SupportedResourcePatterns => new[] { "FAKE?*::INSTR" };
        public override FrameworkElement CreateFrontPanel() => null!;
        public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => Array.Empty<ISequenceBlock>();
    }

    [Fact]
    public async Task ConnectAsync_FullIdn_ParsesSerialAndFirmware()
    {
        var driver = new TestDriver();
        await driver.ConnectAsync(new FakeConnection("TestCo,T1000,SN12345,1.2.3"));

        Assert.True(driver.IsConnected);
        Assert.Equal("SN12345", driver.InstrumentInfo!.SerialNumber);
        Assert.Equal("1.2.3", driver.InstrumentInfo!.FirmwareVersion);
    }

    [Fact]
    public async Task ConnectAsync_ShortIdn_DoesNotThrowAndLeavesFieldsEmpty()
    {
        var driver = new TestDriver();
        await driver.ConnectAsync(new FakeConnection("TestCo,T1000"));

        Assert.True(driver.IsConnected);
        Assert.Equal(string.Empty, driver.InstrumentInfo!.SerialNumber);
        Assert.Equal(string.Empty, driver.InstrumentInfo!.FirmwareVersion);
    }

    [Fact]
    public async Task DisconnectThenReconnect_RestoresConnection()
    {
        var conn = new FakeConnection("TestCo,T1000,SN1,1.0");
        var driver = new TestDriver();
        await driver.ConnectAsync(conn);
        Assert.True(driver.IsConnected);

        await driver.DisconnectAsync();
        Assert.False(driver.IsConnected);
        Assert.Equal(1, conn.CloseCount);

        await driver.ReconnectAsync();
        Assert.True(driver.IsConnected);
        Assert.True(conn.OpenCount >= 2, "Reconnect should re-open the same connection");
    }
}
