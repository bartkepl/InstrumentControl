# Quick Start

Get from a fresh install to your first measurement in five minutes.

---

## Step 1 — Launch the Application

Run `InstrumentControl.exe` from the Start Menu shortcut or directly from the install folder.

On first launch you will see the main window with:

- A **left sidebar** showing the connected instruments list (empty at first)
- A **tab area** with four tabs: *Front Panel*, *Sequence Editor*, *Data / Charts*, *Log*
- A **toolbar** with Run / Pause / Stop buttons and sequence file controls
- A **status bar** at the bottom showing the VISA mode

---

## Step 2 — Connect an Instrument

1. Click **Add Instrument** (the `+` button in the left sidebar)
2. The **Connection Manager** window opens and scans for VISA resources automatically
3. Select a resource from the list — the app queries it with `*IDN?` and highlights the matching driver
4. If auto-detection succeeds, confirm the driver; otherwise choose one from the drop-down
5. Click **Connect**

The instrument now appears in the sidebar. Its name, model and resource address are shown.

!!! tip "No hardware?"
    If NI-VISA is not installed, the app runs in **simulation mode**. You can still add a simulated instrument: click **Add Instrument**, choose **Simulated** from the resource list, select any driver, and connect. The driver will return fake values so you can test sequences without physical hardware.

---

## Step 3 — Use the Front Panel

Switch to the **Front Panel** tab. Each plugin provides its own instrument-specific UI:

- For DMMs (HP 34401A, Keithley 2000): voltage/current/resistance measurement buttons, range selector, reading display
- For a power supply (ITECH IT6922B): voltage and current set-point sliders, output on/off toggle, live readback
- For the oscilloscope (R&S RTB2004): channel enable, timebase, trigger controls, waveform screenshot
- For the CTS Chamber: temperature set-point, ramp rate, start/stop/pause buttons

Click the controls to send commands to the real instrument (or see simulated responses in simulation mode).

---

## Step 4 — Build a Simple Sequence

Switch to the **Sequence Editor** tab. A blank canvas contains a **Start** block.

### Add Blocks

Drag blocks from the **block toolbox** on the left onto the canvas:

1. Drag a measurement block (e.g. `HP34401A_MeasureDCV`) onto the canvas
2. Drag a **WaitBlock** onto the canvas
3. Drag an **EndBlock** onto the canvas

### Connect Blocks

Click and drag from the **green output dot** on the right side of one block to the **input dot** on the left side of the next block. Connect:

```
[Start] → [MeasureDCV] → [Wait] → [End]
```

### Set Block Properties

Double-click a block to open its property editor:

- **MeasureDCV**: set *Instrument* (choose the connected DMM), *Range* (e.g. `10`), *Variable Name* (e.g. `voltage`)
- **Wait**: set *Delay (ms)* to `500`

---

## Step 5 — Run the Sequence

Click **Run** (▶) in the toolbar. Watch:

- The currently-executing block is highlighted on the canvas
- Measurement values appear in the **Log** tab
- Results stream into the **Data / Charts** tab in real time

Click **Stop** (■) to abort at any time, or **Pause** (⏸) and **Resume** to suspend execution.

---

## Step 6 — View and Export Results

Switch to the **Data / Charts** tab:

- The **chart** plots all measurements over time
- The **data table** below shows every result row (timestamp, instrument, parameter, value, unit)
- Click **Export CSV** to save the full result set

Results are automatically accumulated across multiple sequence runs until you click **Clear Data**.

---

## Next Steps

- [Connection Manager](connection-manager.md) — detailed guide to VISA resource discovery
- [Sequence Editor](sequence-editor.md) — loops, conditions, variables, and more
- [Block Reference](blocks-reference.md) — every built-in block explained
- Individual instrument guides in the sidebar for instrument-specific details
