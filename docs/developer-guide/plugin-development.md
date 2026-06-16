# Plugin Development

This guide walks you through writing a new instrument driver plugin from scratch. A minimal plugin consists of three files: the project file, the driver class, and at least one sequence block class.

---

## 1. Create the Project

Create a folder under `instruments/`:

```
instruments/
└── YourInstrument/
    ├── YourInstrument.csproj
    ├── YourInstrumentDriver.cs
    ├── YourInstrumentBlocks.cs
    └── Views/
        ├── YourFrontPanelView.xaml
        ├── YourFrontPanelView.xaml.cs
        └── YourFrontPanelViewModel.cs
```

**YourInstrument.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>YourInstrument</AssemblyName>
    <RootNamespace>YourInstrument</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\InstrumentControl.Core\InstrumentControl.Core.csproj" />
  </ItemGroup>
</Project>
```

Add the new project to `InstrumentControl.sln` and to `build.ps1`.

---

## 2. Write the Driver

```csharp
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using System.Windows;

namespace YourInstrument;

public class YourInstrumentDriver : InstrumentDriverBase
{
    // --- Metadata -------------------------------------------------------
    public override string DriverName    => "YourInstrument";
    public override string Manufacturer  => "YourBrand";
    public override string Model         => "Model123";
    public override string Description   => "Short description of the instrument.";

    // Resource patterns that the Connection Manager uses to match this driver.
    // Use VISA resource patterns for VISA instruments, "COM?*" for serial.
    public override string[] SupportedResourcePatterns => new[]
    {
        "GPIB?*::?*::INSTR",
        "USB?*::?*::INSTR",
    };

    // --- Front Panel ----------------------------------------------------
    public override FrameworkElement CreateFrontPanel()
        => new YourFrontPanelView { DataContext = new YourFrontPanelViewModel(this) };

    // --- Sequence Blocks ------------------------------------------------
    public override IEnumerable<ISequenceBlock> GetAvailableBlocks()
        => BlockRegistry.GetBlocks(GetType().Assembly);

    // --- Instrument-Specific Methods ------------------------------------
    public async Task<double> MeasureDCVAsync()
    {
        await WriteAsync("CONF:VOLT:DC DEF,DEF");
        return await QueryDoubleAsync("READ?");
    }
}
```

### Key Points

- **Inherit `InstrumentDriverBase`** — it provides `ConnectAsync()` (opens VISA session, sends `*IDN?`, parses firmware), `WriteAsync()`, `QueryAsync()`, `QueryDoubleAsync()`, and event helpers.
- **Override `SupportedResourcePatterns`** — VISA resource patterns use `?*` as a wildcard. `"GPIB?*::?*::INSTR"` matches any GPIB address.
- **`CreateFrontPanel()`** must return a `FrameworkElement`. Return `new TextBlock { Text = "No panel" }` for a stub.
- **`GetAvailableBlocks()`** scans your assembly for all classes that registered themselves in `BlockRegistry`. Never return `new[]` with explicit instances — the engine needs fresh instances from the registry.

---

## 3. Write Sequence Blocks

```csharp
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace YourInstrument;

public class YourInstrument_MeasureDCV : SequenceBlockBase
{
    // --- Block Metadata ------------------------------------------------
    public override string BlockType    => "YourInstrument_MeasureDCV";
    public override string DisplayName  => "Measure DCV";
    public override string Description  => "Measures DC voltage on YourInstrument.";
    public override string BlockColor   => "#4CAF50";
    public override string Category     => "YourInstrument";

    // --- Property Definitions ------------------------------------------
    public override BlockPropertyDefinition[] PropertyDefinitions => new[]
    {
        BlockPropertyDefinition.Instrument("Instrument", "Select the connected instrument"),
        BlockPropertyDefinition.Number("Range", "Measurement range (0 = Auto)", 0),
        BlockPropertyDefinition.Variable("VariableName", "Store result in this variable", "dcv"),
    };

    // --- Self-Registration (runs once at startup) ----------------------
    static YourInstrument_MeasureDCV()
    {
        BlockRegistry.Register("YourInstrument_MeasureDCV",
            () => new YourInstrument_MeasureDCV());
    }

    // --- Execution -----------------------------------------------------
    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        // 1. Resolve the instrument
        var instrName = GetPropStr("Instrument");
        if (!context.Instruments.TryGetValue(instrName, out var driver))
            return BlockExecutionResult.Fail($"Instrument '{instrName}' not connected.", NextBlockId);

        if (driver is not YourInstrumentDriver yourDriver)
            return BlockExecutionResult.Fail("Wrong driver type.", NextBlockId);

        // 2. Perform the measurement
        double value = await yourDriver.MeasureDCVAsync();

        // 3. Store result in the context
        var varName = GetPropStr("VariableName");
        if (!string.IsNullOrEmpty(varName))
            context.SetVariable(varName, value);

        // 4. Emit a measurement event (appears in Data Viewer)
        context.OnMeasurement(new MeasurementResult
        {
            InstrumentName = instrName,
            ParameterName  = "DCV",
            Value          = value,
            Unit           = "V",
            Function       = "DCV",
        });

        // 5. Log progress
        context.Log($"[YourInstrument] DCV = {value:F6} V");

        // 6. Continue to the next block
        return BlockExecutionResult.Ok(NextBlockId, value);
    }
}
```

### Key Points

- **`BlockType`** must be a unique string. Convention: `DriverName_FunctionName`.
- **Static constructor** registers the block with `BlockRegistry`. This is called automatically the first time the class is referenced — which happens when the DLL is loaded.
- **`BlockExecutionResult.Ok(NextBlockId)`** continues to the connected next block. **`BlockExecutionResult.Fail(msg)`** logs an error and optionally continues or halts.
- **`context.OnMeasurement()`** fires the `DataManager.ResultAdded` event, making the result visible in the Data Viewer immediately.
- **`GetPropStr("Name")`** retrieves a property value as a string. Use **`GetProp<T>("Name")`** to parse to a specific type.

---

## 4. Write the Front Panel

A minimal front panel is a standard WPF UserControl with data binding to a ViewModel.

**YourFrontPanelView.xaml**

```xml
<UserControl x:Class="YourInstrument.Views.YourFrontPanelView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Margin="10" Spacing="8">
        <TextBlock Text="YourInstrument" FontSize="18" FontWeight="Bold"/>
        <Button Content="Measure DCV"
                Command="{Binding MeasureDCVCommand}"
                Width="150"/>
        <TextBlock Text="{Binding LastReading}"
                   FontSize="24" FontFamily="Courier New"/>
    </StackPanel>
</UserControl>
```

**YourFrontPanelViewModel.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YourInstrument.Views;

public partial class YourFrontPanelViewModel : ObservableObject
{
    private readonly YourInstrumentDriver _driver;

    [ObservableProperty]
    private string _lastReading = "---";

    public YourFrontPanelViewModel(YourInstrumentDriver driver)
    {
        _driver = driver;
    }

    [RelayCommand]
    private async Task MeasureDCVAsync()
    {
        double v = await _driver.MeasureDCVAsync();
        LastReading = $"{v:F6} V";
    }
}
```

---

## 5. Add to Build

**build.ps1** — add a build step for your project:

```powershell
dotnet build instruments/YourInstrument/YourInstrument.csproj `
    -c Release --nologo -v q
```

And a copy step in the instrument DLL collection section:

```powershell
Copy-Item "instruments/YourInstrument/bin/Release/net8.0-windows/YourInstrument.dll" $dest
```

**release.yml** — add the same build step to the GitHub Actions workflow.

---

## Custom Serial Protocol

For devices that use a proprietary binary protocol over RS-232, implement `IConnectionProvider` to handle the framing transparently:

```csharp
public class MyCustomSerialProvider : IConnectionProvider
{
    private SerialPort _port;

    public MyCustomSerialProvider(string portName)
    {
        _port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
    }

    public string ResourceName => _port.PortName;
    public string ConnectionType => "COM-CUSTOM";
    public bool IsOpen => _port.IsOpen;

    public Task OpenAsync() { _port.Open(); return Task.CompletedTask; }
    public Task CloseAsync() { _port.Close(); return Task.CompletedTask; }
    public void Dispose() => _port.Dispose();

    public async Task<string> QueryAsync(string command)
    {
        await WriteAsync(command);
        return await ReadAsync();
    }

    public async Task WriteAsync(string command)
    {
        byte[] frame = BuildFrame(command);
        _port.Write(frame, 0, frame.Length);
        await Task.CompletedTask;
    }

    public async Task<string> ReadAsync()
    {
        byte[] frame = ReadFrame();
        return DecodeFrame(frame);
    }

    private byte[] BuildFrame(string command) { /* ... custom encoding ... */ }
    private byte[] ReadFrame() { /* ... read until ETX ... */ }
    private string DecodeFrame(byte[] frame) { /* ... decode ... */ }

    // These are rarely needed for serial devices but must be implemented:
    public Task WriteRawAsync(byte[] data) => throw new NotSupportedException();
    public Task<byte[]> ReadRawAsync(int count) => throw new NotSupportedException();
}
```

In the driver, override `ConnectAsync` to swap out the generic provider:

```csharp
public override async Task ConnectAsync(IConnectionProvider genericProvider)
{
    string port = genericProvider.ResourceName;   // e.g. "COM3"
    genericProvider.Dispose();                    // discard the unused VISA provider

    var customProvider = new MyCustomSerialProvider(port);
    await customProvider.OpenAsync();

    // Call base with the custom provider instead
    await base.ConnectAsync(customProvider);
}
```

Set `SupportedResourcePatterns = new[] { "COM?*" }` so the driver shows up for COM ports in the Connection Manager.

---

## Plugin Checklist

- [ ] Project references `InstrumentControl.Core`
- [ ] Driver inherits `InstrumentDriverBase`
- [ ] `DriverName`, `Manufacturer`, `Model`, `Description` overridden
- [ ] `SupportedResourcePatterns` returns correct VISA/COM patterns
- [ ] `CreateFrontPanel()` returns a valid `FrameworkElement`
- [ ] `GetAvailableBlocks()` calls `BlockRegistry.GetBlocks(GetType().Assembly)`
- [ ] Each block has a **unique** `BlockType` string
- [ ] Each block's **static constructor** calls `BlockRegistry.Register(...)`
- [ ] `ExecuteAsync` returns `BlockExecutionResult.Ok(NextBlockId)` on success
- [ ] Project added to `InstrumentControl.sln`
- [ ] Build and copy steps added to `build.ps1`
- [ ] Build step added to `.github/workflows/release.yml`
