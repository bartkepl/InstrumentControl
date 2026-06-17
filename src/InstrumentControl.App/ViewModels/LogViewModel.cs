using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using InstrumentControl.Core.Enums;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.App.ViewModels;

public partial class LogViewModel : ObservableObject
{
    [ObservableProperty] private string _allText = "";
    [ObservableProperty] private string _sequenceText = "";
    [ObservableProperty] private string _visaText = "";
    [ObservableProperty] private string _serialText = "";
    [ObservableProperty] private string _instrumentText = "";
    [ObservableProperty] private string _eventText = "";
    [ObservableProperty] private string _debugText = "";

    public LogViewModel(ILogService logService)
    {
        logService.EntryAdded += OnEntryAdded;
    }

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var line = entry.Formatted + "\n";
            AllText += line;
            switch (entry.Source)
            {
                case LogSource.Sequence:   SequenceText   += line; break;
                case LogSource.Visa:       VisaText       += line; break;
                case LogSource.Serial:     SerialText     += line; break;
                case LogSource.Instrument: InstrumentText += line; break;
                case LogSource.Event:      EventText      += line; break;
                case LogSource.Debug:      DebugText      += line; break;
                // LogSource.System: only appears in AllText
            }
        });
    }
}
