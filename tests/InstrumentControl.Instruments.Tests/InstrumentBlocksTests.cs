using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Instruments.Tests;

public class InstrumentBlocksTests
{
    private static IInstrumentDriver[] AllDrivers() => new IInstrumentDriver[]
    {
        new HP34401A.HP34401ADriver(),
        new Keithley2000.Keithley2000Driver(),
        new ItechIT6922B.ItechIT6922BDriver(),
        new RTB2004.RTB2004Driver(),
        new RigolDS1000Z.RigolDS1000ZDriver(),
        new Agilent34970A.Agilent34970ADriver(),
        new CTSChamber.CTSChamberDriver(),
    };

    [Fact]
    public void AllBlocks_HavePopulatedMetadata()
    {
        foreach (var driver in AllDrivers())
        {
            foreach (var block in driver.GetAvailableBlocks())
            {
                Assert.False(string.IsNullOrWhiteSpace(block.BlockType), $"{driver.DriverName}: empty BlockType");
                var b = (SequenceBlockBase)block;
                Assert.False(string.IsNullOrWhiteSpace(b.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(b.Description));
                Assert.False(string.IsNullOrWhiteSpace(b.Category));
                _ = b.BlockColor;
                Assert.NotNull(b.PropertyDefinitions.ToList());
            }
        }
    }

    /// <summary>Pre-populate property defaults and wire the instrument selector to the connected driver.</summary>
    private static SequenceBlockBase Prepare(ISequenceBlock block, string instrName)
    {
        var b = (SequenceBlockBase)block;
        foreach (var pd in b.PropertyDefinitions)
        {
            if (pd.EditorType == PropertyEditorType.InstrumentSelector)
                b.Properties[pd.Name] = instrName;
            else if (pd.DefaultValue != null)
                b.Properties[pd.Name] = pd.DefaultValue;
        }
        return b;
    }

    private static async Task ExecuteAllBlocks(IInstrumentDriver driver, RecordingConnection? conn = null)
    {
        const string name = "INSTR";
        await driver.ConnectAsync(conn ?? new RecordingConnection());
        var ctx = new SequenceContext { CancellationToken = default };
        ctx.Instruments[name] = driver;

        var failures = new List<string>();
        foreach (var block in driver.GetAvailableBlocks())
        {
            var b = Prepare(block, name);
            try
            {
                var result = await b.ExecuteAsync(ctx);
                Assert.NotNull(result); // executed and returned a result (success or graceful failure)
            }
            catch (Exception ex)
            {
                failures.Add($"{b.BlockType}: {ex.GetType().Name} {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0,
            $"{driver.DriverName} blocks threw:\n" + string.Join("\n", failures));
    }

    [Fact]
    public Task HP34401A_Blocks_Execute() => ExecuteAllBlocks(new HP34401A.HP34401ADriver());

    [Fact]
    public Task Keithley2000_Blocks_Execute() => ExecuteAllBlocks(new Keithley2000.Keithley2000Driver());

    [Fact]
    public Task ItechIT6922B_Blocks_Execute() => ExecuteAllBlocks(new ItechIT6922B.ItechIT6922BDriver());

    [Fact]
    public Task RTB2004_Blocks_Execute() => ExecuteAllBlocks(new RTB2004.RTB2004Driver());

    [Fact]
    public Task RigolDS1000Z_Blocks_Execute() => ExecuteAllBlocks(new RigolDS1000Z.RigolDS1000ZDriver());

    [Fact]
    public Task CTSChamber_Blocks_Execute() => ExecuteAllBlocks(
        new CTSChamber.CTSChamberDriver(),
        // SIMULATION type -> driver uses the connection directly (no real COM port);
        // "A0 80 80" => actual == setpoint so WaitForTemperature returns immediately.
        new RecordingConnection("CTS,T-40,SN,1.0") { ConnectionType = "SIMULATION", ResourceName = "COM3" }
            .When("A0", "A0 80.00 80.00"));

    [Fact]
    public async Task DMM_MeasureBlocks_SetOutputVariable()
    {
        // HP DCV block should resolve the driver, measure, and store the value in a variable.
        var driver = new HP34401A.HP34401ADriver();
        await driver.ConnectAsync(new RecordingConnection("HP,34401A,SN,1.0").When("READ?", "+3.30E+00"));
        var ctx = new SequenceContext { CancellationToken = default };
        ctx.Instruments["DMM"] = driver;

        var block = new HP34401A.HP34401A_MeasureDCV();
        block.Properties["InstrumentName"] = "DMM";
        block.Properties["Range"] = "AUTO";
        block.Properties["NPLC"] = "1";
        block.Properties["OutputVariable"] = "v";

        var result = await block.ExecuteAsync(ctx);

        Assert.True(result.Success);
        Assert.Equal(3.3, ctx.GetVariableAsDouble("v"), 3);
    }

    [Fact]
    public async Task MeasureBlock_MissingInstrument_FailsGracefully()
    {
        var block = new HP34401A.HP34401A_MeasureDCV();
        block.Properties["InstrumentName"] = "NOPE";
        var ctx = new SequenceContext { CancellationToken = default };
        var result = await block.ExecuteAsync(ctx);
        Assert.False(result.Success);
    }
}
