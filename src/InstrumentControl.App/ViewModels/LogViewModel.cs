using System.Collections.Concurrent;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.App.ViewModels;

public partial class LogViewModel : ObservableObject
{
    // Same cap/trim strategy as SequenceEditorViewModel's log console: without it,
    // these strings grow forever (every VISA/serial/instrument log line appends via
    // "+="), which reallocates and copies the whole string each time (O(n^2) overall)
    // and forces the bound TextBox to re-layout an ever-growing blob on the UI
    // thread — at a fast continuous-measurement polling rate this compounds until
    // the app grinds to a halt.
    private const int MaxLogLength = 100_000;
    private const int TrimToLength = 80_000;

    [ObservableProperty] private string _allText = "";
    [ObservableProperty] private string _sequenceText = "";
    [ObservableProperty] private string _visaText = "";
    [ObservableProperty] private string _serialText = "";
    [ObservableProperty] private string _instrumentText = "";
    [ObservableProperty] private string _eventText = "";
    [ObservableProperty] private string _debugText = "";

    private readonly StringBuilder _all = new();
    private readonly StringBuilder _sequence = new();
    private readonly StringBuilder _visa = new();
    private readonly StringBuilder _serial = new();
    private readonly StringBuilder _instrument = new();
    private readonly StringBuilder _event = new();
    private readonly StringBuilder _debug = new();

    private readonly ConcurrentQueue<LogEntry> _pending = new();

    public LogViewModel(ILogService logService)
    {
        // Entries are queued and flushed in batches via a timer instead of touching
        // the bound TextBoxes on every single entry — at fast polling rates that
        // would flood the dispatcher with individual updates and re-layout each
        // TextBox dozens of times per second. Mirrors SequenceEditorViewModel's
        // log-flush pattern.
        logService.EntryAdded += (_, entry) => _pending.Enqueue(entry);

        var flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        flushTimer.Tick += (_, _) => Flush();
        flushTimer.Start();
    }

    private void Flush()
    {
        if (_pending.IsEmpty) return;

        while (_pending.TryDequeue(out var entry))
        {
            var line = entry.Formatted + "\n";
            _all.Append(line);
            switch (entry.Source)
            {
                case LogSource.Sequence:   _sequence.Append(line);   break;
                case LogSource.Visa:       _visa.Append(line);       break;
                case LogSource.Serial:     _serial.Append(line);     break;
                case LogSource.Instrument: _instrument.Append(line); break;
                case LogSource.Event:      _event.Append(line);      break;
                case LogSource.Debug:      _debug.Append(line);      break;
                // LogSource.System: only appears in AllText
            }
        }

        Trim(_all); Trim(_sequence); Trim(_visa); Trim(_serial);
        Trim(_instrument); Trim(_event); Trim(_debug);

        AllText        = _all.ToString();
        SequenceText   = _sequence.ToString();
        VisaText       = _visa.ToString();
        SerialText     = _serial.ToString();
        InstrumentText = _instrument.ToString();
        EventText      = _event.ToString();
        DebugText      = _debug.ToString();
    }

    private static void Trim(StringBuilder sb)
    {
        if (sb.Length <= MaxLogLength) return;
        var s = sb.ToString();
        int cut = s.IndexOf('\n', s.Length - TrimToLength);
        sb.Clear();
        sb.Append(cut >= 0 ? s[(cut + 1)..] : s[^TrimToLength..]);
    }
}
