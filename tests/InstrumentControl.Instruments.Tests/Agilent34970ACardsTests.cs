using Agilent34970A.Cards;

namespace InstrumentControl.Instruments.Tests;

public class Agilent34970ACardsTests
{
    // ── CardBase ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("34901A", typeof(Card34901A))]
    [InlineData("34907A", typeof(Card34907A))]
    [InlineData("34908A", typeof(GenericCard))]
    public void CardBase_Create_ReturnsCorrectType(string model, Type expected)
    {
        var card = CardBase.Create(100, model);
        Assert.IsType(expected, card);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    public void CardBase_Create_EmptySlot_ReturnsNull(string model)
    {
        Assert.Null(CardBase.Create(100, model));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    public void CardBase_InvalidSlot_Throws(int slot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Card34901A(slot));
    }

    [Fact]
    public void CardBase_AbsChannelAndSlotIndex()
    {
        var card = new Card34901A(200);
        Assert.Equal(203, card.AbsChannel(3));
        Assert.Equal(2, card.SlotIndex);
        Assert.Contains("34901A", card.ToString());
    }

    // ── Card34901A ────────────────────────────────────────────────────────────

    [Fact]
    public void Card34901A_ValidatesNormalChannel()
    {
        var card = new Card34901A(100);
        card.Validate(new ChannelMeasurement(105, MuxFunction.VDC)); // no throw
    }

    [Fact]
    public void Card34901A_CurrentOnlyOn21And22()
    {
        var card = new Card34901A(100);
        card.Validate(new ChannelMeasurement(121, MuxFunction.CURR_DC)); // ok
        Assert.Throws<InvalidOperationException>(
            () => card.Validate(new ChannelMeasurement(105, MuxFunction.CURR_DC)));
    }

    [Fact]
    public void Card34901A_NonCurrentNotAllowedOnCurrentChannels()
    {
        var card = new Card34901A(100);
        Assert.Throws<InvalidOperationException>(
            () => card.Validate(new ChannelMeasurement(121, MuxFunction.VDC)));
    }

    [Fact]
    public void Card34901A_FourWireRequiresLowChannel()
    {
        var card = new Card34901A(100);
        card.Validate(new ChannelMeasurement(105, MuxFunction.OHM4W)); // ok (1..10)
        Assert.Throws<InvalidOperationException>(
            () => card.Validate(new ChannelMeasurement(115, MuxFunction.OHM4W)));
    }

    [Fact]
    public void Card34901A_OutOfRangeChannel_Throws()
    {
        var card = new Card34901A(100);
        Assert.Throws<InvalidOperationException>(
            () => card.Validate(new ChannelMeasurement(130, MuxFunction.VDC)));
    }

    [Fact]
    public void Card34901A_PairedSenseChannel()
    {
        var card = new Card34901A(100);
        Assert.Equal(115, card.PairedSenseChannel(105));
    }

    // ── Card34907A ────────────────────────────────────────────────────────────

    [Fact]
    public void Card34907A_ChannelMapping()
    {
        var card = new Card34907A(200);
        Assert.Equal(201, card.DioChannel(1));
        Assert.Equal(202, card.DioChannel(2));
        Assert.Equal(203, card.TotalizerChannel);
        Assert.Equal(204, card.DacChannel(1));
        Assert.Equal(205, card.DacChannel(2));
    }

    [Fact]
    public void Card34907A_BuildSetDac_ValidCommand()
    {
        var card = new Card34907A(200);
        Assert.Equal("SOUR:VOLT 5,(@204)", card.BuildSetDacCommand(1, 5.0));
        Assert.Equal("SOUR:VOLT -3.5,(@205)", card.BuildSetDacCommand(2, -3.5));
    }

    [Fact]
    public void Card34907A_BuildSetDac_InvalidDac_Throws()
    {
        var card = new Card34907A(200);
        Assert.Throws<ArgumentOutOfRangeException>(() => card.BuildSetDacCommand(3, 1.0));
    }

    [Fact]
    public void Card34907A_BuildSetDac_VoltageOutOfRange_Throws()
    {
        var card = new Card34907A(200);
        Assert.Throws<ArgumentOutOfRangeException>(() => card.BuildSetDacCommand(1, 20.0));
    }

    [Fact]
    public void Card34907A_DigitalAndTotalizerCommands()
    {
        var card = new Card34907A(300);
        Assert.Equal("SOUR:DIG:DATA:BYTE 255,(@301)", card.BuildDigitalWriteCommand(1, 255));
        Assert.Equal("SENS:DIG:DATA:BYTE? (@302)", card.BuildDigitalReadCommand(2));
        Assert.Equal("MEAS:TOT? READ,(@303)", card.BuildTotalizerReadCommand());
        Assert.Equal("SENS:TOT:CLE:IMM (@303)", card.BuildTotalizerResetCommand());
        Assert.Equal("SOUR:VOLT? (@304)", card.BuildReadDacCommand(1));
    }

    // ── ChannelMeasurement ────────────────────────────────────────────────────

    [Theory]
    [InlineData(MuxFunction.VDC, "CONF:VOLT:DC DEF,DEF,(@101)")]
    [InlineData(MuxFunction.VAC, "CONF:VOLT:AC DEF,(@101)")]
    [InlineData(MuxFunction.OHM2W, "CONF:RES DEF,DEF,(@101)")]
    [InlineData(MuxFunction.OHM4W, "CONF:FRES DEF,DEF,(@101)")]
    [InlineData(MuxFunction.CURR_DC, "CONF:CURR:DC DEF,DEF,(@101)")]
    [InlineData(MuxFunction.FREQ, "CONF:FREQ DEF,(@101)")]
    [InlineData(MuxFunction.PERIOD, "CONF:PER DEF,(@101)")]
    public void ChannelMeasurement_BuildConf(MuxFunction func, string expected)
    {
        var ch = new ChannelMeasurement(101, func);
        Assert.Equal(expected, ch.BuildConfCommand());
    }

    [Fact]
    public void ChannelMeasurement_BuildConf_Temperature()
    {
        Assert.Equal("CONF:TEMP TC,K,(@104)",
            new ChannelMeasurement(104, MuxFunction.TEMP_TC) { TcType = TcType.K }.BuildConfCommand());
        Assert.Equal("CONF:TEMP RTD,85,(@103)",
            new ChannelMeasurement(103, MuxFunction.TEMP_RTD) { RtdAlpha = 85 }.BuildConfCommand());
        Assert.Equal("CONF:TEMP FRTD,91,(@103)",
            new ChannelMeasurement(103, MuxFunction.TEMP_RTD4W) { RtdAlpha = 91 }.BuildConfCommand());
        Assert.Equal("CONF:TEMP THER,5000,(@103)",
            new ChannelMeasurement(103, MuxFunction.TEMP_THERM) { ThermistorType = 5000 }.BuildConfCommand());
    }

    [Fact]
    public void ChannelMeasurement_RangeAutoMapsToDef_NumericPassesThrough()
    {
        Assert.Contains("DEF", new ChannelMeasurement(101, MuxFunction.VDC, "AUTO").BuildConfCommand());
        Assert.Contains("10", new ChannelMeasurement(101, MuxFunction.VDC, "10").BuildConfCommand());
    }

    [Fact]
    public void ChannelMeasurement_BuildNplc_SupportedAndUnsupported()
    {
        var dc = new ChannelMeasurement(101, MuxFunction.VDC) { Nplc = "10" };
        Assert.Equal("SENS:VOLT:DC:NPLC 10,(@101)", dc.BuildNplcCommand());

        var ac = new ChannelMeasurement(101, MuxFunction.VAC) { Nplc = "10" };
        Assert.Null(ac.BuildNplcCommand());           // AC doesn't support NPLC here

        var noNplc = new ChannelMeasurement(101, MuxFunction.VDC);
        Assert.Null(noNplc.BuildNplcCommand());        // empty NPLC -> null
    }

    [Fact]
    public void ChannelMeasurement_Scaling_EnabledAndDisabled()
    {
        var off = new ChannelMeasurement(101, MuxFunction.VDC) { ScaleEnabled = false };
        Assert.Equal(new[] { "CALC:SCAL:STAT OFF,(@101)" }, off.BuildScalingCommands().ToArray());

        var on = new ChannelMeasurement(101, MuxFunction.VDC)
        {
            ScaleEnabled = true, ScaleGain = 2.0, ScaleOffset = 1.0, ScaleUnit = "PSI"
        };
        var cmds = on.BuildScalingCommands().ToArray();
        Assert.Contains("CALC:SCAL:GAIN 2,(@101)", cmds);
        Assert.Contains("CALC:SCAL:OFFS 1,(@101)", cmds);
        Assert.Contains("CALC:SCAL:UNIT \"PSI\",(@101)", cmds);
        Assert.Contains("CALC:SCAL:STAT ON,(@101)", cmds);
    }

    [Fact]
    public void ChannelMeasurement_Unit()
    {
        Assert.Equal("V", new ChannelMeasurement(101, MuxFunction.VDC).Unit);
        Assert.Equal("Ω", new ChannelMeasurement(101, MuxFunction.OHM2W).Unit);
        Assert.Equal("A", new ChannelMeasurement(101, MuxFunction.CURR_DC).Unit);
        Assert.Equal("Hz", new ChannelMeasurement(101, MuxFunction.FREQ).Unit);
        Assert.Equal("°C", new ChannelMeasurement(101, MuxFunction.TEMP_TC).Unit);
    }

    [Theory]
    [InlineData("VDC", MuxFunction.VDC)]
    [InlineData("RES", MuxFunction.OHM2W)]
    [InlineData("FRES", MuxFunction.OHM4W)]
    [InlineData("IDC", MuxFunction.CURR_DC)]
    [InlineData("RTD4W", MuxFunction.TEMP_RTD4W)]
    [InlineData("nonsense", MuxFunction.VDC)]
    public void ChannelMeasurement_FromUi_MapsAliases(string func, MuxFunction expected)
    {
        Assert.Equal(expected, ChannelMeasurement.FromUi(101, func, "AUTO", "").Function);
    }

    [Fact]
    public void ChannelMeasurement_FromUi_ParsesTcTypeParam()
    {
        var ch = ChannelMeasurement.FromUi(104, "TC", "AUTO", "J");
        Assert.Equal(MuxFunction.TEMP_TC, ch.Function);
        Assert.Equal(TcType.J, ch.TcType);
    }

    // ── ScanSettings ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("IMM", "IMM", false)]
    [InlineData("TIMER", "TIM", true)]
    [InlineData("EXT", "EXT", false)]
    [InlineData("BUS", "BUS", false)]
    [InlineData("ALARM", "ALAR", false)]
    [InlineData("weird", "IMM", false)]
    public void ScanSettings_TriggerSourceMapping(string input, string scpi, bool isTimer)
    {
        var s = new ScanSettings { TriggerSource = input };
        Assert.Equal(scpi, s.ScpiTriggerSource);
        Assert.Equal(isTimer, s.IsTimer);
    }
}
