using System.IO;
using System.Windows;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Tests;

// A public driver with a public parameterless ctor so PluginLoader can Activator.CreateInstance it.
public class SampleLoaderDriver : InstrumentDriverBase
{
    public override string DriverName => "SampleLoaderDriver";
    public override string Manufacturer => "SampleCo";
    public override string Model => "SL-1";
    public override string Description => "loader test driver";
    public override string[] SupportedResourcePatterns => new[] { "SIM?*::INSTR" };
    public override FrameworkElement CreateFrontPanel() => null!;
    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() => Array.Empty<ISequenceBlock>();
}

public class PluginLoaderTests
{
    [Fact]
    public void LoadFromDirectory_NonExistent_DoesNotThrow()
    {
        var loader = new PluginLoader();
        loader.LoadFromDirectory(Path.Combine(Path.GetTempPath(), "no_such_dir_" + Guid.NewGuid()));
        Assert.Empty(loader.LoadedDrivers);
    }

    [Fact]
    public void LoadFromAssembly_DiscoversPublicDriver()
    {
        var loader = new PluginLoader();
        loader.LoadFromAssembly(typeof(SampleLoaderDriver).Assembly);
        Assert.Contains(loader.LoadedDrivers, d => d.DriverName == "SampleLoaderDriver");
    }

    [Fact]
    public void CreateDriver_ReturnsFreshInstance()
    {
        var loader = new PluginLoader();
        loader.LoadFromAssembly(typeof(SampleLoaderDriver).Assembly);

        var a = loader.CreateDriver("SampleLoaderDriver");
        var b = loader.CreateDriver("sampleloaderdriver"); // case-insensitive

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotSame(a, b);
        Assert.IsType<SampleLoaderDriver>(a);
    }

    [Fact]
    public void CreateDriver_Unknown_ReturnsNull()
    {
        var loader = new PluginLoader();
        Assert.Null(loader.CreateDriver("DoesNotExist"));
    }

    [Fact]
    public void GetAvailableDrivers_IncludesMetadata()
    {
        var loader = new PluginLoader();
        loader.LoadFromAssembly(typeof(SampleLoaderDriver).Assembly);

        var entry = loader.GetAvailableDrivers().FirstOrDefault(d => d.Name == "SampleLoaderDriver");
        Assert.Equal("SampleCo", entry.Manufacturer);
        Assert.Equal("SL-1", entry.Model);
    }
}
