using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.App.Services;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace InstrumentControl.App.ViewModels;

public partial class DataViewerViewModel : ViewModelBase
{
    private readonly DataManager _dataManager;

    [ObservableProperty] private ObservableCollection<MeasurementResult> _results = new();
    [ObservableProperty] private PlotModel _plotModel;
    [ObservableProperty] private string _selectedSeries = string.Empty;
    [ObservableProperty] private int _resultCount;

    public string ResultCountDisplay =>
        $"{LocalizationService.Get("DataViewer_ResultsLabel")} {ResultCount}";

    private readonly Dictionary<string, LineSeries> _seriesMap = new();
    private static readonly OxyColor[] SeriesColors =
    [
        OxyColors.SteelBlue, OxyColors.OrangeRed, OxyColors.SeaGreen,
        OxyColors.Purple, OxyColors.Goldenrod, OxyColors.Teal
    ];
    private int _colorIdx;

    public DataViewerViewModel(DataManager dataManager)
    {
        _dataManager = dataManager;
        _plotModel = CreatePlotModel();
        _dataManager.ResultAdded += OnResultAdded;

        LocalizationService.LanguageChanged += (_, _) => RunOnUi(() =>
        {
            OnPropertyChanged(nameof(ResultCountDisplay));
            RebuildPlotModel();
        });
    }

    private void RebuildPlotModel()
    {
        var newModel = CreatePlotModel();
        foreach (var s in _seriesMap.Values)
            newModel.Series.Add(s);
        PlotModel = newModel;
    }

    private PlotModel CreatePlotModel()
    {
        var model = new PlotModel
        {
            Title = LocalizationService.Get("Chart_Title"),
            Background = OxyColors.White,
            PlotAreaBackground = OxyColor.FromRgb(248, 249, 250),
            TitleFontSize = 13
        };
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = LocalizationService.Get("Chart_XAxis"),
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(200, 200, 200)
        });
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = LocalizationService.Get("Chart_YAxis"),
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(200, 200, 200)
        });
        return model;
    }

    private void OnResultAdded(object? sender, MeasurementResult r)
    {
        RunOnUi(() =>
        {
            Results.Add(r);
            ResultCount = Results.Count;
            OnPropertyChanged(nameof(ResultCountDisplay));
            UpdateChart(r);
        });
    }

    private void UpdateChart(MeasurementResult r)
    {
        if (!r.IsValid || double.IsNaN(r.Value)) return;

        var key = string.IsNullOrEmpty(r.ParameterName) ? r.Function : r.ParameterName;
        if (!_seriesMap.TryGetValue(key, out var series))
        {
            series = new LineSeries
            {
                Title = key,
                Color = SeriesColors[_colorIdx % SeriesColors.Length],
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                StrokeThickness = 1.5
            };
            _colorIdx++;
            _seriesMap[key] = series;
            PlotModel.Series.Add(series);
        }

        series.Points.Add(new DataPoint(series.Points.Count + 1, r.Value));
        PlotModel.InvalidatePlot(true);
    }

    [RelayCommand]
    private void ClearData()
    {
        _dataManager.Clear();
        Results.Clear();
        ResultCount = 0;
        OnPropertyChanged(nameof(ResultCountDisplay));
        _seriesMap.Clear();
        _colorIdx = 0;
        PlotModel = CreatePlotModel();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = LocalizationService.Get("VM_FilterCsv"),
            DefaultExt = ".csv",
            FileName = string.Format(LocalizationService.Get("VM_CsvDefaultName"), DateTime.Now.ToString("yyyyMMdd_HHmmss"))
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _dataManager.ExportToCsv(dlg.FileName, Results, false);
            MessageBox.Show($"{LocalizationService.Get("VM_ExportSuccess")} {Results.Count}\n{dlg.FileName}",
                LocalizationService.Get("VM_ExportTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{LocalizationService.Get("VM_ExportError")} {ex.Message}",
                LocalizationService.Get("VM_ErrorPrefix"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
