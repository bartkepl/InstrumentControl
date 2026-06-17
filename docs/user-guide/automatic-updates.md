# Automatic Updates

When installed via the **Setup EXE**, InstrumentControl keeps itself up to date using [Velopack](https://github.com/velopack/velopack). The portable ZIP build does **not** auto-update — to update it, download and extract the newer ZIP manually.

---

## How it works

On every launch — **before the main window opens** — the app checks the project's GitHub Releases for a newer version:

1. If a newer release is found, a dialog asks whether to update now.
2. **Yes** downloads only the changed files (delta update) and restarts into the new version.
3. **No** skips the update for this launch and starts normally.

The version offered is compared against the currently installed version, so you are never prompted for a release you already run.

---

## Offline / isolated networks

Update checks need internet access to reach GitHub. If GitHub is unreachable (for example on an isolated lab network), the check **times out after 10 seconds** and the application starts normally. A missing network never blocks startup.

---

## Loop protection

A broken release could otherwise cause an endless *download → apply → restart → download* loop. To prevent that, InstrumentControl tracks failed update attempts:

- Each failed download/apply attempt for a given version is counted in
  `%LocalAppData%\InstrumentControl\update_state.json`.
- After **3** consecutive failures on the **same** version, that version is **skipped** until a newer one is published.
- A newer version resets the counter, so a later good release is still offered.

---

## Update log

All update activity — checks, downloads, timeouts, failures and skips — is appended to:

```
%LocalAppData%\InstrumentControl\update.log
```

This is the first place to look if an installed copy is not updating as expected. Example lines:

```
[2026-06-17 09:12:03] Downloading update 1.4.27...
[2026-06-17 09:12:31] Update failed: HTTP 404 on delta package
[2026-06-17 09:12:31] Recorded failure 1/3 for version 1.4.27
```

---

## Files at a glance

| File (under `%LocalAppData%\InstrumentControl\`) | Purpose |
|---|---|
| `update_state.json` | Last attempted version + failure counter (loop protection) |
| `update.log` | Human-readable history of update activity |
| `settings.json` | UI language preference (unrelated to updates) |
