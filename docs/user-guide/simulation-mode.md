# Simulation Mode

Simulation mode lets you develop and test sequences **without any physical hardware or NI-VISA installation**. Every instrument driver returns physically plausible values with a small amount of Gaussian noise so sequences behave realistically.

---

## When is Simulation Mode Active?

Simulation mode activates automatically in two cases:

1. **NI-VISA not installed** — `visa64.dll` is not found in the standard NI installation path; the application falls back to simulation for all VISA resources.
2. **Explicit checkbox** — the **Use Simulation** checkbox in the Connection Manager is checked; this forces simulation even when NI-VISA is present.

The status bar at the bottom of the main window shows **`VISA: Simulation`** when active.

---

## Connecting a Simulated Instrument

1. Click **Add Instrument** (`+`) in the left sidebar
2. Click **Refresh** — the resource list shows one simulated entry per supported instrument type:

    | Resource | Intended driver |
    |---|---|
    | `SIM::GPIB0::22::INSTR (HP34401A)` | HP 34401A |
    | `SIM::GPIB0::10::INSTR (Keithley2000)` | Keithley 2000 |
    | `SIM::GPIB0::09::INSTR (Agilent34970A)` | Agilent 34970A |
    | `SIM::GPIB0::11::INSTR (ItechIT6922B)` | ITECH IT6922B |
    | `SIM::TCPIP0::192.168.0.100::INSTR (RTB2004)` | R&S RTB2004 |
    | `SIM::TCPIP0::192.168.0.101::INSTR (RigolDS1000Z)` | Rigol DS1000Z |
    | `SIM::COM3 (CTSChamber)` | CTS Chamber |

3. Select a resource, choose the matching driver, and click **Connect**

!!! tip
    Any `SIM::` resource works with any driver. The suggested pairings above are just for clarity. You can also type a custom resource string (e.g. `SIM::INSTR`) in the **Manual Resource** field.

---

## Simulated Values per Instrument

### Multimeters — HP 34401A, Keithley 2000

The simulated DMM tracks the last `CONF:` command and returns a realistic value for each function.

| Function | Command | Simulated value |
|---|---|---|
| DC Voltage | `CONF:VOLT:DC` | 3.300 V ± 1 mV |
| AC Voltage | `CONF:VOLT:AC` | 1.500 V ± 5 mV |
| DC Current | `CONF:CURR:DC` | 125 mA ± 0.2 mA |
| AC Current | `CONF:CURR:AC` | 80 mA ± 0.2 mA |
| 2-wire Resistance | `CONF:RES` | 1 000 Ω ± 0.5 Ω |
| 4-wire Resistance | `CONF:FRES` | 1 000 Ω ± 0.5 Ω |
| Frequency | `CONF:FREQ` | 50.00 Hz ± 0.01 Hz |
| Period | `CONF:PER` | 20.00 ms ± 1 µs |
| Diode | `CONF:DIOD` | 652 mV ± 1 mV |
| Continuity | `CONF:CONT` | 5.2 Ω ± 0.1 Ω |
| Temperature (TC) | `CONF:TEMP` | 25.0 °C ± 0.1 °C |

Burst measurements (`SAMP:COUN N` + `FETCH?`) return *N* independent samples, each with its own noise draw.

---

### Data Acquisition — Agilent 34970A

The scan engine uses the same function-to-value table as the DMMs above. When a `ROUT:SCAN (@...)` command is received, the simulator counts the number of channels in the scan list. A subsequent `FETCH?` returns exactly that many comma-separated values, one per channel, using the currently configured function.

**Example:** scanning channels 101–103 after `CONF:VOLT:DC` → `FETCH?` returns:

```
+3.29987,+3.30101,+3.29954
```

---

### DC Power Supply — ITECH IT6922B

The simulator maintains an internal voltage set-point and current limit. Measurement results reflect the configured values:

| Query | Returned value |
|---|---|
| `VOLT?` | Voltage set-point (last `VOLT x` command) |
| `CURR?` | Current limit (last `CURR x` command) |
| `OUTP?` | `1` if output ON, `0` if OFF |
| `MEAS:VOLT?` | Set-point ± 2 mV when output is ON; `0` when OFF |
| `MEAS:CURR?` | ≈ 8 % of current limit ± 1 mA when ON; `0` when OFF |
| `MEAS:POW?` | Simulated V × I product |
| `STAT:OPER:COND?` | `1` (CV mode always assumed) |

!!! note "Simulated load"
    The 8 % current draw models a modest resistive load on the output. There is no simulated short-circuit or CC-mode transition.

OVP and OCP levels are stored and returned correctly by `VOLT:PROT?`, `CURR:PROT:LEV?`, and their `STAT?` companions.

---

### Oscilloscopes — R&S RTB2004, Rigol DS1000Z

**Parametric measurements** — `MEASn:RES:ACT?`:

| Measurement type | Simulated value |
|---|---|
| Frequency | 1 000.0 Hz ± 0.5 Hz |
| Period | 1.000 ms ± 100 ns |
| Amplitude / Pk-Pk | 2.00 V ± 10 mV |
| RMS | 707 mV ± 3 mV |
| Mean / Vavg | 0 V ± 2 mV |
| Rise / Fall time | 10 ns ± 0.5 ns |
| Duty cycle | 50.0 % ± 0.05 % |
| Phase | 0.0 ° ± 0.1 ° |

**Waveform readout** — `CHANn:DATA?`:

Returns 1 000 ASCII-encoded samples of a **1 kHz sine wave** with amplitude ≈ 1 V plus small noise (σ = 5 mV). The time axis is derived from the current timebase setting (`TIM:SCAL`):

```
Time span = TIM:SCAL × 10  (one full acquisition window)
```

The waveform header (`CHANn:DATA:HEAD?`) is computed to match:

```
−half_span, +half_span, 1000, 1
```

Channel scale (`CHANn:SCAL`) and timebase values are stored and returned correctly by their `?` queries.

---

### Environmental Chamber — CTS T-40/50

The chamber simulator maintains a continuously-updated **actual temperature** that tracks the set-point according to the configured ramp rates. This means sequences using `CTS_WaitForTemperature` will actually wait for the simulated temperature to converge.

| Property | Default |
|---|---|
| Initial temperature | 23.5 °C |
| Default set-point | 25.0 °C |
| Ramp-up rate | 3 K/min |
| Ramp-down rate | 3 K/min |
| Noise on each read | ± 0.02 °C |

**Temperature dynamics:** Every `A0` query (read temperature) triggers an update. The actual temperature moves toward the set-point at the configured rate based on real elapsed wall-clock time between queries. If the chamber is **not running** (before `CTS_ChamberStart`), the temperature drifts slightly around its current value but does not track the set-point.

**Example timeline** with default 3 K/min ramp and set-point changed to 85 °C:

```
t=0 s   CTS_SetTemperature → 85 °C
t=0 s   ReadTemperature → 23.5 °C  (chamber just started)
t=60 s  ReadTemperature → 26.5 °C  (+3 K after 1 min)
t=600 s ReadTemperature → 53.5 °C  (+30 K after 10 min)
t=1233 s ReadTemperature → 85.0 °C (set-point reached)
```

The simulated chamber reports **running** (`O` → `O101`) and **ramp active** (`R0` → `R0 11 ...`) once `CTS_ChamberStart` is executed.

---

## How Noise is Generated

All noise values use the **Box-Muller transform** to produce a Gaussian (normal) distribution:

```csharp
double u1 = 1.0 - rng.NextDouble();
double u2 = 1.0 - rng.NextDouble();
double noise = sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
```

This gives realistic measurement scatter — occasional outliers included — rather than the flat uniform distribution that a plain `NextDouble()` would produce.

---

## Limitations

- **No CV/CC transition** for the power supply — the output is always in CV mode in simulation.
- **No fault conditions** — no OVP/OCP trip, no instrument error states.
- **No waveform math** — oscilloscope `MATH` channel queries return generic values.
- **No multi-instrument coupling** — e.g., the power supply's simulated output voltage does not affect a simulated DMM connected in parallel.
- **Single-instance state** — each `SimulatedConnectionProvider` holds its own state. Two simulated connections to the same `SIM::GPIB0::22::INSTR` resource string are independent.
