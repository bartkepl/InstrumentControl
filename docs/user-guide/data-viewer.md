# Data Viewer

The **Data / Charts** tab collects every measurement result produced during the current session and displays them as a chart and a table.

---

## Chart

The chart is an **OxyPlot** time-series graph. Each unique parameter name (e.g. `DCV`, `Temperature`, `OutputVoltage`) becomes a separate series with a distinct colour.

| Control | Description |
|---|---|
| Mouse wheel | Zoom in/out on the time axis |
| Left-drag | Pan the chart |
| Right-click → Reset | Restore the default view |
| Legend | Click a series name to toggle its visibility |

The chart updates in real time as measurements arrive — both from sequence runs and from manual front-panel actions.

---

## Data Table

Below the chart is a scrollable table with one row per measurement:

| Column | Description |
|---|---|
| **Timestamp** | Date and time of the measurement (millisecond precision) |
| **Instrument** | Driver name (e.g. `HP34401A`) |
| **Channel** | Channel identifier, if the instrument is multi-channel |
| **Parameter** | Parameter name (e.g. `DCV`, `ACV`, `Temperature`) |
| **Value** | Numeric reading |
| **Unit** | Unit string (e.g. `V`, `A`, `°C`) |
| **Function** | Measurement function string (mirrors Parameter for simple instruments) |

Click a column header to sort. The table updates live and scrolls to the latest row automatically.

---

## Filtering

Use the **Parameter filter** drop-down above the chart to show only a specific parameter. The filter applies to both the chart and the table.

---

## Exporting Data

Click **Export CSV** to save the current result set. A file-save dialog lets you choose the location and file name.

The exported file format:

```
Timestamp,Instrument,Channel,Parameter,Value,Unit,Function
2026-06-16T10:23:45.123,HP34401A,,DCV,5.001234,V,DCV
2026-06-16T10:23:46.045,HP34401A,,DCV,5.002019,V,DCV
```

Alternatively, use **SaveCsvBlock** in a sequence to export automatically to a predetermined path during a run (see [Block Reference — SaveCsvBlock](blocks-reference.md#savecsvblock)).

---

## Clearing Data

Click **Clear Data** to remove all results from the current session. The chart and table are wiped. Previously exported CSV files are not affected.

---

## Live Data Window

Each instrument also has a dedicated floating **Live Data Window** that can be opened independently. It shows:

- A chart containing only that instrument's measurements
- A table with only that instrument's results

This is useful when you have multiple instruments running simultaneously and want to watch each one separately. The Live Data Window does not replace the main Data / Charts tab — data is shown in both simultaneously.

Open the Live Data Window via **View → Live Data — [Instrument Name]** or by clicking the chart icon in the instrument's sidebar row.
