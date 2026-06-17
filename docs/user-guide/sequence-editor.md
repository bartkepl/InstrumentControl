# Sequence Editor

The Sequence Editor lets you build automated measurement programs by connecting **blocks** on a visual canvas — no code required.

---

## Canvas Overview

The canvas occupies the main area of the **Sequence Editor** tab.

| Area | Description |
|---|---|
| **Block toolbox** (left panel) | Lists all available blocks, grouped by category and instrument |
| **Canvas** (centre) | Infinite drag-and-drop surface where you arrange blocks |
| **Properties panel** (right) | Opens when you double-click a block; shows editable fields |
| **Editor toolbar** | New, Open, Save — sequence-file actions only |

!!! note "Run / Pause / Stop live in the main toolbar"
    Execution is controlled from the **global toolbar at the top of the main window** (Run ▶, Pause/Resume ⏸, Stop ■), not from the Sequence Editor tab. This keeps a single source of truth for sequence control regardless of which tab is active. The editor's own toolbar carries only the file actions (New / Open / Save).

---

## Working with Blocks

### Adding Blocks

Drag a block from the **block toolbox** on the left onto the canvas. Alternatively, right-click the canvas and choose *Add Block* from the context menu.

### Moving Blocks

Left-click and drag a block to reposition it. Blocks snap to a grid by default.

### Connecting Blocks

Every block has:

- An **input dot** on the left side (where execution enters this block)
- An **output dot** on the right side (where execution continues)

Click and drag from an **output dot** to the **input dot** of the next block to create a connection arrow. The arrow represents the execution flow.

Special blocks have multiple outputs:

- **ConditionBlock** has a *True* output and a *False* output
- **LoopBlock** has a *Body* output (executed each iteration) and a *Next* output (continues after the loop completes)

### Deleting Connections

Right-click a connection arrow and choose *Delete Connection*.

### Editing Block Properties

Double-click a block to open its **property editor** in the right panel. Fields vary by block type. Press Enter or click outside the field to confirm a change.

### Deleting Blocks

Right-click a block and choose *Delete*, or select it and press **Delete**.

---

## Sequence Execution

### Running a Sequence

Click **Run** (▶) in the main toolbar. Execution starts at the **StartBlock** and follows the connection arrows.

The currently-running block is highlighted. The **Log** tab shows step-by-step progress messages. Block highlighting is refreshed on a timer rather than per-block, so the window **stays responsive even during tight loops** with thousands of zero-delay iterations — you can still scroll, switch tabs, and click Stop.

### Pausing and Resuming

Click **Pause** (⏸). Execution suspends and the status changes to *Paused*; the button becomes **Resume** (▶) — click it to continue from the same point.

Pause takes effect **mid-loop**: the engine checks a pause gate on every loop iteration (and before each block in a loop body), so a long `LoopBlock` halts within one iteration instead of running to completion first.

### Stopping

Click **Stop** (■). The current block is interrupted and the sequence ends. If a block was in the middle of a VISA command, the command is cancelled. Stop also works while the sequence is paused.

### Execution Limits

The sequence engine enforces a **100,000-iteration safety limit** to prevent infinite loops. If a loop runs beyond this limit, the sequence stops with an error in the Log.

---

## Variables

Variables are named values that persist for the lifetime of a single sequence run. They allow blocks to pass data to each other.

### Setting a Variable

Use a **SetVariableBlock** or a measurement block's *Variable Name* property. Variable names are case-insensitive.

### Reading a Variable

Blocks that accept a *Variable Name* input read from the current value at run time. The **MathBlock** also reads variables using the `{varname}` syntax in expressions.

### Variable Types

Variables hold either a `double` (number) or a `string`. If a block writes a numeric string (e.g. `"3.14"`), subsequent blocks that expect a number will auto-parse it.

---

## Saving and Loading Sequences

Sequences are stored as **JSON files** (`.iseq`):

- **Save** — click the floppy-disk icon; on first save you choose a file path, after that it overwrites
- **Open** — click the folder icon; browse to an `.iseq` file
- **New** — click the document icon; clears the canvas and starts a fresh sequence (a StartBlock is added automatically)

The JSON file stores block positions, types, properties, and connection IDs. It is human-readable and can be edited in any text editor if needed.

---

## Typical Sequence Patterns

### Simple Measurement

```
[Start] → [Wait 500ms] → [MeasureDCV] → [AddToChart] → [End]
```

### Repeated Measurement with Loop

```
[Start] → [Loop N=100] 
               ↓ (body)
          [MeasureDCV] → [Wait 1000ms] → (back to loop)
               ↓ (next, after loop)
          [SaveCsvBlock] → [End]
```

### Conditional Action

```
[Start] → [MeasureDCV] → [SetVariable voltage={result}]
       → [Condition: voltage > 5.0]
              ↓ True              ↓ False
         [LogMessage "HIGH"]  [LogMessage "LOW"]
              ↓                    ↓
         [End]               [End]
```

### Temperature Sweep with CTS Chamber

```
[Start] 
→ [ChamberStart]
→ [Loop temperatures: 25, 40, 60, 85°C]
    ↓ (body)
    [SetTemperature]
    → [WaitForTemperature ±0.5°C]
    → [MeasureDCV]
    → [SaveCsv]
    → (next loop iteration)
→ [ChamberStop]
→ [End]
```
