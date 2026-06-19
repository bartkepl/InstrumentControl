using Agilent34970A;
using Agilent34970A.Cards;

namespace InstrumentControl.Instruments.Tests;

public class Agilent34970ADriverTests
{
    private static async Task<Agilent34970ADriver> ConnectAsync(RecordingConnection conn)
    {
        var driver = new Agilent34970ADriver();
        await driver.ConnectAsync(conn);
        return driver;
    }

    [Fact]
    public void Identity_AndSlots()
    {
        var driver = new Agilent34970ADriver();
        Assert.Equal("Agilent34970A", driver.DriverName);
        Assert.Equal(new[] { 100, 200, 300 }, Agilent34970ADriver.Slots);
    }

    [Fact]
    public void SetCard_AddsAndRemoves()
    {
        var driver = new Agilent34970ADriver();
        driver.SetCard(100, "34901A");
        Assert.IsType<Card34901A>(driver.Cards[100]);

        driver.SetCard(100, "0");      // empty -> remove
        Assert.False(driver.Cards.ContainsKey(100));
    }

    [Fact]
    public void AddCardHelpers_PopulateDictionary()
    {
        var driver = new Agilent34970ADriver();
        driver.AddCard34901A(100);
        driver.AddCard34907A(200);
        Assert.IsType<Card34901A>(driver.Cards[100]);
        Assert.IsType<Card34907A>(driver.Cards[200]);
    }

    [Fact]
    public async Task DetectCardsAsync_ParsesCtypeResponses()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0")
            .When("SYST:CTYP? 100", "Agilent Technologies,34901A,0,1.0")
            .When("SYST:CTYP? 200", "Agilent Technologies,34907A,0,1.0")
            .When("SYST:CTYP? 300", "0,0,0,0");
        var driver = await ConnectAsync(conn);

        var detected = await driver.DetectCardsAsync();

        Assert.Equal("34901A", detected[100]);
        Assert.Equal("34907A", detected[200]);
        Assert.Equal("", detected[300]);
        Assert.IsType<Card34901A>(driver.Cards[100]);
        Assert.IsType<Card34907A>(driver.Cards[200]);
        Assert.False(driver.Cards.ContainsKey(300));
    }

    [Fact]
    public async Task ScanChannelList_ConfiguresAndReadsValues()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0")
            .When("READ?", "+1.0E+00,+2.0E+00");
        var driver = await ConnectAsync(conn);
        driver.AddCard34901A(100);

        var results = await driver.ScanChannelListAsync("101,102");

        Assert.Equal(2, results.Count);
        Assert.Equal(1.0, results[0].Value, 6);
        Assert.Equal(2.0, results[1].Value, 6);
        Assert.Contains("CONF:VOLT:DC DEF,DEF,(@101)", conn.Written);
        Assert.Contains("CONF:VOLT:DC DEF,DEF,(@102)", conn.Written);
        Assert.Contains("ROUT:SCAN (@101,102)", conn.Written);
        Assert.Contains("TRIG:SOUR IMM", conn.Written);
        Assert.Contains("TRIG:COUN 1", conn.Written);
    }

    [Fact]
    public async Task ScanAsync_EmptyChannels_ReturnsEmpty()
    {
        var driver = await ConnectAsync(new RecordingConnection("Agilent Technologies,34970A,SN1,1.0"));
        var results = await driver.ScanAsync(Array.Empty<ChannelMeasurement>());
        Assert.Empty(results);
    }

    [Fact]
    public async Task ScanAsync_FourWirePairConflict_Throws()
    {
        var driver = await ConnectAsync(new RecordingConnection("Agilent Technologies,34970A,SN1,1.0"));
        var channels = new[]
        {
            new ChannelMeasurement(105, MuxFunction.OHM4W),  // reserves 115
            new ChannelMeasurement(115, MuxFunction.VDC),    // conflict
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ScanAsync(channels));
    }

    [Fact]
    public async Task ScanSpecAsync_ParsesMixedSpec()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0")
            .When("READ?", "+1.0,+2.0,+3.0");
        var driver = await ConnectAsync(conn);

        var results = await driver.ScanSpecAsync("101=VDC; 102=VDC@10; 103=TC:K");

        Assert.Equal(3, results.Count);
        Assert.Contains("CONF:VOLT:DC DEF,DEF,(@101)", conn.Written);
        Assert.Contains("CONF:TEMP TC,K,(@103)", conn.Written);
    }

    [Fact]
    public void ParseChannelSpec_HandlesRangeAndFunctions()
    {
        var driver = new Agilent34970ADriver();
        var parsed = driver.ParseChannelSpec("101=VDC; 102=VDC@10; 103=RTD4W:85; 104=TC:K");

        Assert.Equal(4, parsed.Count);
        Assert.Equal(MuxFunction.VDC, parsed[0].Function);
        Assert.Equal("10", parsed[1].Range);
        Assert.Equal(MuxFunction.TEMP_RTD4W, parsed[2].Function);
        Assert.Equal(MuxFunction.TEMP_TC, parsed[3].Function);
        Assert.Equal(TcType.K, parsed[3].TcType);
    }

    [Fact]
    public void ParseChannelSpec_EmptyInput_ReturnsEmpty()
    {
        var driver = new Agilent34970ADriver();
        Assert.Empty(driver.ParseChannelSpec(""));
    }

    [Fact]
    public async Task SetDac_RequiresCard_SendsSourCommand()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0")
            .When("SYST:ERR?", "+0,\"No error\"");
        var driver = await ConnectAsync(conn);
        driver.AddCard34907A(200);

        await driver.SetDacAsync(200, 1, 5.0);

        Assert.Contains("SOUR:VOLT 5,(@204)", conn.Written);
    }

    [Fact]
    public async Task SetDac_WithoutCard_Throws()
    {
        var driver = await ConnectAsync(new RecordingConnection("Agilent Technologies,34970A,SN1,1.0"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.SetDacAsync(200, 1, 5.0));
    }

    [Fact]
    public async Task DigitalOutput_AndInput()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0")
            .When("SYST:ERR?", "+0,\"No error\"")
            .When("SENS:DIG:DATA:BYTE? (@201)", "170");
        var driver = await ConnectAsync(conn);
        driver.AddCard34907A(200);

        await driver.SetDigitalOutputAsync(200, 0xFF);
        byte read = await driver.ReadDigitalInputAsync(200);

        Assert.Contains("SOUR:DIG:DATA:BYTE 255,(@201)", conn.Written);
        Assert.Equal(170, read);
    }

    [Fact]
    public async Task ReadTotalizer_ParsesValue()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0")
            .When("MEAS:TOT? READ,(@203)", "+42");
        var driver = await ConnectAsync(conn);
        driver.AddCard34907A(200);

        Assert.Equal(42, await driver.ReadTotalizerAsync(200), 6);
    }

    [Fact]
    public async Task ResetTotalizer_SendsClearCommand()
    {
        var conn = new RecordingConnection("Agilent Technologies,34970A,SN1,1.0");
        var driver = await ConnectAsync(conn);
        driver.AddCard34907A(200);

        await driver.ResetTotalizerAsync(200);
        Assert.Contains("SENS:TOT:CLE:IMM (@203)", conn.Written);
    }

    [Fact]
    public async Task GetAvailableBlocks_ReturnsBlocks()
    {
        var driver = new Agilent34970ADriver();
        Assert.NotEmpty(driver.GetAvailableBlocks());
    }
}
