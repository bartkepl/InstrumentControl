using System.Collections.ObjectModel;
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

    private readonly InstrumentDriverBase _driver;
    private readonly ObservableCollection<LiveEntry> _entries = new();
    private readonly PlotModel _plotModel;
    private readonly LineSeries _series;
    private int _x;

    public LiveDataWindow(InstrumentDriverBase driver)
    {
        _driver = driver;
        Title = $"Live Data — {driver.DriverName}  ({driver.Manufacturer} {driver.Model})";
        Width = 700;
        Height = 520;
        MinWidth = 500;
        MinHeight = 360;
        Background = new SolidColorBrush(Color.FromRgb(0x18, 0x1A, 0x1C));

        // ── OxyPlot chart ────────────────────────────────────────────────────
        _plotModel = new PlotModel
        {
            Background           = OxyColor.FromRgb(0x0D, 0x0F, 0x11),
            PlotAreaBackground   = OxyColor.FromRgb(0x0D, 0x0F, 0x11),
            PlotAreaBorderColor  = OxyColor.FromRgb(0x28, 0x34, 0x3C),
            PlotMargins          = new OxyThickness(52, 4, 8, 28),
        };
        _series = new LineSeries
        {
            Color           = OxyColor.FromRgb(0x00, 0xD8, 0xCC),
            StrokeThickness = 1.5,
            LineStyle       = LineStyle.Solid,
        };
        _plotModel.Axes.Add(new LinearAxis
        {
            Position          = AxisPosition.Bottom,
            Title             = "N",
            TitleColor        = OxyColor.FromRgb(0x50, 0x68, 0x78),
            TitleFontSize     = 9,
            TextColor         = OxyColor.FromRgb(0x60, 0x74, 0x84),
            TicklineColor     = OxyColor.FromRgb(0x28, 0x34, 0x3C),
            AxislineColor     = OxyColor.FromRgb(0x28, 0x34, 0x3C),
            MajorGridlineColor = OxyColor.FromRgb(0x1A, 0x22, 0x2A),
            MajorGridlineStyle = LineStyle.Solid,
            FontSize          = 9,
        });
        _plotModel.Axes.Add(new LinearAxis
        {
            Position          = AxisPosition.Left,
            TextColor         = OxyColor.FromRgb(0x60, 0x74, 0x84),
            TicklineColor     = OxyColor.FromRgb(0x28, 0x34, 0x3C),
            AxislineColor     = OxyColor.FromRgb(0x28, 0x34, 0x3C),
            MajorGridlineColor = OxyColor.FromRgb(0x1A, 0x22, 0x2A),
            MajorGridlineStyle = LineStyle.Solid,
            FontSize          = 9,
        });
        _plotModel.Series.Add(_series);

        var plotView = new PlotView
        {
            Model      = _plotModel,
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0F, 0x11)),
        };

        // ── DataGrid ─────────────────────────────────────────────────────────
        var dataGrid = new DataGrid
        {
            Background              = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x14)),
            Foreground              = new SolidColorBrush(Color.FromRgb(0xB0, 0xBC, 0xC4)),
            BorderBrush             = new SolidColorBrush(Color.FromRgb(0x22, 0x2C, 0x34)),
            BorderThickness         = new Thickness(0, 1, 0, 0),
            GridLinesVisibility     = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x2A)),
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

        // ── Toolbar ──────────────────────────────────────────────────────────
        var clrBtn = new Button
        {
            Content         = "CLR",
            Width           = 54,
            Height          = 26,
            Margin          = new Thickness(6, 4, 6, 4),
            Background      = new SolidColorBrush(Color.FromRgb(0x20, 0x28, 0x30)),
            Foreground      = new SolidColorBrush(Color.FromRgb(0x00, 0xD8, 0xCC)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0x28, 0x38, 0x44)),
            BorderThickness = new Thickness(1),
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 10,
            Cursor          = System.Windows.Input.Cursors.Hand,
        };
        clrBtn.Click += (_, _) =>
        {
            _entries.Clear();
            _series.Points.Clear();
            _x = 0;
            _plotModel.InvalidatePlot(true);
        };

        var titleLabel = new TextBlock
        {
            Text                = $"  {driver.DriverName}  —  {driver.Manufacturer} {driver.Model}",
            Foreground          = new SolidColorBrush(Color.FromRgb(0x70, 0x84, 0x90)),
            FontFamily          = new FontFamily("Consolas"),
            FontSize            = 10,
            VerticalAlignment   = System.Windows.VerticalAlignment.Center,
        };

        var toolbar = new DockPanel
        {
            Background   = new SolidColorBrush(Color.FromRgb(0x1C, 0x20, 0x24)),
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
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(165) });
        Grid.SetRow(plotView, 0);
        Grid.SetRow(toolbar, 1);
        Grid.SetRow(dataGrid, 2);
        grid.Children.Add(plotView);
        grid.Children.Add(toolbar);
        grid.Children.Add(dataGrid);

        Content = grid;

        _driver.MeasurementReceived += OnMeasurement;
        Closed += (_, _) => _driver.MeasurementReceived -= OnMeasurement;
    }

    private void OnMeasurement(object? sender, MeasurementResult result)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _entries.Insert(0, new LiveEntry(DateTime.Now, result.Function, result.Value, result.Unit));
            if (_entries.Count > 300) _entries.RemoveAt(300);

            if (!double.IsNaN(result.Value) && !double.IsInfinity(result.Value))
            {
                _series.Points.Add(new DataPoint(_x++, result.Value));
                if (_series.Points.Count > 300) _series.Points.RemoveAt(0);
                _plotModel.InvalidatePlot(true);
            }
        });
    }
}
