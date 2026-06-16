# Core Interfaces

All plugin contracts live in `src/InstrumentControl.Core/Interfaces/`. These interfaces define the boundary between the application and instrument plugins.

---

## IInstrumentDriver

**File:** `src/InstrumentControl.Core/Interfaces/IInstrumentDriver.cs`

The primary plugin contract. Every instrument driver must implement this interface (typically by inheriting `InstrumentDriverBase`).

```csharp
public interface IInstrumentDriver : IDisposable
{
    // --- Identity ---
    string DriverName    { get; }
    string Manufacturer  { get; }
    string Model         { get; }
    string Description   { get; }

    // --- Connection matching ---
    string[] SupportedResourcePatterns { get; }

    // --- State ---
    bool IsConnected       { get; }
    InstrumentInfo? Info   { get; }

    // --- Lifecycle ---
    Task ConnectAsync(IConnectionProvider connection);
    Task DisconnectAsync();
    Task<string> GetIdentificationAsync();
    Task ResetAsync();

    // --- Plugin extension points ---
    FrameworkElement CreateFrontPanel();
    IEnumerable<ISequenceBlock> GetAvailableBlocks();

    // --- Events ---
    event EventHandler<MeasurementResult> MeasurementReceived;
    event EventHandler<string>            StatusChanged;
    event EventHandler<Exception>         ErrorOccurred;
}
```

### SupportedResourcePatterns

Used by the Connection Manager to highlight the matching driver when a resource is selected. Patterns use `?` (single character) and `*` (zero or more characters):

| Pattern | Matches |
|---|---|
| `GPIB?*::?*::INSTR` | Any GPIB resource |
| `USB?*::?*::INSTR` | Any USB-TMC resource |
| `TCPIP?*::?*::INSTR` | Any LAN/VXI-11 resource |
| `ASRL?*::INSTR` | Any VISA serial resource |
| `COM?*` | Windows COM ports (non-VISA) |

### Events

| Event | Payload | When |
|---|---|---|
| `MeasurementReceived` | `MeasurementResult` | Every time the driver produces a data point (front panel or block) |
| `StatusChanged` | `string` status message | Connection state changes |
| `ErrorOccurred` | `Exception` | Communication errors |

---

## ISequenceBlock

**File:** `src/InstrumentControl.Core/Interfaces/ISequenceBlock.cs`

Defines a single node in a sequence graph.

```csharp
public interface ISequenceBlock
{
    // --- Identity & display ---
    string BlockType    { get; }
    string DisplayName  { get; }
    string Description  { get; }
    string BlockColor   { get; }   // CSS hex color, e.g. "#4CAF50"
    string Category     { get; }

    // --- Canvas position ---
    double X { get; set; }
    double Y { get; set; }

    // --- Graph linking ---
    string  BlockId    { get; }
    string? NextBlockId { get; set; }

    // --- Properties (editable at design time) ---
    Dictionary<string, object?> Properties { get; set; }
    BlockPropertyDefinition[]   PropertyDefinitions { get; }

    // --- Runtime ---
    Task<BlockExecutionResult> ExecuteAsync(SequenceContext context);

    // --- Persistence ---
    BlockData   Serialize();
    void        Deserialize(BlockData data);

    // --- Cloning ---
    ISequenceBlock Clone();
}
```

### BlockColor

Use a CSS hex color string. Suggested palette:

| Color | Hex | Suggested use |
|---|---|---|
| Green | `#4CAF50` | Measurement blocks |
| Blue | `#2196F3` | Output / file blocks |
| Orange | `#FF9800` | Loop / repeat blocks |
| Purple | `#9C27B0` | Condition / branch blocks |
| Teal | `#009688` | Variable / data blocks |
| Gray | `#607D8B` | Wait / utility blocks |
| Red | `#F44336` | Stop / error blocks |

### Category

The category string is used to group blocks in the toolbox. Use your `DriverName` for instrument-specific blocks (e.g. `"HP34401A"`) and logical names for built-in blocks (`"Control"`, `"Data"`, `"Output"`).

---

## IConnectionProvider

**File:** `src/InstrumentControl.Core/Interfaces/IConnectionProvider.cs`

Abstracts the communication transport. The driver never talks directly to VISA or COM ports — it always goes through this interface.

```csharp
public interface IConnectionProvider : IDisposable
{
    string ResourceName    { get; }
    string ConnectionType  { get; }   // "VISA", "COM-CTS", "SIMULATION", ...
    bool   IsOpen          { get; }

    Task OpenAsync();
    Task CloseAsync();

    // ASCII SCPI operations
    Task         WriteAsync(string command);
    Task<string> QueryAsync(string command);
    Task<string> ReadAsync();

    // Binary operations (for waveform data, etc.)
    Task         WriteRawAsync(byte[] data);
    Task<byte[]> ReadRawAsync(int count);
}
```

### Implementations

| Class | Transport | Notes |
|---|---|---|
| `VisaConnectionProvider` | NI-VISA (P/Invoke) | Used for all GPIB, USB-TMC, TCPIP resources |
| `CTSSerialConnectionProvider` | System.IO.Ports.SerialPort | Custom binary framing for CTS chambers |
| `SimulatedConnectionProvider` | In-memory | Returns hardcoded defaults; no hardware required |

---

## IHasBodyOutput

**File:** `src/InstrumentControl.Core/Interfaces/IHasBodyOutput.cs`

Marker interface for blocks that have a secondary execution path (loop body, condition branch).

```csharp
public interface IHasBodyOutput
{
    string? BodyBlockId { get; set; }
}
```

`BlockCanvas` uses this interface to detect blocks that need a second output dot drawn on the canvas. `LoopBlock` and `ConditionBlock` implement this interface.

---

## BlockExecutionResult

**File:** `src/InstrumentControl.Core/Models/BlockExecutionResult.cs`

Returned by `ISequenceBlock.ExecuteAsync()`.

```csharp
public class BlockExecutionResult
{
    public bool    Success      { get; init; }
    public string? NextBlockId  { get; init; }   // null = terminate sequence
    public string? ErrorMessage { get; init; }
    public object? OutputValue  { get; init; }   // optional intermediate result

    // Factory methods
    public static BlockExecutionResult Ok(string? nextBlockId, object? output = null);
    public static BlockExecutionResult Fail(string error, string? nextBlockId = null);
}
```

- Returning `Ok(null)` terminates the sequence successfully.
- Returning `Fail(msg)` logs the error and, if `nextBlockId` is provided, continues to that block (useful for error recovery paths).
- The `OutputValue` field is optional and is not used by the engine — it can carry data for debugging or for blocks that wrap other blocks.

---

## SequenceContext

**File:** `src/InstrumentControl.Core/Models/SequenceContext.cs`

Passed to every `ExecuteAsync()` call. Provides access to all runtime state.

```csharp
public class SequenceContext
{
    // Connected instruments, keyed by DriverName
    public Dictionary<string, IInstrumentDriver> Instruments { get; }

    // Variables set/read by blocks during a run
    public Dictionary<string, object?> Variables { get; }

    // All blocks in the sequence, keyed by BlockId (for jumping / condition branches)
    public Dictionary<string, ISequenceBlock> AllBlocks { get; }

    // Results produced so far in this run
    public List<MeasurementResult> Results { get; }

    // Cancellation (triggered by Stop button)
    public CancellationToken CancellationToken { get; }

    // --- Helper methods ---
    public void   SetVariable(string name, object? value);
    public object? GetVariable(string name);
    public T?      GetVariable<T>(string name);
    public double  GetVariableAsDouble(string name, double defaultValue = 0);

    public void Log(string message);
    public void OnMeasurement(MeasurementResult result);
}
```

### Variable Substitution

The `{varname}` syntax in `LogMessageBlock` and `MathBlock` properties is resolved by reading from `context.Variables`. Example:

```
Message: "Temperature = {temp} °C"  →  "Temperature = 23.45 °C"
```

Blocks that accept a *Variable Name* property write the result to `context.SetVariable(varName, value)` for downstream blocks to use.
