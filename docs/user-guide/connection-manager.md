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
RIGOL TECHNOLOGIES,DS1054Z,...        → Rigol DS1000Z driver
```

If identification fails (the instrument does not respond to `*IDN?` or the response is not recognized), you can choose the driver manually from the drop-down.

---

## Manual Driver Selection

Use the **Driver** list to override auto-detection:

1. Select a resource
2. Pick the correct driver from the list of all installed plugins (after a successful `*IDN?` match, non-matching drivers are dimmed but still selectable)
3. Click **Connect**

---

## Instrument Label & Automatic Naming

The **Instrument label** field (in the *Options* section) sets the name shown in the sidebar, the Front Panel tab, and the *Instrument* selector inside sequence blocks.

When you pick a driver, the field is **pre-filled automatically**:

- The first instance of a driver gets the driver name — e.g. `HP34401A`.
- Each additional instance of the same driver type is numbered — `HP34401A No.2`, `HP34401A No.3`, …

The numbering is based on the instruments already connected. You can overwrite the suggested label with any name you like before clicking **Connect**; the label is what sequence blocks use to address the instrument, so unique, memorable names (e.g. `DMM_input`, `PSU_5V`) make sequences easier to read.

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

A resource that is already held by a connected instrument is marked **in use** in the discovered-resources list and cannot be selected. If you enter its address manually, **Test** and **Connect** refuse it with a message asking you to disconnect the existing instrument first. (Simulated `SIM::` resources are exempt — they can be shared.)

Each connected instrument:

- Appears as a row in the left sidebar with its name and VISA address
- Gets its own **Front Panel** tab (tabs cycle through instruments when more than one is connected)
- Can be used as a source in sequence blocks via the *Instrument* property selector

---

## Disconnecting and Reconnecting

There are two distinct actions:

- **Remove** — click the **✕** button on the instrument's row in the sidebar. The VISA session is closed and the instrument is removed from the list entirely. Any sequence blocks that referenced it will log an error when executed.
- **Disconnect / Reconnect** — use the **Disconnect** button on the instrument's [Front Panel](front-panel.md) toolbar. This closes the session but keeps the instrument in the list, and a **Reconnect** button appears in its place to re-open the same resource later.

Disconnecting one instrument never affects the others — each row owns its own connection.

---

## Simulation Mode

When NI-VISA is not installed (or the **Use Simulation** checkbox is checked), the Connection Manager automatically activates simulation mode:

- The resource list shows one pre-labelled `SIM::` entry for each supported instrument type.
- Connecting any driver to a `SIM::` resource creates a **simulated instrument**.
- The `SimulatedConnectionProvider` maintains internal instrument state and returns physically plausible values with Gaussian noise — not a constant placeholder.
- The status bar shows `VISA: Simulation`.

Examples of what the simulation returns:

| Driver | Typical simulated reading |
|---|---|
| HP 34401A / Keithley 2000 (DC Voltage) | 3.300 V ± 1 mV |
| ITECH IT6922B MEAS:VOLT? (output ON) | Set-point ± 2 mV |
| CTS Chamber A0 query | Temperature ramps at 3 K/min toward set-point |
| RTB2004 frequency measurement | 1 000.0 Hz ± 0.5 Hz |

For the complete reference of simulated values see **[Simulation Mode](simulation-mode.md)**.
