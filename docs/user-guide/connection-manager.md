# Connection Manager

The Connection Manager is the dialog used to discover and connect to instruments. Open it by clicking **Add Instrument** (`+`) in the left sidebar.

---

## Resource Discovery

When the Connection Manager opens it automatically scans for available VISA resources across all interface types:

| Interface | Example resource string | Typical devices |
|---|---|---|
| GPIB | `GPIB0::1::INSTR` | Older bench instruments |
| USB-TMC | `USB0::0x0957::0x0607::MY12345678::INSTR` | Modern USB instruments |
| TCPIP / LAN | `TCPIP0::192.168.1.100::inst0::INSTR` | LAN-enabled instruments |
| RS-232 / COM | `COM3` | Serial-only devices |

The scan result is shown in the **Resource List**. Click **Refresh** to repeat the scan at any time.

!!! note "COM ports"
    COM ports appear in the list only when a plugin that supports serial resources (e.g. the CTS Chamber driver) is loaded. The Connection Manager filters the list to show only resources that at least one installed plugin can handle.

---

## Automatic Identification

When you select a resource from the list, InstrumentControl sends `*IDN?` (IEEE 488.2 identification query) to the instrument and parses the response. If the response matches a known driver's `SupportedResourcePatterns`, the driver is automatically highlighted in the **Driver** drop-down.

Example `*IDN?` response and how it maps to drivers:

```
HEWLETT-PACKARD,34401A,0,10-5-2      → HP 34401A driver
AGILENT TECHNOLOGIES,34970A,...       → Agilent 34970A driver
KEITHLEY INSTRUMENTS INC.,MODEL 2000  → Keithley 2000 driver
ITECH ELECTRONIC,IT6922B,...          → ITECH IT6922B driver
Rohde&Schwarz,RTB2004,...             → R&S RTB2004 driver
```

If identification fails (the instrument does not respond to `*IDN?` or the response is not recognized), you can choose the driver manually from the drop-down.

---

## Manual Driver Selection

Use the **Driver** drop-down to override auto-detection:

1. Select a resource
2. Expand the **Driver** drop-down
3. Choose the correct driver from the list of all installed plugins
4. Click **Connect**

---

## Connection Status

After clicking **Connect** the Connection Manager shows a progress indicator while:

1. Opening the VISA session
2. Sending `*IDN?` (or the device-specific identification for non-VISA drivers)
3. Parsing firmware and serial number
4. Initializing the driver's internal state

On success the dialog closes and the instrument appears in the sidebar. On failure an error message describes the cause (resource busy, wrong VISA address, driver mismatch, etc.).

---

## Multiple Instruments

You can connect as many instruments as you need. Click **Add Instrument** again to open the Connection Manager a second time. Instruments already connected are still active while you connect a new one.

Each connected instrument:

- Appears as a row in the left sidebar with its name and VISA address
- Gets its own **Front Panel** tab (tabs cycle through instruments when more than one is connected)
- Can be used as a source in sequence blocks via the *Instrument* property selector

---

## Disconnecting

Right-click an instrument in the sidebar and choose **Disconnect**, or select it and click the **−** (remove) button. The VISA session is closed and the instrument row is removed. Any sequence blocks that referenced it will log an error when executed.

---

## Simulation Mode

When NI-VISA is not installed:

- The Connection Manager shows a single `SIMULATED` resource
- Connecting to it with any driver creates a **simulated instrument**
- The `SimulatedConnectionProvider` returns configurable default values (usually `+1.234567E+00`)
- The status bar shows `VISA: Simulation`

Simulated instruments are useful for developing and testing sequences on machines without lab hardware.
