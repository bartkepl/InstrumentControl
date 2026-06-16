# ITECH IT6922B — DC Power Supply

## Overview

The ITECH IT6922B is a 60 V / 5 A programmable DC power supply with SCPI control over USB, LAN, or GPIB.

| Specification | Value |
|---|---|
| Type | DC Power Supply |
| Voltage range | 0 … 60 V |
| Current range | 0 … 5 A |
| Interface | USB-TMC, LAN (VXI-11), GPIB |
| Driver name | `ItechIT6922B` |
| Resource patterns | `USB?*::?*::INSTR`, `TCPIP?*::?*::INSTR`, `GPIB?*::?*::INSTR` |

---

## Connecting

1. Connect via USB, LAN, or GPIB
2. Click **Add Instrument** and select the resource
3. Auto-detection matches the `*IDN?` response (`ITECH ELECTRONIC,IT6922B`)
4. Click **Connect**

---

## Front Panel

| Control | Description |
|---|---|
| **Voltage set-point** | Numeric input for target voltage (0–60 V) |
| **Current limit** | Numeric input for current limit (0–5 A) |
| **Output ON/OFF** | Toggle to enable or disable the output |
| **Voltage readback** | Live actual output voltage |
| **Current readback** | Live actual output current |
| **Power readback** | Live actual power (V × A) |
| **OVP threshold** | Over-voltage protection level |
| **OCP threshold** | Over-current protection level |

!!! warning "Output safety"
    Always set the voltage and current limit **before** enabling the output. The power supply will immediately apply the set voltage when the output is enabled.

---

## Sequence Blocks

### ItechIT6922B_SetVoltage

Sets the target output voltage.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **Voltage (V)** | Number | Target voltage, 0–60 V |

Sends: `VOLT {voltage}`

### ItechIT6922B_SetCurrent

Sets the current limit.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **Current (A)** | Number | Current limit, 0–5 A |

Sends: `CURR {current}`

### ItechIT6922B_SetOutput

Enables or disables the output.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **Output** | CheckBox | `true` = ON, `false` = OFF |

Sends: `OUTP ON` or `OUTP OFF`

### ItechIT6922B_MeasureVoltage

Reads the actual output voltage.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **Variable** | Variable | Receives measured voltage in V |

Sends: `MEAS:VOLT?`

### ItechIT6922B_MeasureCurrent

Reads the actual output current.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **Variable** | Variable | Receives measured current in A |

Sends: `MEAS:CURR?`

### ItechIT6922B_MeasurePower

Reads the actual output power.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **Variable** | Variable | Receives measured power in W |

Sends: `MEAS:POW?`

### ItechIT6922B_SetOVP

Sets the over-voltage protection threshold.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **OVP Voltage (V)** | Number | Protection threshold |

Sends: `VOLT:PROT {voltage}`

### ItechIT6922B_SetOCP

Sets the over-current protection threshold.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected IT6922B instance |
| **OCP Current (A)** | Number | Protection threshold |

Sends: `CURR:PROT {current}`

---

## Example Sequence — Voltage Sweep

```
[Start]
→ [ItechIT6922B_SetCurrent, I=0.5A]
→ [ItechIT6922B_SetOutput, Output=ON]
→ [Loop N=6, counter=i]
    ↓ body
    [MathBlock, Expression={i}*2+1, Result=setV]     # 1,3,5,7,9,11 V
    → [ItechIT6922B_SetVoltage, Voltage={setV}]
    → [Wait 500ms]
    → [ItechIT6922B_MeasureVoltage, Variable=vActual]
    → [ItechIT6922B_MeasureCurrent, Variable=iActual]
    → [AddToChart, Series="Voltage", Variable=vActual]
    → [SaveCsv, Path="C:\Data\sweep.csv", Append=true]
→ [ItechIT6922B_SetOutput, Output=OFF]
→ [End]
```
