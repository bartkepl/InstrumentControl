# Services Reference

The `InstrumentControl.Core` library exposes four services that the application and plugins rely on.

---

## VisaService

**File:** `src/InstrumentControl.Core/Services/VisaService.cs`

Provides access to the NI-VISA library via P/Invoke and handles the simulation mode fallback.

### Key Methods

```csharp
public class VisaService
{
    // Returns true when NI-VISA DLL was found and loaded successfully
    public bool IsVisa { get; }

    // Scan all VISA interfaces for connected resources
    public IReadOnlyList<string> FindResources();

    // Open a session to a specific resource
    // Returns VisaConnectionProvider (real hardware) or SimulatedConnectionProvider (no VISA)
    public IConnectionProvider OpenSession(string resourceName);
}
```

### Simulation Mode

If the NI-VISA DLL (`visa32.dll` or `visa64.dll`) is not found, `VisaService` activates simulation mode:

- `IsSimulationMode` returns `true`
- `FindResources()` returns a set of `SIM::` resource strings, one per supported instrument type
- `OpenSimulated(resourceName)` returns a `SimulatedConnectionProvider`
- The status bar displays `VISA: Simulation`

### SimulatedConnectionProvider

`SimulatedConnectionProvider` is a stateful SCPI / protocol simulator. It parses every `WriteAsync` call to track instrument state, then returns context-appropriate responses to `QueryAsync` / `ReadAsync`.

**State tracked across Write calls:**

| Write command | State updated |
|---|---|
| `CONF:VOLT:DC`, `CONF:RES`, … | Active DMM function (`_confFunc`) |
| `SAMP:COUN N` | Burst sample count |
| `ROUT:SCAN (@...)` | Number of channels to return on next `FETCH?` |
| `VOLT x` | Power supply voltage set-point |
| `CURR x` | Power supply current limit |
| `OUTP ON/OFF` | Power supply output state |
| `VOLT:PROT x`, `CURR:PROT:LEV x` | OVP / OCP levels |
| `a0 x` (CTS) | Chamber temperature set-point + timestamp reset |
| `u0 x` / `d0 x` (CTS) | Ramp-up / ramp-down rate (K/min) |
| `s1 1` / `s1 0` (CTS) | Chamber running state |
| `TIM:SCAL x` | Oscilloscope timebase |
| `CHANn:SCAL x` | Oscilloscope channel scale |

**Selected query responses:**

| Query | Response |
|---|---|
| `*IDN?` | `SIMULATED,INSTRUMENT,SIM001,1.0` |
| `READ?` / `FETCH?` | Value(s) matching last `CONF:` function, Gaussian noise applied |
| `MEAS:VOLT?` | PSU set-point ± 2 mV (or `0` if output OFF) |
| `MEAS:CURR?` | ≈ 8 % of current limit ± 1 mA (or `0` if output OFF) |
| `CHANn:DATA?` | 1 000-point 1 kHz sine wave ASCII |
| `A0` (CTS) | `A0 {actual} {setpoint}` — temperature ramps in real time |
| `C` (CTS) | `C 2.10;1.20;001;` (firmware identification) |

All noise is generated with the Box-Muller Gaussian transform (not uniform `NextDouble()`).

Responses can be overridden for individual commands via `SetResponse(command, value)`.

See [Simulation Mode](../user-guide/simulation-mode.md) in the User Guide for the full table of simulated values.

### P/Invoke Bindings

`VisaService` wraps the following NI-VISA C API functions:

| NI-VISA function | C# binding | Purpose |
|---|---|---|
| `viOpen` | `VisaNative.viOpen` | Open a resource session |
| `viClose` | `VisaNative.viClose` | Close a session |
| `viWrite` | `VisaNative.viWrite` | Write ASCII string |
| `viRead` | `VisaNative.viRead` | Read ASCII response |
| `viBufWrite` | `VisaNative.viBufWrite` | Write raw bytes |
| `viBufRead` | `VisaNative.viBufRead` | Read raw bytes |
| `viSetAttribute` | `VisaNative.viSetAttribute` | Set VISA attributes (timeout, etc.) |
| `viFindRsrc` | `VisaNative.viFindRsrc` | Find resources matching a pattern |
| `viFindNext` | `VisaNative.viFindNext` | Iterate resource list |

---

## PluginLoader

**File:** `src/InstrumentControl.Core/Services/PluginLoader.cs`

Discovers and instantiates instrument driver plugins at startup.

### Algorithm

```csharp
public class PluginLoader
{
    // Scan a directory for DLL files and instantiate all IInstrumentDriver implementations
    public IReadOnlyList<IInstrumentDriver> LoadPlugins(string pluginsDirectory)
    {
        var drivers = new List<IInstrumentDriver>();

        foreach (var dllPath in Directory.GetFiles(pluginsDirectory, "*.dll"))
        {
            var assembly = Assembly.LoadFrom(dllPath);

            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(IInstrumentDriver).IsAssignableFrom(type)) continue;
                if (type.IsAbstract || type.IsInterface) continue;
                if (type.GetConstructor(Type.EmptyTypes) is null) continue;

                var instance = (IInstrumentDriver)Activator.CreateInstance(type)!;
                drivers.Add(instance);

                // Side effect: loading the assembly triggers static constructors,
                // which register blocks in BlockRegistry.
            }
        }

        return drivers;
    }
}
```

### Block Registration Side Effect

When `Assembly.LoadFrom()` loads a plugin DLL, the .NET runtime triggers static constructors on all types that have them. Each block class has a static constructor that calls `BlockRegistry.Register(...)`. This is how blocks become available without any explicit registration call.

### No MEF Required

The `[Export(typeof(IInstrumentDriver))]` attribute seen in some drivers is left over from an earlier MEF-based approach. It has no effect on the current reflection-based loader. Do not use MEF APIs — they are not referenced.

---

## SequenceEngine

**File:** `src/InstrumentControl.Core/Services/SequenceEngine.cs`

Executes a sequence graph by walking the block chain.

### State Machine

```
Idle → Running → Completed
            ↓
          Paused ↔ Running
            ↓
          Error
            ↓
          Stopped (user cancel)
```

### Key Members

```csharp
public class SequenceEngine
{
    public SequenceState State { get; }

    // Start execution; returns when the sequence finishes or is cancelled
    public Task RunAsync(
        SequenceDefinition sequence,
        Dictionary<string, IInstrumentDriver> instruments,
        CancellationToken ct = default);

    // Pause execution after current block completes
    public void Pause();

    // Resume after a Pause
    public void Resume();

    // Events
    public event EventHandler<string>            BlockStarted;    // BlockId
    public event EventHandler<string>            BlockCompleted;  // BlockId
    public event EventHandler<string>            LogMessage;
    public event EventHandler<MeasurementResult> MeasurementReceived;
    public event EventHandler<SequenceState>     StateChanged;
}
```

### Execution Loop

```csharp
// Simplified pseudocode
var ctx = new SequenceContext(instruments, cancellationToken);
var current = FindStartBlock(sequence);

int iterations = 0;
while (current is not null)
{
    if (ct.IsCancellationRequested) break;

    if (_pauseSource is not null)
        await _pauseSource.Task;   // suspends here until Resume()

    StateChanged?.Invoke(this, SequenceState.Running);
    BlockStarted?.Invoke(this, current.BlockId);

    var result = await current.ExecuteAsync(ctx);

    BlockCompleted?.Invoke(this, current.BlockId);

    if (!result.Success)
    {
        LogMessage?.Invoke(this, $"ERROR in {current.DisplayName}: {result.ErrorMessage}");
        // Continue or abort based on result.NextBlockId
    }

    current = result.NextBlockId is not null
        ? ctx.AllBlocks[result.NextBlockId]
        : null;

    if (++iterations > 100_000)
        throw new InvalidOperationException("Sequence exceeded 100,000 iterations. Possible infinite loop.");
}
```

### Pause / Resume

Pause is implemented using a `TaskCompletionSource<bool>`. When `Pause()` is called, a new `TCS` is created. The execution loop `await`s `_pauseSource.Task` before each block. `Resume()` calls `_pauseSource.SetResult(true)` and nulls the source.

This approach uses zero CPU while paused (no polling).

### BlockRegistry

A static dictionary inside `SequenceEngine`:

```csharp
public static class BlockRegistry
{
    private static readonly Dictionary<string, Func<ISequenceBlock>> _factories = new();

    public static void Register(string blockType, Func<ISequenceBlock> factory)
        => _factories[blockType] = factory;

    public static ISequenceBlock Create(string blockType)
        => _factories.TryGetValue(blockType, out var f)
            ? f()
            : throw new KeyNotFoundException($"Unknown block type: {blockType}");

    // Returns one instance of each block registered by the given assembly
    public static IEnumerable<ISequenceBlock> GetBlocks(Assembly assembly)
        => _factories.Values
            .Select(f => f())
            .Where(b => b.GetType().Assembly == assembly);
}
```

---

## DataManager

**File:** `src/InstrumentControl.Core/Services/DataManager.cs`

Thread-safe storage for measurement results collected during a session.

```csharp
public class DataManager
{
    // Add a result (thread-safe; can be called from any thread)
    public void AddResult(MeasurementResult result);

    // Retrieve all results, optionally filtered by parameter name
    public IReadOnlyList<MeasurementResult> GetResults(string? parameterFilter = null);

    // Get distinct parameter names across all results
    public IReadOnlyList<string> GetParameterNames();

    // Export to CSV file
    // If append=true, rows are added to an existing file instead of overwriting
    public void ExportToCsv(string filePath, bool append = false, string? parameterFilter = null);

    // Remove all stored results
    public void Clear();

    // Fired on the thread that called AddResult, then marshalled to UI thread
    public event EventHandler<MeasurementResult> ResultAdded;
}
```

### Thread Safety

All access to `_results` is guarded by `lock (_results)`. `ResultAdded` is marshalled to the WPF dispatcher thread before firing so that ViewModels can update `ObservableCollection` properties without cross-thread exceptions.

### CSV Format

```
Timestamp,Instrument,Channel,Parameter,Value,Unit,Function
2026-06-16T10:23:45.123,HP34401A,,DCV,5.001234,V,DCV
2026-06-16T10:23:46.045,HP34401A,,DCV,5.002019,V,DCV
2026-06-16T10:23:47.001,CTSChamber,,Temperature,23.500000,°C,TEMP
```

- `Channel` is empty for single-channel instruments
- All timestamps are ISO 8601 with millisecond precision
- `Value` uses invariant culture (`.` decimal separator) for CSV portability
