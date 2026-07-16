using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace InstrumentControl.Core.Views;

public class LiveDataWindow : Window
{
    private record LiveEntry(DateTime Timestamp, string Function, double Value, string Unit);

    private sealed class FunctionSeries
    {
        public FunctionSeries(LineSeries series, CheckBox toggle) { Series = series; Toggle = toggle; }
        public LineSeries Series { get; }
        public CheckBox Toggle { get; }
        public int X;
    }

    // Distinct, readable-on-white colours cycled through as new measurement
    // functions (VOLT, CURR, POW, ...) show up.
    private static readonly OxyColor[] SeriesPalette =
    {
        OxyColor.FromRgb(0x0A, 0x56, 0xB8), // blue
        OxyColor.FromRgb(0xE0, 0x7A, 0x00), // amber
        OxyColor.FromRgb(0x0A, 0x8F, 0x42), // green
        OxyColor.FromRgb(0xB8, 0x30, 0x8F), // magenta
        OxyColor.FromRgb(0xC0, 0x39, 0x2B), // red
        OxyColor.FromRgb(0x00, 0x88, 0xA3), // teal
        OxyColor.FromRgb(0x7A, 0x3F, 0xA0), // purple
        OxyColor.FromRgb(0x7A, 0x8F, 0x00), // olive
    };

    // How many points/rows the chart and grid keep on screen per series. Every
    // measurement is still logged in full to disk (see LogMeasurement) and kept
    // in the driver's in-memory history — this only bounds what's rendered.
    private const int MaxDisplayedPoints = 500;

    private readonly InstrumentDriverBase _driver;
    private readonly ObservableCollection<LiveEntry> _entries = new();
    private readonly PlotModel _plotModel;
    private readonly WrapPanel _legendPanel;
    private readonly Dictionary<string, FunctionSeries> _seriesByFunction = new();

    public LiveDataWindow(InstrumentDriverBase driver)
    {
        _driver = driver;
        Title = $"Live Data — {driver.DriverName}  ({driver.Manufacturer} {driver.Model})";
        Width = 700;
        Height = 560;
        MinWidth = 500;
        MinHeight = 400;
        Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF8));

        // ── OxyPlot chart (light theme — dark axes/text on a white plot area) ──
        _plotModel = new PlotModel
        {
            Background           = OxyColor.FromRgb(0xFF, 0xFF, 0xFF),
            PlotAreaBackground   = OxyColor.FromRgb(0xFF, 0xFF, 0xFF),
            PlotAreaBorderColor  = OxyColor.FromRgb(0xC7, 0xCC, 0xDC),
            PlotMargins          = new OxyThickness(52, 4, 8, 28),
        };
        _plotModel.Axes.Add(new LinearAxis
        {
            Position          = AxisPosition.Bottom,
            Title             = "N",
            TitleColor        = OxyColor.FromRgb(0x4B, 0x53, 0x72),
            TitleFontSize     = 9,
            TextColor         = OxyColor.FromRgb(0x4B, 0x53, 0x72),
            TicklineColor     = OxyColor.FromRgb(0xC7, 0xCC, 0xDC),
            AxislineColor     = OxyColor.FromRgb(0xC7, 0xCC, 0xDC),
            MajorGridlineColor = OxyColor.FromRgb(0xE2, 0xE6, 0xF0),
            MajorGridlineStyle = LineStyle.Solid,
            FontSize          = 9,
        });
        _plotModel.Axes.Add(new LinearAxis
        {
            Position          = AxisPosition.Left,
            TextColor         = OxyColor.FromRgb(0x4B, 0x53, 0x72),
            TicklineColor     = OxyColor.FromRgb(0xC7, 0xCC, 0xDC),
            AxislineColor     = OxyColor.FromRgb(0xC7, 0xCC, 0xDC),
            MajorGridlineColor = OxyColor.FromRgb(0xE2, 0xE6, 0xF0),
            MajorGridlineStyle = LineStyle.Solid,
            FontSize          = 9,
        });

        var plotView = new PlotView
        {
            Model      = _plotModel,
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
        };

        // ── DataGrid ─────────────────────────────────────────────────────────
        var dataGrid = new DataGrid
        {
            Background              = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            Foreground              = new SolidColorBrush(Color.FromRgb(0x16, 0x22, 0x3D)),
            BorderBrush             = new SolidColorBrush(Color.FromRgb(0xC7, 0xCC, 0xDC)),
            BorderThickness         = new Thickness(0, 1, 0, 0),
            GridLinesVisibility     = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE6, 0xF0)),
            RowBackground           = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            AutoGenerateColumns     = false,
            IsReadOnly              = true,
            CanUserSortColumns      = false,
            CanUserReorderColumns   = false,
            RowHeight               = 20,
            FontFamily              = new FontFamily("Consolas"),
            FontSize                = 11,
            HeadersVisibility       = DataGridHeadersVisibility.Column,
            ItemsSource             = _entries,
        };
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Czas",
            Binding = new Binding("Timestamp") { StringFormat = "HH:mm:ss.fff" },
            Width   = new DataGridLength(90),
        });
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Funkcja",
            Binding = new Binding("Function"),
            Width   = new DataGridLength(70),
        });
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Wartość",
            Binding = new Binding("Value") { StringFormat = "G10" },
            Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Jedn.",
            Binding = new Binding("Unit"),
            Width   = new DataGridLength(64),
        });

        // ── Legend (one colour-coded checkbox per measurement function) ────────
        _legendPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(8, 4, 8, 4),
        };
        var legendBorder = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xFB)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0xC7, 0xCC, 0xDC)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child           = _legendPanel,
        };

        // ── Toolbar ──────────────────────────────────────────────────────────
        var clrBtn = new Button
        {
            Content         = "CLR",
            Width           = 54,
            Height          = 26,
            Margin          = new Thickness(6, 4, 6, 4),
            Background      = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            Foreground      = new SolidColorBrush(Color.FromRgb(0x0A, 0x56, 0xB8)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0xC7, 0xCC, 0xDC)),
            BorderThickness = new Thickness(1),
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 10,
            Cursor          = System.Windows.Input.Cursors.Hand,
        };
        clrBtn.Click += (_, _) =>
        {
            _driver.ClearMeasurementHistory();
            _entries.Clear();
            foreach (var fs in _seriesByFunction.Values)
                _plotModel.Series.Remove(fs.Series);
            _seriesByFunction.Clear();
            _legendPanel.Children.Clear();
            _plotModel.InvalidatePlot(true);
        };

        var titleLabel = new TextBlock
        {
            Text                = $"  {driver.DriverName}  —  {driver.Manufacturer} {driver.Model}",
            Foreground          = new SolidColorBrush(Color.FromRgb(0x4B, 0x53, 0x72)),
            FontFamily          = new FontFamily("Consolas"),
            FontSize            = 10,
            VerticalAlignment   = System.Windows.VerticalAlignment.Center,
        };

        var toolbar = new DockPanel
        {
            Background   = new SolidColorBrush(Color.FromRgb(0xE2, 0xE6, 0xF0)),
            LastChildFill = true,
            Height       = 34,
        };
        DockPanel.SetDock(clrBtn, Dock.Right);
        toolbar.Children.Add(clrBtn);
        toolbar.Children.Add(titleLabel);

        // ── Layout ───────────────────────────────────────────────────────────
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(165) });
        Grid.SetRow(plotView, 0);
        Grid.SetRow(legendBorder, 1);
        Grid.SetRow(toolbar, 2);
        Grid.SetRow(dataGrid, 3);
        grid.Children.Add(plotView);
        grid.Children.Add(legendBorder);
        grid.Children.Add(toolbar);
        grid.Children.Add(dataGrid);

        Content = grid;

        // Seed with everything already recorded for this driver (single-shot and
        // continuous measurements taken before this window was ever opened), then
        // keep appending live. This makes "Live" show the full history since
        // connection instead of only data captured since the window was last shown.
        foreach (var past in _driver.MeasurementHistory.TakeLast(MaxDisplayedPoints))
            AddEntry(past);

        _driver.MeasurementReceived += OnMeasurement;
        Closed += (_, _) => _driver.MeasurementReceived -= OnMeasurement;
    }

    private void OnMeasurement(object? sender, MeasurementResult result) =>
        Dispatcher.InvokeAsync(() => AddEntry(result));

    private void AddEntry(MeasurementResult result)
    {
        _entries.Insert(0, new LiveEntry(result.Timestamp, result.Function, result.Value, result.Unit));
        if (_entries.Count > MaxDisplayedPoints) _entries.RemoveAt(MaxDisplayedPoints);

        if (!double.IsNaN(result.Value) && !double.IsInfinity(result.Value))
        {
            var fs = GetOrCreateSeries(result.Function, result.Unit);
            fs.Series.Points.Add(new DataPoint(fs.X++, result.Value));
            if (fs.Series.Points.Count > MaxDisplayedPoints) fs.Series.Points.RemoveAt(0);
            _plotModel.InvalidatePlot(true);
        }
    }

    // Each measurement function (VOLT, CURR, POW, ...) gets its own line so
    // values on completely different scales never connect into one zig-zag —
    // plus a checkbox to show/hide that line independently.
    private FunctionSeries GetOrCreateSeries(string function, string unit)
    {
        if (_seriesByFunction.TryGetValue(function, out var existing)) return existing;

        var color = SeriesPalette[_seriesByFunction.Count % SeriesPalette.Length];
        var series = new LineSeries
        {
            Title           = string.IsNullOrEmpty(unit) ? function : $"{function} [{unit}]",
            Color           = color,
            StrokeThickness = 1.5,
            LineStyle       = LineStyle.Solid,
        };
        _plotModel.Series.Add(series);

        var swatch = new Border
        {
            Width               = 10,
            Height              = 10,
            CornerRadius        = new CornerRadius(2),
            Background          = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
            Margin              = new Thickness(0, 0, 4, 0),
            VerticalAlignment   = System.Windows.VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text                = string.IsNullOrEmpty(unit) ? function : $"{function} [{unit}]",
            FontFamily          = new FontFamily("Consolas"),
            FontSize            = 10,
            Foreground          = new SolidColorBrush(Color.FromRgb(0x16, 0x22, 0x3D)),
            VerticalAlignment   = System.Windows.VerticalAlignment.Center,
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(swatch);
        content.Children.Add(label);

        var toggle = new CheckBox
        {
            Content             = content,
            IsChecked           = true,
            Margin              = new Thickness(4, 2, 12, 2),
            VerticalAlignment   = System.Windows.VerticalAlignment.Center,
        };
        toggle.Checked   += (_, _) => { series.IsVisible = true;  _plotModel.InvalidatePlot(false); };
        toggle.Unchecked += (_, _) => { series.IsVisible = false; _plotModel.InvalidatePlot(false); };
        _legendPanel.Children.Add(toggle);

        var fs = new FunctionSeries(series, toggle);
        _seriesByFunction[function] = fs;
        return fs;
    }
}
