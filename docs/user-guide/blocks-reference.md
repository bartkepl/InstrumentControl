# Block Reference

This page describes every **built-in sequence block** available in InstrumentControl. Instrument-specific blocks (e.g. `HP34401A_MeasureDCV`) are documented in the individual instrument pages.

---

## Control Flow

### StartBlock

| Field | Value |
|---|---|
| **Category** | Control |
| **Color** | Green |

Every sequence must begin with exactly one `StartBlock`. It has no properties and performs no action — it is simply the execution entry point. Place it at the top-left of the canvas.

---

### EndBlock

| Field | Value |
|---|---|
| **Category** | Control |
| **Color** | Red |

Terminates the sequence. When execution reaches an `EndBlock`, the run is marked as **Completed**. A sequence can have multiple `EndBlock` nodes (e.g. one for a normal exit and one for an error path).

---

### WaitBlock

| Field | Value |
|---|---|
| **Category** | Control |
| **Color** | Gray |

Pauses execution for a fixed duration.

| Property | Type | Description |
|---|---|---|
| **Delay (ms)** | Number | Milliseconds to wait. Minimum 0, no upper limit. |

**Tip:** Use `WaitBlock` between measurements to allow instruments to settle, or between loop iterations to control the sampling rate.

---

### LoopBlock

| Field | Value |
|---|---|
| **Category** | Control |
| **Color** | Orange |

Repeats a body sub-sequence a fixed number of times.

| Property | Type | Description |
|---|---|---|
| **Iterations** | Number | Number of times to execute the body. |
| **Counter Variable** | Variable | Optional. If set, the current iteration index (0-based) is stored in this variable each iteration. |
| **Delay Between Iterations (ms)** | Number | Optional delay inserted between each iteration. Defaults to 0. |

**Outputs:**
- **Body** (green) — connects to the first block inside the loop body
- **Next** (blue) — connects to the block after the loop

The loop body must eventually connect back to the `LoopBlock`'s input — the engine handles the repeat automatically; you do not need to wire the last body block back.

---

### ConditionBlock

| Field | Value |
|---|---|
| **Category** | Control |
| **Color** | Purple |

Branches execution based on a comparison between a variable and a value.

| Property | Type | Description |
|---|---|---|
| **Variable** | Variable | The variable to compare. |
| **Operator** | Combo | `==`, `!=`, `>`, `<`, `>=`, `<=` |
| **Value** | Text | The value to compare against. Numeric strings are compared numerically. |

**Outputs:**
- **True** (green) — taken when the condition is satisfied
- **False** (red) — taken when the condition is not satisfied

---

## Data & Variables

### SetVariableBlock

| Field | Value |
|---|---|
| **Category** | Data |
| **Color** | Teal |

Assigns a value to a named variable.

| Property | Type | Description |
|---|---|---|
| **Variable Name** | Variable | Name of the variable to set. |
| **Value** | Text | The value to assign. If the string parses as a number, it is stored as `double`; otherwise as `string`. |

---

### MathBlock

| Field | Value |
|---|---|
| **Category** | Data |
| **Color** | Cyan |

Evaluates a mathematical expression and stores the result in a variable.

| Property | Type | Description |
|---|---|---|
| **Expression** | Text | Math expression. Use `{varname}` to substitute variable values. |
| **Result Variable** | Variable | Variable that receives the computed result (always a `double`). |

**Available functions:**

| Function | Description |
|---|---|
| `sqrt(x)` | Square root |
| `abs(x)` | Absolute value |
| `pow(x, y)` | x raised to the power y |
| `sin(x)`, `cos(x)`, `tan(x)` | Trigonometric functions (radians) |
| `log(x)` | Natural logarithm |
| `round(x)` | Round to nearest integer |
| `min(x, y)`, `max(x, y)` | Minimum / maximum |
| `clamp(x, lo, hi)` | Clamp x between lo and hi |
| `deg2rad(x)` | Degrees to radians |
| `hypot(x, y)` | Euclidean distance √(x²+y²) |

**Example expressions:**

```
{voltage} * 1000          # Convert V to mV
sqrt({r_squared})         # Square root
clamp({temp}, 20, 85)     # Limit temperature to 20–85 °C
pow({base}, 2) + {offset} # Quadratic
```

---

## Logging

### LogMessageBlock

| Field | Value |
|---|---|
| **Category** | Logging |
| **Color** | Yellow |

Writes a message to the sequence log (visible in the **Log** tab).

| Property | Type | Description |
|---|---|---|
| **Message** | Text | Message to log. Supports `{varname}` substitution. |

---

## File Output

### SaveCsvBlock

| Field | Value |
|---|---|
| **Category** | Output |
| **Color** | Blue |

Exports all accumulated measurement results to a CSV file.

| Property | Type | Description |
|---|---|---|
| **File Path** | FilePath | Absolute or relative path to the output `.csv` file. |
| **Append** | CheckBox | If checked, new rows are appended to an existing file. If unchecked, the file is overwritten. |
| **Parameter Filter** | Text | Optional. If set, only results whose `ParameterName` matches this string are exported. Leave blank to export all. |

**CSV format:**

```
Timestamp,Instrument,Channel,Parameter,Value,Unit,Function
2026-06-16T10:23:45.123,HP34401A,,DCV,5.001234,V,DCV
```

---

## Visualization

### AddToChartBlock

| Field | Value |
|---|---|
| **Category** | Visualization |
| **Color** | Magenta |

Adds a value to the live chart in the **Data / Charts** tab.

| Property | Type | Description |
|---|---|---|
| **Variable** | Variable | The variable whose current value to plot. |
| **Series Name** | Text | The name of the chart series. Multiple blocks with the same name plot onto the same series. |
| **X Variable** | Variable | Optional. If set, uses this variable as the X axis value instead of the timestamp. |

---

## Instrument Blocks

Each instrument plugin registers its own measurement blocks. They appear in the block toolbox under the instrument's name. See the individual instrument pages for details:

- [HP 34401A blocks](instruments/hp34401a.md#sequence-blocks)
- [Agilent 34970A blocks](instruments/agilent34970a.md#sequence-blocks)
- [Keithley 2000 blocks](instruments/keithley2000.md#sequence-blocks)
- [ITECH IT6922B blocks](instruments/itech-it6922b.md#sequence-blocks)
- [R&S RTB2004 blocks](instruments/rtb2004.md#sequence-blocks)
- [CTS Chamber blocks](instruments/cts-chamber.md#sequence-blocks)
