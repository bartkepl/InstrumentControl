# Keithley 2000 — 6½-digit Multimeter

## Overview

The Keithley 2000 is a high-accuracy 6½-digit multimeter with a SCPI command set similar to the HP 34401A.

| Specification | Value |
|---|---|
| Type | 6½-digit DMM |
| Interface | GPIB, USB-TMC |
| Driver name | `Keithley2000` |
| Resource patterns | `GPIB?*::?*::INSTR`, `USB?*::?*::INSTR` |

---

## Connecting

1. Connect the Keithley 2000 via GPIB or USB
2. Click **Add Instrument** and select the resource
3. Auto-detection parses the `*IDN?` response (`KEITHLEY INSTRUMENTS INC.,MODEL 2000`)
4. Click **Connect**

---

## Front Panel

The Keithley 2000 front panel is identical in layout to the [HP 34401A front panel](hp34401a.md#front-panel) — it provides the same set of measurement buttons, range selector, and continuous mode toggle.

---

## Sequence Blocks

The Keithley 2000 provides the same eight measurement blocks as the HP 34401A. All blocks share the same properties: **Instrument**, **Range**, **Variable Name**.

| Block | Measurement | Unit |
|---|---|---|
| `Keithley2000_MeasureDCV` | DC Voltage | V |
| `Keithley2000_MeasureACV` | AC Voltage (RMS) | V |
| `Keithley2000_MeasureDCI` | DC Current | A |
| `Keithley2000_MeasureACI` | AC Current (RMS) | A |
| `Keithley2000_MeasureResistance2W` | 2-wire Resistance | Ω |
| `Keithley2000_MeasureResistance4W` | 4-wire Resistance | Ω |
| `Keithley2000_MeasureFrequency` | Frequency | Hz |
| `Keithley2000_MeasurePeriod` | Period | s |

---

## SCPI Commands

The driver sends standard IEEE 488.2 / SCPI commands compatible with the Keithley 2000 command reference:

| Action | Command |
|---|---|
| Configure DCV | `CONF:VOLT:DC {range},DEF` |
| Configure ACV | `CONF:VOLT:AC {range},DEF` |
| Trigger + read | `READ?` |
| Identification | `*IDN?` |
| Reset | `*RST` |
