# Rigol DS1000Z — 4-Channel Oscilloscope

## Overview

The Rigol DS1054Z and DS1104Z are 4-channel digital oscilloscopes with 50 MHz and 100 MHz bandwidth respectively. They connect via LAN (TCPIP), USB, or GPIB and communicate over standard VISA.

| Specification | Value |
|---|---|
| Type | Digital Oscilloscope |
| Models | DS1054Z (50 MHz), DS1104Z (100 MHz) |
| Channels | 4 analog |
| Interface | LAN (TCPIP), USB-TMC, GPIB |
| Driver name | `RigolDS1000Z` |
| Resource patterns | `TCPIP?*::?*::INSTR`, `TCPIP?*::hislip?*::INSTR`, `USB?*::0x1AB1::?*::INSTR`, `GPIB?*::?*::INSTR` |

---

## Connecting

1. Connect the DS1000Z via LAN or USB
2. Click **Add Instrument** and select the resource
3. Auto-detection parses `*IDN?` (`RIGOL TECHNOLOGIES,DS1054Z` or `DS1104Z`)
4. Click **Connect**

For LAN: find the instrument IP address in its **Utility → IO Setting → LAN** menu, then enter the resource manually in the Connection Manager: `TCPIP0::192.168.x.x::inst0::INSTR`

---

## Front Panel

| Control | Description |
|---|---|
| **CH1–CH4 enable** | Toggle to show/hide each channel |
| **V/div per channel** | Vertical scale selector |
| **Coupling** | DC / AC / GND per channel |
| **Probe ratio** | ×0.1 … ×1000 per channel |
| **BW limit** | OFF or 20 MHz bandwidth limit |
| **Invert** | Invert signal polarity |
| **Timebase** | Horizontal time/div selector |
| **Timebase mode** | MAIN / XY / ROLL |
| **Trigger mode** | EDGE / PULSe / SLOPe / VIDeo / PATTern / RS232 / I2C / SPI |
| **Trigger source** | CH1–CH4 or AC |
| **Trigger level** | Threshold in V |
| **Trigger slope** | Positive / Negative / Either |
| **Trigger sweep** | AUTO / NORMal / SINGle |
| **Acquire type** | Normal / Averages / Peak Detect / Hi-Res |
| **Run** button | Start continuous acquisition |
| **Stop** button | Freeze the display |
| **Single** button | One-shot acquisition |
| **AutoScale** button | Auto-configure vertical and horizontal scales |
| **Math / FFT** | Configure MATH channel and FFT analysis |

---

## Sequence Blocks

### RigolDS1000Z_SetChannel

Configures a single analog channel.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Channel** | Combo | 1, 2, 3, 4 |
| **Display** | CheckBox | Enable or disable the channel |
| **Coupling** | Combo | DC, AC, GND |
| **V/div** | Combo | Vertical scale (0.001 V … 100 V/div) |
| **Offset [V]** | Text | Vertical offset |
| **Probe ×** | Combo | Probe attenuation ratio (0.1 × … 1000 ×) |
| **BW limit** | Combo | OFF or 20M (20 MHz limit) |
| **Invert** | CheckBox | Invert channel polarity |

### RigolDS1000Z_SetTimebase

Sets the horizontal timebase.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **s/div** | Combo | Time per division (5 ns … 50 s) |
| **Time offset [s]** | Text | Horizontal offset from trigger point |
| **Timebase mode** | Combo | MAIN, XY, ROLL |

### RigolDS1000Z_SetTrigger

Configures the trigger system. Edge trigger parameters are applied when **Trigger mode** is EDGE.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Trigger mode** | Combo | EDGE, PULSe, SLOPe, VIDeo, PATTern, RS232, I2C, SPI |
| **Sweep** | Combo | AUTO, NORMal, SINGle |
| **Source** | Combo | CH1, CH2, CH3, CH4, AC |
| **Slope** | Combo | POSitive (rising), NEGative (falling), RFALl (either) |
| **Level [V]** | Text | Trigger threshold voltage |

### RigolDS1000Z_SetAcquire

Configures the acquisition mode and memory depth.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Acquire type** | Combo | NORMal, AVERages, PEAKdetect, HRESolution |
| **Averages** | Combo | 2, 4, 8 … 1024 (active when type = AVERages) |
| **Memory depth** | Combo | AUTO, 12 k, 120 k, 1.2 M, 12 M, 24 M points |

### RigolDS1000Z_Run

Starts continuous acquisition (`RUN`).

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |

### RigolDS1000Z_Stop

Stops acquisition and freezes the display (`STOP`).

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |

### RigolDS1000Z_Single

Arms the oscilloscope for a single-shot acquisition (`SINGle`), then waits.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Wait after [ms]** | Number | Delay after sending the command (default 500 ms) |

### RigolDS1000Z_AutoScale

Sends `:AUToset` to automatically configure vertical scale, timebase, and trigger.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Wait after [ms]** | Number | Delay to let the scope settle (default 2000 ms) |

### RigolDS1000Z_MeasureVoltage

Reads an automated voltage-domain measurement from one channel.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Channel** | Combo | 1, 2, 3, 4 |
| **Parameter** | Combo | VMAX, VMIN, VPP, VTOP, VBASe, VAMP, VAVG, VRMS, OVERshoot, PREShoot |
| **Output variable** | Variable | Receives the measurement value in volts |

### RigolDS1000Z_MeasureTime

Reads a time-domain or frequency measurement from one channel.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Channel** | Combo | 1, 2, 3, 4 |
| **Parameter** | Combo | FREQuency (Hz), PERiod (s), RISetime (s), FALLtime (s), PWIDth (s), NWIDth (s) |
| **Output variable** | Variable | Receives the measurement value |

### RigolDS1000Z_MeasureDuty

Reads the duty cycle of a periodic signal.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Channel** | Combo | 1, 2, 3, 4 |
| **Parameter** | Combo | PDUTycycle (positive), NDUTycycle (negative) |
| **Output variable** | Variable | Receives the duty cycle in % |

### RigolDS1000Z_ReadWaveform

Downloads the raw waveform data from one channel and saves it as a CSV file with columns `Czas[s]` and `Napięcie[V]`.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Channel** | Combo | 1, 2, 3, 4 |
| **Waveform mode** | Combo | NORMal (screen points), MAXimum, RAW (full memory) |
| **CSV file path** | FilePath | Destination file for the waveform data |

### RigolDS1000Z_MathSetup

Configures the MATH channel for arithmetic operations or FFT analysis.

| Property | Type | Description |
|---|---|---|
| **Instrument** | InstrumentSelector | Connected DS1000Z instance |
| **Operation** | Combo | ADD, SUBTract, MULTiply, DIVision, FFT, ANDer, ORer, XORer, NOTer, INTGral, DIFF, SQRT, LOG, LN, EXP, ABS |
| **Source 1** | Combo | CH1, CH2, CH3, CH4 |
| **Source 2** | Combo | CH1, CH2, CH3, CH4 (ignored for unary operations) |
| **Enable Math** | CheckBox | Show or hide the MATH channel on screen |
| **FFT: window** | Combo | RECTangle, BLACkman, HANNing, HAMMing, FLATtop, TRIangle |
| **FFT: unit** | Combo | VRMS, DB |

---

## Example Sequence — Single-Shot Capture with Measurement

```
[Start]
→ [RigolDS1000Z_SetChannel, Channel=1, Coupling=DC, V/div=1, Probe×=10]
→ [RigolDS1000Z_SetTimebase, s/div=0.001, Mode=MAIN]
→ [RigolDS1000Z_SetTrigger, Mode=EDGE, Sweep=SINGle, Source=CH1, Slope=POSitive, Level=0.5]
→ [RigolDS1000Z_Single, WaitMs=2000]
→ [RigolDS1000Z_MeasureVoltage, Channel=1, Parameter=VPP, OutputVariable=vpp]
→ [RigolDS1000Z_MeasureTime, Channel=1, Parameter=FREQuency, OutputVariable=freq]
→ [LogMessage "VPP={vpp} V  Freq={freq} Hz"]
→ [RigolDS1000Z_ReadWaveform, Channel=1, WaveMode=NORMal, FilePath=C:\Data\waveform.csv]
→ [SaveCsv, Path=C:\Data\results.csv]
→ [End]
```

## Example Sequence — FFT Analysis

```
[Start]
→ [RigolDS1000Z_SetChannel, Channel=1, Coupling=DC, V/div=0.5, Probe×=1]
→ [RigolDS1000Z_SetTimebase, s/div=0.0001]
→ [RigolDS1000Z_Run]
→ [WaitBlock, 500]
→ [RigolDS1000Z_MathSetup, Operation=FFT, Source1=CH1, FFTWindow=HANNing, FFTUnit=DB, Enable=true]
→ [End]
```
