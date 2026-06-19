# Testing & CI

Automated tests cover the **UI-independent logic** — everything except the WPF view layer. They run locally and gate every CI build, and a coverage report + badge are produced on every push.

---

## Test projects

```
tests/InstrumentControl.Core.Tests/          # xUnit, net10.0-windows — Core library
tests/InstrumentControl.Instruments.Tests/   # xUnit, net10.0-windows — all 7 instrument plugins
```

Both are part of `InstrumentControl.sln`.

### `InstrumentControl.Core.Tests`

References only `InstrumentControl.Core`.

| Area | What it covers |
|---|---|
| `SequenceEngineTests` | Engine state machine: empty sequence, Start→End run, `Stop` cancellation, Pause/Resume |
| `LoopBlockTests` | Fixed iteration count + counter, infinite loop stopped by cancellation, pause gate |
| `BuiltInBlocksTests` | Start/End/EndLoop/Wait/LogMessage/SetVariable/Condition/AddToChart/SaveCsv execution and routing |
| `MathBlockTests` | The recursive-descent math evaluator: operators, precedence, functions, constants, variables, error paths |
| `DataManagerTests` | Thread-safe storage, CSV export (header/append/escaping), series and parameter queries |
| `SimulatedConnectionProviderTests` | Simulated transport for DMM, power supply, oscilloscope and chamber commands |
| `VisaServiceTests` | Simulation-mode resource discovery and session creation (no NI-VISA needed) |
| `PluginLoaderTests` | Reflection-based driver discovery, `CreateDriver`, metadata |
| `ModelsTests` / `SequenceBlockBaseTests` / `BlockRegistryTests` | Models, serialization/clone, the block registry |
| `InstrumentDriverBaseTests` / `InstrumentDriverBaseExtraTests` | `*IDN?` parsing, connect/disconnect/reconnect, reset, query helpers |

### `InstrumentControl.Instruments.Tests`

References `InstrumentControl.Core` and all seven plugin projects. It verifies the exact SCPI strings each driver generates, using a recording fake connection (`RecordingConnection`) that captures every written command and returns canned query responses.

| Area | What it covers |
|---|---|
| `HP34401ADriverTests` / `Keithley2000DriverTests` | DMM `CONF`/`SENS`/`READ?`, config cache, MATH, limit test, burst, display |
| `ItechIT6922BDriverTests` | Power-supply set/measure, OVP/OCP, operating-mode decoding |
| `RTB2004DriverTests` / `RigolDS1000ZDriverTests` | Scope channel/timebase/trigger/acquire, measurements, waveform parsing, IEEE binary block, source normalization |
| `Agilent34970ADriverTests` / `Agilent34970ACardsTests` | Card detection, mixed scans, DAC/DIO/totalizer, and the 34901A/34907A card model |
| `CTSChamberDriverTests` | Chamber protocol over the simulation fast-path (temperature, ramps, start/stop, state) |
| `InstrumentBlocksTests` | Metadata of every plugin block + execution of every sequence block against a connected driver |

---

## Running the tests

```powershell
# Everything
dotnet test

# A single project
dotnet test tests/InstrumentControl.Core.Tests
```

All tests are expected to pass with no failures and no skips.

---

## Coverage

Coverage is collected with **coverlet** and rendered with **ReportGenerator**.

- Configuration lives in `coverlet.runsettings`: it includes the eight product assemblies (Core + 7 plugins) and **excludes the WPF view layer** (`*/Views/`, `*.xaml.cs`, `LiveDataWindow`) plus auto-property accessors, so the metric reflects testable logic only.
- ReportGenerator is pinned as a local tool in `.config/dotnet-tools.json`.

Generate an HTML report locally:

```powershell
dotnet tool restore
dotnet test tests/InstrumentControl.Core.Tests        --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/core
dotnet test tests/InstrumentControl.Instruments.Tests --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/instruments
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:"Html;TextSummary"
# open coveragereport/index.html
```

!!! warning "Use separate result directories"
    Run each test project into its **own** `--results-directory`. Pointing both runs at the same directory can intermittently produce one empty Cobertura file, silently dropping that project's coverage. ReportGenerator then merges `TestResults/**/coverage.cobertura.xml`.

---

## Continuous integration

Two workflows run on every push and pull request to `main`:

### `release.yml`

The **test step runs before build and publish**:

```yaml
- name: Run tests
  run: dotnet test tests/InstrumentControl.Core.Tests --nologo -v q
```

If a test fails, no artifact, Velopack package, or GitHub Release is produced — a green test run is a hard prerequisite for a release.

### `coverage.yml`

Runs both test projects on `windows-latest`, merges coverage, posts a Markdown summary to the run, and uploads the full HTML report as an artifact. On pushes to `main` it publishes the report and the coverage badge to the dedicated **`badges`** branch using the built-in `GITHUB_TOKEN` (no external service or secret required). The badge in the project README points at the raw SVG on that branch.

---

## Writing new tests

- Keep tests free of WPF/dispatcher dependencies. Logic that needs the UI thread belongs in the App project and is not unit-tested here.
- For driver/connection behavior use `RecordingConnection` (instruments tests) or a lightweight `IConnectionProvider` such as `FakeConnection` (Core tests); the `SimulatedConnectionProvider` in `VisaService` covers higher-level scenarios. Never touch real VISA in a test.
- The CTS driver opens a real COM port unless the connection reports `ConnectionType == "SIMULATION"` — set that on the fake connection to exercise the simulation fast-path.
- Async tests should be `async Task` and `await` the operation rather than blocking (xUnit flags blocking waits).
- Block classes self-register in a static constructor, so referencing the type once (e.g. `_ = new LoopBlock();`) ensures `BlockRegistry` knows about it.
- Numeric SCPI arguments are formatted with `InvariantCulture`, so `1e-3` serializes as `0.001` (not `1E-03`).
