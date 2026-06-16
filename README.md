# InstrumentControl

**Universal Windows instrument control software** for VISA/GPIB/serial lab equipment.  
Built with .NET 8 WPF, a plugin DLL architecture, and a visual Scratch-like sequence editor.

[![Build](https://github.com/bartkepl/InstrumentControl/actions/workflows/release.yml/badge.svg)](https://github.com/bartkepl/InstrumentControl/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Features

- **Virtual front panels** — realistic per-instrument UI with controls matching the real device
- **Live Data window** — floating OxyPlot chart + measurement table, one window per instrument
- **Visual sequence editor** — drag-and-drop block programming (Start → Measure → Loop → Save CSV → End)
- **VISA auto-discovery** — scans USB, GPIB, TCPIP and serial for connected instruments
- **Non-VISA serial support** — custom RS-232 devices with proprietary binary protocols are handled via `IConnectionProvider` implementations inside the plugin (e.g. CTS chamber binary ASCII framing)
- **Instrument auto-detection** — sends `*IDN?` to the selected resource and highlights the matching driver automatically
- **Simulation mode** — runs without NI-VISA installed; great for UI development
- **Plugin architecture** — each instrument is an independent DLL, loaded at runtime
- **Data viewer** — live charts and tables from sequence runs
- **About dialog** — version, author and GitHub link accessible via the toolbar

## Supported Instruments

| Instrument | Type | Interface | Sequence blocks |
|---|---|---|---|
| HP 34401A | 6½-digit DMM | VISA (GPIB/USB/RS-232) | MeasureDCV, MeasureACV, MeasureDCI, MeasureACI, MeasureResistance, MeasureFrequency, MeasurePeriod, MeasureTemperature |
| Agilent 34970A | DAQ / Switch Unit | VISA (GPIB/USB) | ScanChannels, SetDAC, SetDigitalOutput, MeasureTemperature, MeasureVoltage |
| Keithley 2000 | 6½-digit DMM | VISA (GPIB/USB) | MeasureDCV, MeasureACV, MeasureDCI, MeasureACI, MeasureResistance, MeasureFrequency, MeasurePeriod, MeasureTemperature |
| ITECH IT6922B | DC Power Supply (60 V / 5 A) | VISA (USB/LAN/GPIB) | SetVoltage, SetCurrent, SetOutput, MeasureVoltage, MeasureCurrent, MeasurePower, SetOVP, SetOCP |
| R&S RTB2004 | 4-channel oscilloscope 300 MHz | VISA (LAN/USB) | SetChannel, SetTimebase, SetTrigger, Run, Stop, Single, Autoscale, Measure, ReadWaveform |
| CTS T-40/50 | Environmental chamber (−75 … +185 °C) | RS-232 (non-VISA, binary ASCII protocol) | SetTemperature, SetRamp, ChamberStart, ChamberStop, ChamberPause, ReadTemperature, WaitForTemperature |

## Requirements

| Component | Version |
|---|---|
| Windows | 10 or 11, 64-bit |
| NI-VISA *(optional)* | 21.x or newer |

**.NET 8 Runtime is not required** — the installer and ZIP both include the runtime (self-contained build).

Without NI-VISA the application starts in **simulation mode** — all VISA calls return a configurable default value, so you can develop and test sequences without physical hardware.

## Installation

### Option A — Installer (recommended)

1. Download `InstrumentControl-x.y.z-win-Setup.exe` from the [Releases](../../releases) page
2. Run it — the app installs to `%LocalAppData%\InstrumentControl\` (no admin rights needed)
3. A shortcut is created in the Start menu
4. **The app updates itself automatically** on every launch when a new release is available

To install to a custom folder, run the installer from the command line:

```
InstrumentControl-x.y.z-win-Setup.exe --installDir "D:\Lab\InstrumentControl"
```

### Option B — Portable ZIP

1. Download `InstrumentControl-x.y.z-win-x64.zip` from the [Releases](../../releases) page
2. Extract to any folder — no installer required
3. Run `InstrumentControl.exe`

> The ZIP version does **not** auto-update. To update, download and extract the new ZIP manually.

## Automatic Updates

When installed via the Setup EXE, the application checks GitHub Releases for a newer version on every launch — **before the main window opens**. If an update is found, a dialog appears asking whether to update now. Choosing **Yes** downloads only the changed files (delta update) and restarts the app into the new version. Choosing **No** skips the update and launches normally.

Update checks require an internet connection. If GitHub is unreachable (e.g. isolated lab network), the check silently times out and the app starts normally.

## Quick Start

1. Run `InstrumentControl.exe` (installed or portable)
2. Click **Add Instrument** in the left sidebar
3. The Connection Manager opens and scans for VISA resources automatically
4. Select a resource — the app queries it with `*IDN?` and highlights the matching driver; select a driver manually if needed, then click **Connect**
5. The **Front Panel** tab opens — use it to control the instrument directly
6. Switch to the **Sequence Editor** tab to build automated measurement programs:
   - Drag blocks from the left toolbox onto the canvas
   - Connect blocks by dragging from the green output dot to the next block's input
   - Double-click a block to set its properties
   - Click **Run** to execute the sequence
7. Results appear in the **Data / Charts** tab

## Building from Source

**Prerequisites:** .NET 8 SDK, Windows 10/11 64-bit

```powershell
git clone https://github.com/bartkepl/InstrumentControl.git
cd InstrumentControl
.\build.ps1
```

The script builds all projects and assembles the output:

```
bin\Release\
  InstrumentControl.exe
  instruments\
    HP34401A.dll
    Agilent34970A.dll
    Keithley2000.dll
    ItechIT6922B.dll
    RTB2004.dll
    CTSChamber.dll
  ... (.NET 8 runtime + WPF files — self-contained, no separate runtime install needed)
```

For a debug build:

```powershell
.\build.ps1 -Debug
```

> **Note:** Close `InstrumentControl.exe` before rebuilding — the running process locks the output DLLs (`MSB3027`).

## Project Structure

```
InstrumentControl.sln
├── src/
│   ├── InstrumentControl.Core/        # Core library (no WPF UI)
│   │   ├── Base/                      # InstrumentDriverBase, SequenceBlockBase
│   │   ├── Interfaces/                # IInstrumentDriver, ISequenceBlock, IConnectionProvider
│   │   ├── Models/                    # MeasurementResult, BlockData, SequenceContext, …
│   │   ├── Services/                  # VisaService (P/Invoke), PluginLoader, SequenceEngine, DataManager
│   │   └── Views/                     # LiveDataWindow (OxyPlot + DataGrid, code-behind only)
│   └── InstrumentControl.App/         # WPF executable
│       ├── Controls/                  # BlockCanvas (drag-and-drop), BlockPropertiesEditor
│       ├── ViewModels/                # MainWindow, SequenceEditor, DataViewer, ConnectionManager
│       └── Views/                     # XAML views and windows
└── instruments/
    ├── HP34401A/                      # HP 34401A plugin DLL
    │   ├── HP34401ADriver.cs          # SCPI driver
    │   ├── HP34401ABlocks.cs          # 8 sequence blocks
    │   └── Views/                     # Front panel XAML + ViewModel
    ├── Agilent34970A/                 # Agilent 34970A plugin DLL
    ├── Keithley2000/                  # Keithley 2000 plugin DLL
    ├── ItechIT6922B/                  # ITECH IT6922B DC power supply plugin DLL
    ├── RTB2004/                       # R&S RTB2004 oscilloscope plugin DLL
    └── CTSChamber/                    # CTS T-40/50 environmental chamber plugin DLL
        ├── CTSSerialConnectionProvider.cs  # Custom IConnectionProvider — CTS binary ASCII framing
        ├── CTSChamberDriver.cs             # Driver (overrides ConnectAsync to use CTS provider)
        ├── CTSChamberBlocks.cs             # 7 sequence blocks
        └── Views/                          # Front panel XAML + ViewModel
```

## Adding a New Instrument Plugin

1. Create a project under `instruments/YourInstrument/`:

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

2. Implement a driver inheriting `InstrumentDriverBase`:

```csharp
[Export(typeof(IInstrumentDriver))]
public class YourDriver : InstrumentDriverBase
{
    public override string DriverName    => "Your Instrument";
    public override string Manufacturer  => "Brand";
    public override string Model         => "Model123";

    public override FrameworkElement CreateFrontPanel() =>
        new YourFrontPanelView { DataContext = new YourFrontPanelViewModel(this) };

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() =>
        BlockRegistry.GetBlocks(GetType().Assembly);
}
```

3. Add `instruments/YourInstrument/YourInstrument.csproj` to the solution and to `build.ps1`.

4. Build — copy `YourInstrument.dll` into the `instruments/` subfolder next to `InstrumentControl.exe`.

The plugin is discovered automatically on the next launch.

### Non-VISA serial devices (RS-232 with proprietary protocols)

Instruments that use a custom binary protocol over COM port (e.g. CTS environmental chambers) implement a custom `IConnectionProvider` inside the plugin and override `ConnectAsync` in the driver to swap out the generic provider:

```csharp
public override async Task ConnectAsync(IConnectionProvider connection)
{
    string port = connection.ResourceName;  // e.g. "COM3"
    connection.Dispose();                   // discard generic provider

    var custom = new MyCustomSerialProvider(port);  // 19200 baud, odd parity, ...
    Connection = custom;
    await custom.OpenAsync();
    // ...
}
```

`SupportedResourcePatterns = new[] { "COM?*" }` makes the driver appear in the connection manager for COM ports. The custom provider implements the binary framing in `QueryAsync`/`WriteAsync`, keeping the driver code clean (standard base class helpers work unchanged).

## Built-in Sequence Blocks

| Block | Description |
|---|---|
| `StartBlock` | Entry point of every sequence |
| `EndBlock` | Terminates the sequence |
| `WaitBlock` | Pause for N milliseconds |
| `LoopBlock` | Repeat N times |
| `ConditionBlock` | Branch based on a variable comparison |
| `SetVariableBlock` | Assign a value to a named variable |
| `LogMessageBlock` | Write a message to the sequence log |
| `SaveCsvBlock` | Append measurement results to a CSV file |
| `AddToChartBlock` | Plot a value on the Data Viewer chart |

Instrument plugins add their own measurement blocks (e.g. `HP34401A_MeasureDCV`).

## Dependencies

All NuGet packages used by this project are licensed under the **MIT License**:

| Package | Version | License |
|---|---|---|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.3.2 | MIT |
| [OxyPlot.Wpf](https://github.com/oxyplot/oxyplot) | 2.1.2 | MIT |
| [Microsoft.Xaml.Behaviors.Wpf](https://github.com/microsoft/XamlBehaviorsWpf) | 1.1.135 | MIT |
| [System.Composition](https://github.com/dotnet/runtime) | 8.0.0 | MIT |
| [System.IO.Ports](https://github.com/dotnet/runtime) | 8.0.0 | MIT |
| [Velopack](https://github.com/velopack/velopack) | 1.2.0 | MIT |

NI-VISA is a separately installed National Instruments product and is **not bundled** with this software. It is required only for communication with physical instruments.

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.
