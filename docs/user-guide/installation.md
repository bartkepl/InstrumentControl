# Installation

## System Requirements

| Component | Requirement |
|---|---|
| Operating System | Windows 10 or 11, 64-bit |
| NI-VISA | Optional — 21.x or newer (for real instruments) |
| .NET 8 Runtime | Not required — bundled in the installer and ZIP |

!!! info "Simulation Mode"
    Without NI-VISA the application starts in **simulation mode**. All VISA calls return a configurable default response, so you can design and test sequences on any Windows laptop without physical hardware.

---

## Option A — Installer (Recommended)

The installer is the easiest way to get started. It requires no administrator rights and enables automatic updates.

1. Go to the [Releases](https://github.com/bartkepl/InstrumentControl/releases) page
2. Download `InstrumentControl-x.y.z-win-Setup.exe`
3. Run the installer — it installs to `%LocalAppData%\InstrumentControl\`
4. A shortcut appears in the **Start Menu** under *InstrumentControl*

### Custom Install Location

Run the installer from a Command Prompt or PowerShell to override the default folder:

```powershell
InstrumentControl-x.y.z-win-Setup.exe --installDir "D:\Lab\InstrumentControl"
```

### Automatic Updates

When installed via the Setup EXE, InstrumentControl checks **GitHub Releases on every launch** (before the main window opens). If a newer version is found, a dialog appears:

- **Yes** — downloads only the changed files (delta update) and restarts into the new version
- **No** — skips the update and opens normally

Update checks require internet access. On isolated lab networks with no GitHub connectivity the check silently times out and the app starts normally.

---

## Option B — Portable ZIP

No installation needed — extract and run.

1. Go to the [Releases](https://github.com/bartkepl/InstrumentControl/releases) page
2. Download `InstrumentControl-x.y.z-win-x64.zip`
3. Extract to any folder (e.g. `D:\Lab\InstrumentControl`)
4. Run `InstrumentControl.exe`

!!! warning "No auto-update in portable mode"
    The ZIP version does not auto-update. To upgrade, download and extract the new ZIP manually, then replace the old files.

---

## NI-VISA Setup

InstrumentControl uses NI-VISA for communication with GPIB, USB-TMC, and LAN instruments. If you already have NI-MAX or another NI software suite installed, NI-VISA is most likely already present.

### Check if NI-VISA is Installed

Open **NI MAX** (National Instruments Measurement & Automation Explorer) — if it lists your instruments, NI-VISA is working.

Alternatively, run InstrumentControl and check the **status bar** at the bottom:

- `VISA: OK` — NI-VISA found and loaded
- `VISA: Simulation` — NI-VISA not found; simulation mode active

### Install NI-VISA

1. Download the NI-VISA Runtime from [ni.com/visa](https://www.ni.com/en/support/downloads/drivers/download.ni-visa.html)
2. Install and reboot
3. Restart InstrumentControl — the status bar should now show `VISA: OK`

---

## Uninstalling

### Installer Edition

Go to **Settings → Apps → InstrumentControl** and click *Uninstall*, or run:

```powershell
& "$env:LocalAppData\InstrumentControl\Update.exe" --uninstall
```

### Portable Edition

Delete the extracted folder. No registry entries or system files are written.
