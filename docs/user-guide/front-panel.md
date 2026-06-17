# Front Panel

The **Front Panel** tab displays the instrument-specific control UI provided by each plugin. It lets you control and monitor an instrument manually, without building a sequence.

---

## Overview

Each instrument plugin ships its own XAML front panel. The panel mirrors the physical instrument's controls and gives you interactive access to the most common functions.

When multiple instruments are connected, use the **instrument selector** (drop-down at the top of the Front Panel tab) to switch between them, or use the tabs that appear for each connected device.

---

## Connection toolbar

Above every front panel is a small toolbar with the instrument title, its resource string, and connection controls:

| Button | When shown | Action |
|---|---|---|
| **Disconnect** | While connected | Closes the connection to **this** instrument only — other connected instruments are unaffected. |
| **Reconnect** | While disconnected | Re-opens the connection to the same instrument (same resource/transport) and re-reads its identification. |
| **Reset** | While connected | Sends `*RST` / `*CLS` to return the instrument to its default state. |

Disconnecting from the front panel leaves the instrument in the sidebar list so you can reconnect it later; it does not remove the instrument. To remove it entirely, use the **✕** button in the sidebar.

---

## HP 34401A / Keithley 2000 — DMM Front Panel

The DMM front panel provides:

| Control | Description |
|---|---|
| **Measure** buttons | One button per function: DCV, ACV, DCI, ACI, Resistance (2W), Resistance (4W), Frequency, Period, Diode, Continuity |
| **Range** drop-down | Auto or fixed range (100 mV, 1 V, 10 V, 100 V, 1000 V for DCV) |
| **Reading display** | Shows the latest measured value with unit |
| **Continuous** toggle | When enabled, repeats the selected measurement every second |

Click a **Measure** button to trigger a single measurement. The result appears in the large reading display and is also logged in the **Log** tab.

---

## ITECH IT6922B — Power Supply Front Panel

| Control | Description |
|---|---|
| **Voltage set-point** | Text box or slider, 0 … 60 V |
| **Current limit** | Text box or slider, 0 … 5 A |
| **Output ON/OFF** | Toggle button to enable/disable the output |
| **Voltage readback** | Live display of actual output voltage |
| **Current readback** | Live display of actual output current |
| **Power readback** | Live display of actual power (V × A) |
| **OVP** | Over-voltage protection threshold (text box) |
| **OCP** | Over-current protection threshold (text box) |

Set the voltage and current limit first, then enable the output. The readback values update automatically when the output is enabled.

---

## R&S RTB2004 — Oscilloscope Front Panel

| Control | Description |
|---|---|
| **Channel enable** | Toggle checkboxes for CH1–CH4 |
| **Vertical range** | V/div selector per channel |
| **Coupling** | AC / DC / GND per channel |
| **Timebase** | Time/div selector |
| **Trigger** | Source channel, level, edge (rising/falling) |
| **Run / Stop / Single** | Acquisition mode buttons |
| **Autoscale** | Auto-adjusts vertical and horizontal scales |
| **Screenshot** | Captures the instrument screen as a PNG |

---

## Agilent 34970A — DAQ Front Panel

The 34970A front panel shows the installed cards and allows:

| Control | Description |
|---|---|
| **Card slot view** | Lists installed cards (34901A analog, 34907A digital) with their channel ranges |
| **Scan** | Starts a single scan across all configured channels |
| **Channel readings** | Grid showing the last measured value for each channel |
| **DAC outputs** | Set voltage on DAC channels (34907A card) |
| **Digital I/O** | Byte-level read/write for DIO ports |

---

## CTS Chamber — Environmental Chamber Front Panel

| Control | Description |
|---|---|
| **Set Temperature** | Target temperature in °C |
| **Set Ramp** | Ramp rate in °C/min |
| **Start / Stop / Pause** | Chamber program control |
| **Temperature readback** | Current chamber temperature |
| **Status display** | Chamber operating mode (Running, Idle, Error) |

---

## Rigol DS1054Z / DS1104Z — Oscilloscope Front Panel

| Control | Description |
|---|---|
| **Channel enable** | Toggle checkboxes for CH1–CH4 |
| **Vertical range** | V/div selector per channel |
| **Coupling** | DC / AC / GND per channel |
| **Probe ratio** | ×0.1 … ×1000 per channel |
| **BW limit** | OFF or 20 MHz per channel |
| **Invert** | Invert channel polarity |
| **Timebase** | Time/div selector, plus MAIN / XY / ROLL mode |
| **Trigger** | Mode (edge/pulse/slope/…), source, level, slope, sweep |
| **Acquire** | Normal / Averages / Peak Detect / Hi-Res |
| **Run / Stop / Single** | Acquisition mode buttons |
| **AutoScale** | Auto-adjusts vertical and horizontal scales |
| **Math / FFT** | MATH channel arithmetic and FFT analysis |

---

## Live Data Window

Every instrument also has a **Live Data Window** that floats independently. Open it from the **View** menu or by clicking the chart icon next to the instrument in the sidebar.

The Live Data Window contains:

- An **OxyPlot chart** plotting all measurements from that instrument over time (one series per parameter)
- A **data table** showing the raw result rows

The Live Data Window updates in real time during both manual front-panel measurements and sequence runs.
