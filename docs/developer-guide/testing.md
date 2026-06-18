# Testing & CI

Automated tests cover the **UI-independent core logic** — the parts that are deterministic and don't need a real instrument or a WPF dispatcher. They run locally and gate every CI build.

---

## Test project

```
tests/InstrumentControl.Core.Tests/   # xUnit, net10.0-windows
```

The project references only `InstrumentControl.Core`, so it builds fast and has no plugin/UI dependencies. It is part of `InstrumentControl.sln`.

| Test file | What it covers |
|---|---|
| `SequenceEngineTests.cs` | Engine state machine: empty sequence, minimal Start→End run, `Stop` cancellation, Pause/Resume during a loop |
| `LoopBlockTests.cs` | `LoopBlock`: fixed iteration count + counter variable, infinite loop stopped by cancellation, pause gate halts iteration |
| `SequenceContextTests.cs` | Variables round-trip, the pause gate (`SetPaused` / `WaitIfPausedAsync`), and cancellation while paused |
| `InstrumentDriverBaseTests.cs` | `*IDN?` parsing (firmware/serial) and the Disconnect → Reconnect round-trip |

---

## Running the tests

```powershell
# From the repository root
dotnet test tests/InstrumentControl.Core.Tests
```

All tests are expected to pass with no failures and no skips.

---

## Continuous integration

The GitHub Actions workflow `.github/workflows/release.yml` runs on every push and pull request to `main`. The **test step runs before the build and publish steps**:

```yaml
- name: Run tests
  run: dotnet test tests/InstrumentControl.Core.Tests --nologo -v q
```

If any test fails, the job stops there — no artifact, Velopack package, or GitHub Release is produced. This makes a green test run a hard prerequisite for a release.

---

## Writing new tests

- Keep tests free of WPF/dispatcher dependencies so they stay in `InstrumentControl.Core.Tests` and run in CI. Logic that needs the UI thread belongs in the App project and is not unit-tested here.
- For driver/connection behavior, implement a lightweight in-memory `IConnectionProvider` (see the `FakeConnection` helper in `InstrumentDriverBaseTests.cs`) instead of touching real VISA. The `SimulatedConnectionProvider` in `VisaService` is also available for higher-level scenarios.
- Async tests should be `async Task` and `await` the operation rather than blocking, to avoid deadlocks (xUnit flags blocking waits).
- Register block types before use — block classes self-register in a static constructor, so referencing the type once (e.g. `_ = new LoopBlock();`) ensures `BlockRegistry` knows about it.
