# CTS T-40/50 — Environmental Chamber

## Overview

The CTS T-40/50 is an environmental test chamber for temperature and humidity testing. It uses a proprietary binary ASCII protocol over RS-232 — not VISA/SCPI.

| Specification | Value |
|---|---|
| Type | Environmental Test Chamber |
| Temperature range | −75 … +185 °C |
| Interface | RS-232 (COM port), 19200 baud, 8N1 with odd parity |
| Protocol | Custom binary ASCII framing (STX / ADR / DATA / CHK / ETX) |
| Driver name | `CTSChamber` |
| Resource patterns | `COM?*` |

---

## Serial Connection Settings

The CTS chamber does **not** use VISA. The driver uses a custom `IConnectionProvider` that opens a COM port directly:

| Parameter | Value |
|---|---|
| Baud rate | 19200 |
| Data bits | 8 |
| Parity | Odd |
| Stop bits | 1 |
| Flow control | None |

---

## Connecting

1. Connect the CTS chamber's RS-232 port to the PC (use a USB-to-RS232 adapter if needed)
2. Note the COM port number from Device Manager
3. Click **Add Instrument** and select the COM port resource (e.g. `COM3`)
4. The Connection Manager lists COM ports automatically
5. Select the **CTSChamber** driver from the drop-down (auto-detection is not possible because the chamber does not respond to `*IDN?`)
6. Click **Connect**

!!! tip "USB-to-RS232 adapters"
    Use a high-quality adapter with a genuine FTDI or Prolific chip. Cheap adapters often lose data at 19200 baud with odd parity.

---

## Protocol Details

The CTS chamber uses a proprietary binary ASCII framing scheme. Developers who need to extend the driver should read the [CTSSerialConnectionProvider](../../developer-guide/plugin-development.md#custom-serial-protocol) documentation.

Frame structure:

```
STX  |  ADR  |  DATA (each byte OR'd with 0x80)  |  CHK  |  ETX
0x02    0x30    0xC2 0xD4 ...                        0xHH   0x03
```

The checksum (`CHK`) is the XOR of all bytes from `ADR` to the last `DATA` byte.

---

## Front Panel

| Control | Description |
|---|---|
| **Set Temperature (°C)** | Target temperature |
| **Set Ramp (°C/min)** | Temperature ramp rate |
| **Start** | Start the chamber program |
| **Stop** | Stop the chamber |
| **Pause** | Pause the current program |
| **Temperature readback** | Current chamber temperature in °C |
| **Status** | Chamber operating mode (Running, Paused, Idle, Error) |

---

## Sequence Blocks

### CTSChamber_SetTemperature

Sets the target temperature set-point.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |
| **Temperature (°C)** | Number | Target temperature |

### CTSChamber_SetRamp

Sets the ramp rate between temperature set-points.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |
| **Ramp Rate (°C/min)** | Number | Rate of temperature change |

### CTSChamber_Start

Starts the chamber conditioning program.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |

### CTSChamber_Stop

Stops the chamber and returns to standby.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |

### CTSChamber_Pause

Pauses the current program without losing the set-point.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |

### CTSChamber_ReadTemperature

Reads the current chamber temperature.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |
| **Variable** | Variable | Receives temperature in °C |

### CTSChamber_WaitForTemperature

Blocks execution until the chamber reaches a temperature within a tolerance band, or a timeout expires.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected CTS chamber instance |
| **Target (°C)** | Number | Target temperature |
| **Tolerance (°C)** | Number | Acceptable deviation band (e.g. `0.5` means ±0.5 °C) |
| **Timeout (s)** | Number | Maximum wait time in seconds. 0 = no timeout. |

---

## Example Sequence — Temperature Profile Test

```
[Start]
→ [CTSChamber_SetRamp, Rate=2.0]
→ [CTSChamber_Start]
→ [Loop temperatures in -40, 25, 85°C: use SetVariable]
    ↓
    [SetVariable, Name=targetTemp, Value=-40]
    → [CTSChamber_SetTemperature, Temp={targetTemp}]
    → [CTSChamber_WaitForTemperature, Target={targetTemp}, Tolerance=0.5, Timeout=3600]
    → [Wait 300000ms]   # soak 5 minutes
    → [HP34401A_MeasureDCV, Variable=voltage]
    → [SaveCsv, Path="C:\Data\temp_profile.csv", Append=true]
→ [CTSChamber_Stop]
→ [End]
```
