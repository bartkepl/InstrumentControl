# Agilent 34970A — Data Acquisition / Switch Unit

## Overview

The Agilent 34970A is a modular data acquisition and switching unit that accepts plug-in
cards for analog measurement, digital I/O, totalizing, and DAC output. The driver is
organised **per card** (per slot), not per function: a single card exposes all of its
capabilities at once.

| Specification | Value |
|---|---|
| Type | DAQ / Switch Unit |
| Interface | GPIB, USB-TMC |
| Cards supported | 34901A (20-ch analog mux), 34907A (multifunction: DIO + Totalizer + DAC) |
| Driver name | `Agilent34970A` |
| Resource patterns | `GPIB?*::?*::INSTR`, `USB?*::?*::INSTR` |

### Channel addressing

Channels use the form `sNN` where `s` = slot (1/2/3) and `NN` = channel within the card.
In SCPI lists this becomes a 3-digit number: slot 1 ch 1 → `101`, slot 2 ch 16 → `216`,
slot 3 ch 5 → `305`. In this app the slot is written as **100 / 200 / 300**.

---

## Supported Cards

### 34901A — 20-Channel Multiplexer (Analog Input)

- 20 multiplexer channels: `101…120` (slot 1).
- 2 dedicated current channels: `121`, `122` (built-in shunts) — **current is only
  measurable here**.
- Per-channel function: each channel may measure a **different** quantity, so one scan can
  return e.g. 2× DC voltage **and** 1× 4-wire RTD temperature.
- Measures: DC/AC voltage, 2-wire/4-wire resistance, DC/AC current (ch 21/22 only),
  frequency, period, temperature (thermocouple / RTD 2W & 4W / thermistor).
- **4-wire rule:** a 4-wire measurement (FRES / RTD 4W) must use a *source* channel in
  `01…10`; the instrument automatically pairs it with channel `n+10` (`11…20`), which must
  **not** appear separately in the scan list.
- Built-in isothermal reference junction for thermocouples.
- Max 250 V channel-to-earth.

### 34907A — Multifunction Module (DIO + Totalizer + DAC)

Fixed channel assignment — **do not confuse these** (mixing them up causes instrument
error 305):

| Channel | Function |
|---|---|
| `s01` | Digital I/O port 1 (8-bit) |
| `s02` | Digital I/O port 2 (8-bit) |
| `s03` | Totalizer (26-bit pulse counter, up to 100 kHz) |
| `s04` | DAC output 1 (±12 V, 16-bit) |
| `s05` | DAC output 2 (±12 V, 16-bit) |

So for slot 1: DAC1 = `104`, DAC2 = `105`, totalizer = `103`, DIO ports = `101` / `102`.

---

## SCPI commands used

| Operation | Command |
|---|---|
| Detect card in a slot | `SYSTem:CTYPe? {100\|200\|300}` |
| Configure a channel | `CONFigure:<func> [<range>[,<res>]],(@<ch>)` |
| Configure temperature | `CONF:TEMP {TC\|RTD\|FRTD\|THER},<type>,(@<ch>)` |
| Define scan list | `ROUTe:SCAN (@<ch_list>)` |
| Initiate + read a sweep | `READ?` |
| Set DAC output | `SOURce:VOLTage <volts>,(@<s04\|s05>)` |
| Write digital byte | `SOURce:DIGital:DATA:BYTE <data>,(@<s01\|s02>)` |
| Read digital byte | `[SENSe:]DIGital:DATA:BYTE? (@<s01\|s02>)` |
| Read totalizer | `MEASure:TOTalize? READ,(@<s03>)` |
| Clear totalizer | `[SENSe:]TOTalize:CLEar:IMMediate (@<s03>)` |
| Read error queue | `SYSTem:ERRor?` |
| Integration time | `[SENSe:]<func>:NPLC <n>,(@<ch>)` |
| Mx+B scaling | `CALCulate:SCALe:GAIN/OFFSet/UNIT/STATe …,(@<ch>)` |
| Trigger source | `TRIGger:SOURce {IMMediate\|TIMer\|EXTernal\|BUS\|ALARm}` |
| Scan interval (timer) | `TRIGger:TIMer <seconds>` |
| Sweep count | `TRIGger:COUNt <n>` |
| Channel delay | `ROUTe:CHANnel:DELay <seconds>,(@<ch_list>)` |

Mixed-function scanning works because the instrument keeps a **per-channel** configuration:
the driver sends one `CONF:…` per channel, then a single `ROUT:SCAN` over the whole list,
then `READ?`. Values are returned in scan-list order.

---

## Connecting

1. Power on the 34970A with cards installed.
2. Add the instrument, select the GPIB/USB resource and connect.
3. Click **WYKRYJ KARTY** (Detect cards) on the front panel — the driver queries
   `SYST:CTYP?` for each slot and fills in the detected card types.

---

## Front Panel

Tabs are **built dynamically from the cards in the slots** — one tab per installed card.
The sidebar handles card configuration: **WYKRYJ KARTY** (auto-detect) or manual per slot.

### 34901A multiplexer tab

- Editable grid: each row = a **channel chosen from a slot-aware dropdown** + function +
  range. Mixed functions in one scan (e.g. 2× VDC + 1× RTD 4W).
- The grid enforces the card rules **live**:
    - a channel already used in one row disappears from the dropdowns of the other rows
      (each channel can be configured only once),
    - selecting a current channel (`s21`/`s22`) restricts that row's function list to
      `CURR_DC`/`CURR_AC` (and auto-switches the function); other channels hide the current
      functions,
    - configuring a row as 4-wire (`OHM4W`/`TEMP_RTD4W`) reserves the source channel **and**
      its `n+10` pair, so both vanish from the other rows.
- **Konfig. kanału…** opens a dialog for the selected row: resolution, NPLC, and Mx+B
  scaling (gain M, offset B, custom unit).
- **Konfig. skanu…** opens a dialog for scan settings: trigger source, timer interval,
  sweep count, channel delay.
- Single scan, continuous scan (UI poll interval), and a results grid with auto-sized,
  resizable columns.

### 34907A multifunction tab

All three functions of the one card on a single tab (no slot selector — the tab *is* the
card):

- **DAC1 / DAC2** with set-point, ▲/▼ step buttons (configurable step), and Set.
- **Digital I/O** — both ports shown **side by side** (port 1 = s01, port 2 = s02), each
  with read indicators and write toggles.
- **Totalizer** — read / reset.

---

## Sequence Blocks

| Block | Purpose |
|---|---|
| **Wykryj karty** (`A34970A_DetectCards`) | Run `SYST:CTYP?` and configure detected cards. |
| **Skanuj kanały** (`A34970A_ScanChannels`) | Scan a channel list with one shared function. |
| **Skan mieszany** (`A34970A_ScanMixed`) | Mixed per-channel scan. Spec format: `kanał=FUNK[:param][@zakres]` separated by `;`, e.g. `101=VDC; 102=VDC@10; 103=RTD4W:85; 104=TC:K`. |
| **Zmierz kanał** (`A34970A_MeasureChannel`) | Measure a single channel (slot + channel + function). |
| **Ustaw DAC** (`A34970A_SetDAC`) | Set a DAC output (channel s04/s05), −12…+12 V. |
| **Cyfrowe wyjście** (`A34970A_SetDigitalOutput`) | Write a byte to DIO port 1 or 2. |
| **Cyfrowe wejście** (`A34970A_ReadDigitalInput`) | Read a byte from DIO port 1 or 2. |
| **Odczyt totalizatora** (`A34970A_ReadTotalizer`) | Read the totalizer count, optionally reset after. |

### Mixed-scan spec — function names

`VDC`, `VAC`, `OHM2W`/`RES`, `OHM4W`/`FRES`, `IDC`/`CURR_DC`, `IAC`/`CURR_AC`, `FREQ`,
`PER`/`PERIOD`, `TC` (param = type letter, e.g. `K`), `RTD` (param = α: `85`/`91`),
`RTD4W`/`FRTD` (param = α), `THERM` (param = `2200`/`5000`/`10000`).

---

## Example Sequence — Mixed Measurement Scan

```
[Start]
→ [A34970A_DetectCards, Instrument=DAQ]
→ [Loop N=10, counter=i]
    ↓ body
    [A34970A_ScanMixed, Instrument=DAQ,
       Spec="101=VDC; 102=VDC; 103=RTD4W:85", Prefix=ch]
    → [AddToChart, Series="RTD", Variable=ch_103]
    → [Wait 5000ms]
→ [SaveCsv, Path="C:\Data\scan.csv"]
→ [End]
```
