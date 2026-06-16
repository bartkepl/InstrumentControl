# Agilent 34970A — Data Acquisition / Switch Unit

## Overview

The Agilent 34970A is a modular data acquisition and switching unit that accepts plug-in cards for analog measurement, digital I/O, and DAC output.

| Specification | Value |
|---|---|
| Type | DAQ / Switch Unit |
| Interface | GPIB, USB-TMC |
| Cards supported | 34901A (20-ch analog mux), 34907A (DIO + DAC) |
| Driver name | `Agilent34970A` |
| Resource patterns | `GPIB?*::?*::INSTR`, `USB?*::?*::INSTR` |

---

## Supported Cards

### 34901A — 20-Channel Multiplexer (Analog Input)

- 20 channels: `(@101)` … `(@120)`
- Measures: DC voltage, AC voltage, 2W resistance, 4W resistance, DC current, AC current, temperature (thermocouple / RTD)
- Two-wire and four-wire measurement modes
- Maximum 250 V (channel-to-earth)

### 34907A — Digital I/O + DAC Module

- Two 8-bit digital I/O ports (A and B)
- Two DAC channels with 16-bit resolution, 0–12 V range
- Digital channels: `(@201)` … `(@216)`
- DAC channels: `(@221)`, `(@222)`

---

## Connecting

1. Power on the 34970A with cards installed
2. Click **Add Instrument**
3. Select the GPIB or USB resource
4. Auto-detection reads `*IDN?` and matches the driver
5. Click **Connect**

The driver queries the installed cards and populates the front panel with the detected configuration.

---

## Front Panel

| Control | Description |
|---|---|
| **Card Slot View** | Shows installed cards and available channels |
| **Scan** | Triggers a single scan across all configured channels |
| **Channel Readings** | Grid of latest readings per channel |
| **DAC Output (34907A)** | Voltage set-point for DAC1 and DAC2 |
| **Digital I/O (34907A)** | Byte-level read/write for Port A and Port B |

---

## Sequence Blocks

### Agilent34970A_ScanChannels

Scans a list of channels and stores results.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected 34970A instance |
| **Channels** | Text | Channel list, e.g. `(@101:110,201)` |
| **Function** | Combo | VOLT:DC, VOLT:AC, RES, FRES, CURR:DC, CURR:AC, TEMP |
| **Variable Prefix** | Text | Variables `{prefix}_101`, `{prefix}_102`, … receive channel readings |

### Agilent34970A_MeasureVoltage

Measures DC voltage on a single channel.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected 34970A instance |
| **Channel** | Number | Channel number, e.g. `101` |
| **Range** | Number | 0 = Auto |
| **Variable** | Variable | Receives the measured voltage in V |

### Agilent34970A_MeasureTemperature

Measures temperature via thermocouple or RTD.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected 34970A instance |
| **Channel** | Number | Channel number |
| **Transducer** | Combo | TC (thermocouple) or RTD |
| **TC Type** | Combo | K, J, T, E, R, S, B, N (for thermocouple) |
| **Variable** | Variable | Receives temperature in °C |

### Agilent34970A_SetDAC

Sets a DAC output voltage (requires 34907A card).

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected 34970A instance |
| **DAC Channel** | Combo | DAC1 (`@221`) or DAC2 (`@222`) |
| **Voltage** | Number | Output voltage, 0 … 12 V |

### Agilent34970A_SetDigitalOutput

Sets an 8-bit value on a DIO port (requires 34907A card).

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected 34970A instance |
| **Port** | Combo | Port A or Port B |
| **Value** | Number | Byte value 0–255 |

---

## Example Sequence — Multi-Channel Temperature Scan

```
[Start]
→ [Loop N=10, counter=i]
    ↓ body
    [Agilent34970A_ScanChannels, Channels=(@101:104), Function=TEMP, TC=K, Prefix=ch]
    → [AddToChart, Series="CH101 Temp", Variable=ch_101]
    → [Wait 5000ms]
→ [SaveCsv, Path="C:\Data\temp_scan.csv"]
→ [End]
```
