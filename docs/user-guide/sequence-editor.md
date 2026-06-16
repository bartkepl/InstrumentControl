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
| **Toolbar** | Run ▶, Pause ⏸, Stop ■, New, Open, Save |

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

Click **Run** (▶) or press `F5`. Execution starts at the **StartBlock** and follows the connection arrows.

The currently-running block is highlighted. The **Log** tab shows step-by-step progress messages.

### Pausing and Resuming

Click **Pause** (⏸) or press `F7`. Execution suspends after the current block finishes. Click **Resume** (▶) or press `F5` again to continue.

### Stopping

Click **Stop** (■) or press `F8`. The current block is interrupted and the sequence ends. If a block was in the middle of a VISA command, the command is cancelled.

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

Sequences are stored as **JSON files** (`.seq`):

- **Save** — click the floppy-disk icon or press `Ctrl+S`; choose a file path
- **Open** — click the folder icon or press `Ctrl+O`; browse to a `.seq` file
- **New** — click the document icon or press `Ctrl+N`; clears the canvas and starts a fresh sequence

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
