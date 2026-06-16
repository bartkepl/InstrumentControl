using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using InstrumentControl.App.ViewModels;
using InstrumentControl.Core.Interfaces;

namespace InstrumentControl.App.Controls;

public class BlockDroppedEventArgs(ISequenceBlock block, double x, double y) : EventArgs
{
    public ISequenceBlock Block { get; } = block;
    public double X { get; } = x;
    public double Y { get; } = y;
}

public class BlockConnectedEventArgs(string fromId, string toId, string portType = "Next") : EventArgs
{
    public string FromBlockId { get; } = fromId;
    public string ToBlockId { get; } = toId;
    public string PortType { get; } = portType;
}

public class BlockDeletedEventArgs(string blockId) : EventArgs
{
    public string BlockId { get; } = blockId;
}

public class ConnectionDeletedEventArgs(string fromBlockId, string portType) : EventArgs
{
    public string FromBlockId { get; } = fromBlockId;
    public string PortType { get; } = portType;
}

// Inheriting from Border avoids the GetVisualChild/VisualChildrenCount override trap.
public class BlockCanvas : Border
{
    private readonly Canvas _canvas;
    private readonly Dictionary<string, BlockVisual> _blockVisuals = new();
    private readonly List<ConnectionVisual> _connectionVisuals = new();

    private readonly ScrollViewer _scroll;
    private double _scale = 1.0;

    private BlockVisual? _dragging;
    private Point _dragOffset;
    private string? _connectingFromId;
    private string _connectingPortType = "Next";
    private Line? _tempLine;

    public static readonly DependencyProperty BlocksProperty =
        DependencyProperty.Register(nameof(Blocks), typeof(ObservableCollection<SequenceBlockVm>),
            typeof(BlockCanvas), new PropertyMetadata(null, OnBlocksChanged));

    public static readonly DependencyProperty ConnectionsProperty =
        DependencyProperty.Register(nameof(Connections), typeof(ObservableCollection<ConnectionVm>),
            typeof(BlockCanvas), new PropertyMetadata(null, OnConnectionsChanged));

    public static readonly DependencyProperty SelectedBlockProperty =
        DependencyProperty.Register(nameof(SelectedBlock), typeof(SequenceBlockVm),
            typeof(BlockCanvas), new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ExecutingBlockIdProperty =
        DependencyProperty.Register(nameof(ExecutingBlockId), typeof(string),
            typeof(BlockCanvas), new PropertyMetadata(string.Empty, OnExecutingBlockChanged));

    public ObservableCollection<SequenceBlockVm>? Blocks
    {
        get => (ObservableCollection<SequenceBlockVm>?)GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }
    public ObservableCollection<ConnectionVm>? Connections
    {
        get => (ObservableCollection<ConnectionVm>?)GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }
    public SequenceBlockVm? SelectedBlock
    {
        get => (SequenceBlockVm?)GetValue(SelectedBlockProperty);
        set => SetValue(SelectedBlockProperty, value);
    }
    public string ExecutingBlockId
    {
        get => (string)GetValue(ExecutingBlockIdProperty);
        set => SetValue(ExecutingBlockIdProperty, value);
    }

    public event EventHandler<BlockDroppedEventArgs>? BlockDropped;
    public event EventHandler<BlockConnectedEventArgs>? BlockConnected;
    public event EventHandler<BlockDeletedEventArgs>? BlockDeleted;
    public event EventHandler<ConnectionDeletedEventArgs>? ConnectionDeleted;

    public BlockCanvas()
    {
        _canvas = new Canvas
        {
            Width = 3000,
            Height = 2000,
            Background = BuildGridBrush()
        };

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _canvas
        };

        Child = _scroll;
        AllowDrop = true;
        Focusable = true;

        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += Canvas_MouseUp;
        _scroll.PreviewMouseWheel += Scroll_PreviewMouseWheel;
        PreviewKeyDown += Canvas_PreviewKeyDown;

        Drop += Canvas_Drop;
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
    }

    // ── Dependency property callbacks ──────────────────────────────────────

    private static void OnBlocksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (BlockCanvas)d;
        if (e.OldValue is ObservableCollection<SequenceBlockVm> old)
            old.CollectionChanged -= c.Blocks_Changed;
        if (e.NewValue is ObservableCollection<SequenceBlockVm> nw)
        {
            nw.CollectionChanged += c.Blocks_Changed;
            c.RebuildAll();
        }
    }

    private static void OnConnectionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (BlockCanvas)d;
        if (e.OldValue is ObservableCollection<ConnectionVm> old)
            old.CollectionChanged -= c.Connections_Changed;
        if (e.NewValue is ObservableCollection<ConnectionVm> nw)
        {
            nw.CollectionChanged += c.Connections_Changed;
            c.RedrawConnections();
        }
    }

    private static void OnExecutingBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (BlockCanvas)d;
        var id = (string?)e.NewValue ?? string.Empty;
        foreach (var kv in c._blockVisuals)
            kv.Value.SetExecuting(kv.Key == id);
    }

    private void Blocks_Changed(object? s, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            foreach (SequenceBlockVm vm in e.NewItems) AddBlockVisual(vm);
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            foreach (SequenceBlockVm vm in e.OldItems) RemoveBlockVisual(vm.Block.BlockId);
        else
            RebuildAll();
    }

    private void Connections_Changed(object? s, NotifyCollectionChangedEventArgs e)
        => RedrawConnections();

    // ── Block management ──────────────────────────────────────────────────

    private void RebuildAll()
    {
        _canvas.Children.Clear();
        _blockVisuals.Clear();
        _connectionVisuals.Clear();
        if (Blocks == null) return;
        foreach (var vm in Blocks) AddBlockVisual(vm);
        // Defer until layout pass so TranslatePoint works correctly for body ports
        _canvas.Dispatcher.BeginInvoke(RedrawConnections, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void AddBlockVisual(SequenceBlockVm vm)
    {
        var bv = new BlockVisual(vm);
        _blockVisuals[vm.Block.BlockId] = bv;
        _canvas.Children.Add(bv.Root);
        Canvas.SetLeft(bv.Root, vm.X);
        Canvas.SetTop(bv.Root, vm.Y);
        Canvas.SetZIndex(bv.Root, 10);

        // Header drag
        bv.Root.MouseLeftButtonDown += (s, e) =>
        {
            if (e.Source == bv.OutputPort || e.Source == bv.BodyPort) return;
            _dragging = bv;
            _dragOffset = e.GetPosition(bv.Root);
            bv.Root.CaptureMouse();
            SelectedBlock = vm;
            foreach (var kv in _blockVisuals) kv.Value.SetSelected(kv.Key == vm.Block.BlockId);
            Focus();
            e.Handled = true;
        };

        // Right-click context menu
        bv.Root.MouseRightButtonDown += (s, e) => ShowContextMenu(bv, e);

        // Output port (Next) → start connection
        bv.OutputPort.MouseLeftButtonDown += (s, e) =>
        {
            _connectingFromId = vm.Block.BlockId;
            _connectingPortType = "Next";
            StartTempLine(bv.GetOutputPortCenter(_canvas));
            e.Handled = true;
        };

        // Body port (Loop body) → start connection
        if (bv.BodyPort != null)
        {
            bv.BodyPort.MouseLeftButtonDown += (s, e) =>
            {
                _connectingFromId = vm.Block.BlockId;
                _connectingPortType = "Body";
                StartTempLine(bv.GetBodyPortCenter(_canvas));
                e.Handled = true;
            };
        }
    }

    private void StartTempLine(Point from)
    {
        _tempLine = new Line
        {
            X1 = from.X, Y1 = from.Y, X2 = from.X, Y2 = from.Y,
            Stroke = _connectingPortType == "Body" ? Brushes.Orange : Brushes.Yellow,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        _canvas.Children.Add(_tempLine);
        Canvas.SetZIndex(_tempLine, 50);
    }

    private void RemoveBlockVisual(string blockId)
    {
        if (_blockVisuals.TryGetValue(blockId, out var bv))
        {
            _canvas.Children.Remove(bv.Root);
            _blockVisuals.Remove(blockId);
        }
        RedrawConnections();
    }

    // ── Mouse events on canvas ────────────────────────────────────────────

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(_canvas);
            double x = Snap(pos.X - _dragOffset.X);
            double y = Snap(pos.Y - _dragOffset.Y);
            Canvas.SetLeft(_dragging.Root, x);
            Canvas.SetTop(_dragging.Root, y);
            _dragging.Vm.X = x;
            _dragging.Vm.Y = y;
            RedrawConnections();
        }

        if (_connectingFromId != null && _tempLine != null)
        {
            var pos = e.GetPosition(_canvas);
            _tempLine.X2 = pos.X;
            _tempLine.Y2 = pos.Y;
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging != null)
        {
            _dragging.Root.ReleaseMouseCapture();
            _dragging = null;
        }

        if (_connectingFromId != null)
        {
            if (_tempLine != null) { _canvas.Children.Remove(_tempLine); _tempLine = null; }

            // Hit-test: find input port of another block under cursor
            var pos = e.GetPosition(_canvas);
            foreach (var kv in _blockVisuals)
            {
                if (kv.Key == _connectingFromId) continue;
                var pt = kv.Value.GetInputPortCenter(_canvas);
                if (Math.Abs(pos.X - pt.X) < 24 && Math.Abs(pos.Y - pt.Y) < 24)
                {
                    BlockConnected?.Invoke(this,
                        new BlockConnectedEventArgs(_connectingFromId, kv.Key, _connectingPortType));
                    break;
                }
            }
            _connectingFromId = null;
            _connectingPortType = "Next";
        }
    }

    private void ShowContextMenu(BlockVisual bv, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        var del = new MenuItem { Header = "Usuń blok" };
        del.Click += (_, _) => BlockDeleted?.Invoke(this, new BlockDeletedEventArgs(bv.Vm.Block.BlockId));
        menu.Items.Add(del);

        var disc = new MenuItem { Header = "Odłącz połączenia" };
        disc.Click += (_, _) =>
        {
            bv.Vm.Block.NextBlockId = null;
            if (bv.Vm.Block is IHasBodyOutput bbo) bbo.BodyBlockId = null;
            Connections?.RemoveWhere(c => c.FromBlockId == bv.Vm.Block.BlockId);
            RedrawConnections();
        };
        menu.Items.Add(disc);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void Scroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        _scale = Math.Clamp(_scale + (e.Delta > 0 ? 0.1 : -0.1), 0.25, 3.0);
        _canvas.LayoutTransform = new ScaleTransform(_scale, _scale);
        e.Handled = true;
    }

    private void Canvas_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && SelectedBlock != null)
        {
            BlockDeleted?.Invoke(this, new BlockDeletedEventArgs(SelectedBlock.Block.BlockId));
            e.Handled = true;
        }
    }

    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData("SequenceBlock") is ISequenceBlock block)
        {
            var pos = e.GetPosition(_canvas);
            BlockDropped?.Invoke(this, new BlockDroppedEventArgs(block, Snap(pos.X), Snap(pos.Y)));
        }
    }

    // ── Connection drawing ────────────────────────────────────────────────

    private void RedrawConnections()
    {
        foreach (var cv in _connectionVisuals)
        {
            _canvas.Children.Remove(cv.VisualPath);
            _canvas.Children.Remove(cv.HitPath);
        }
        _connectionVisuals.Clear();

        if (Connections == null) return;
        foreach (var conn in Connections)
        {
            if (!_blockVisuals.TryGetValue(conn.FromBlockId, out var from)) continue;
            if (!_blockVisuals.TryGetValue(conn.ToBlockId, out var to)) continue;

            var fromPt = conn.PortType == "Body"
                ? from.GetBodyPortCenter(_canvas)
                : from.GetOutputPortCenter(_canvas);

            var lineColor = conn.PortType == "Body"
                ? Color.FromRgb(0xE6, 0x7E, 0x22)   // orange for loop body
                : Color.FromRgb(80, 200, 120);        // green for next

            var cv = new ConnectionVisual(fromPt, to.GetInputPortCenter(_canvas), lineColor, conn.PortType);
            cv.DeleteRequested += (_, _) =>
                ConnectionDeleted?.Invoke(this, new ConnectionDeletedEventArgs(conn.FromBlockId, conn.PortType));

            _connectionVisuals.Add(cv);
            _canvas.Children.Add(cv.VisualPath);
            Canvas.SetZIndex(cv.VisualPath, 5);
            _canvas.Children.Add(cv.HitPath);
            Canvas.SetZIndex(cv.HitPath, 6);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static double Snap(double v) => Math.Max(0, Math.Round(v / 20.0) * 20.0);

    private static Brush BuildGridBrush()
    {
        var dg = new DrawingGroup();
        var dc = dg.Open();
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(28, 28, 32)), null, new Rect(0, 0, 20, 20));
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 0.5);
        dc.DrawLine(gridPen, new Point(0, 0), new Point(20, 0));
        dc.DrawLine(gridPen, new Point(0, 0), new Point(0, 20));
        dc.Close();
        return new DrawingBrush(dg)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 20, 20),
            ViewportUnits = BrushMappingMode.Absolute
        };
    }
}

// ── BlockVisual ─────────────────────────────────────────────────────────────

internal class BlockVisual
{
    public SequenceBlockVm Vm { get; }
    public Border Root { get; }
    public Border OutputPort { get; }
    public Border? BodyPort { get; }

    public BlockVisual(SequenceBlockVm vm)
    {
        Vm = vm;
        var col = vm.Color;
        var brush = new SolidColorBrush(col);
        var darkBrush = new SolidColorBrush(
            Color.FromRgb((byte)(col.R * 0.6), (byte)(col.G * 0.6), (byte)(col.B * 0.6)));

        // Input port (left circle)
        var inputPort = new Ellipse
        {
            Width = 11, Height = 11,
            Fill = Brushes.White, Stroke = brush, StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-7, 0, 0, 0),
            ToolTip = "Wejście"
        };

        // Output port (right circle) — exit / next
        OutputPort = new Border
        {
            Width = 11, Height = 11, CornerRadius = new CornerRadius(6),
            Background = brush, BorderBrush = darkBrush, BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, -7, 0),
            Cursor = Cursors.Cross,
            ToolTip = "Wyjście (następny blok)"
        };

        var header = new Grid { Height = 30 };
        header.Children.Add(inputPort);
        header.Children.Add(new TextBlock
        {
            Text = vm.DisplayName,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0)
        });
        header.Children.Add(OutputPort);

        var headerBorder = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(5, 5, 0, 0),
            Child = header
        };

        // Body section — category label + optional body port for loop blocks
        var bodyStack = new StackPanel();
        bodyStack.Children.Add(new TextBlock
        {
            Text = vm.Block.Category,
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        bool hasBodyPort = vm.Block is IHasBodyOutput;
        if (hasBodyPort)
        {
            var loopBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
            var loopDarkBrush = new SolidColorBrush(Color.FromRgb(0x90, 0x50, 0x14));

            bodyStack.Children.Add(new TextBlock
            {
                Text = "↓ ciało",
                FontSize = 8,
                Foreground = loopBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            BodyPort = new Border
            {
                Width = 11, Height = 11, CornerRadius = new CornerRadius(6),
                Background = loopBrush, BorderBrush = loopDarkBrush, BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Cursor = Cursors.Cross,
                ToolTip = "Ciało pętli — przeciągnij do pierwszego bloku w pętli"
            };
            bodyStack.Children.Add(BodyPort);
        }

        var body = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 48)),
            CornerRadius = new CornerRadius(0, 0, 5, 5),
            Padding = hasBodyPort
                ? new Thickness(8, 4, 8, 8)   // extra bottom padding for body port
                : new Thickness(8, 4, 8, 4),
            Child = bodyStack
        };

        Root = new Border
        {
            Width = 160,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            ClipToBounds = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10, ShadowDepth = 3, Opacity = 0.5, Color = Colors.Black
            },
            Child = new StackPanel { Children = { headerBorder, body } }
        };
    }

    public void SetExecuting(bool executing)
    {
        Root.BorderBrush = executing
            ? Brushes.Yellow
            : (Vm.IsSelected ? Brushes.White : Brushes.Transparent);
    }

    public void SetSelected(bool selected)
    {
        Vm.IsSelected = selected;
        if (!Vm.IsExecuting)
            Root.BorderBrush = selected ? Brushes.White : Brushes.Transparent;
    }

    public Point GetOutputPortCenter(Canvas canvas)
    {
        double left = Canvas.GetLeft(Root);
        double top = Canvas.GetTop(Root);
        return new Point(left + Root.Width + 7, top + 15);
    }

    public Point GetInputPortCenter(Canvas canvas)
    {
        double left = Canvas.GetLeft(Root);
        double top = Canvas.GetTop(Root);
        return new Point(left - 7, top + 15);
    }

    public Point GetBodyPortCenter(Canvas canvas)
    {
        if (BodyPort != null)
            return BodyPort.TranslatePoint(new Point(5.5, 5.5), canvas);

        double left = Canvas.GetLeft(Root);
        double top = Canvas.GetTop(Root);
        return new Point(left + Root.Width / 2.0, top + (Root.ActualHeight > 10 ? Root.ActualHeight : 68));
    }
}

// ── ConnectionVisual ─────────────────────────────────────────────────────────

internal class ConnectionVisual
{
    public Path VisualPath { get; }
    public Path HitPath { get; }

    public event EventHandler? DeleteRequested;

    public ConnectionVisual(Point from, Point to, Color? color = null, string portType = "Next")
    {
        var connColor = color ?? Color.FromRgb(80, 200, 120);

        // Direction-aware bezier control points
        Point cp1, cp2;
        if (portType == "Body")
        {
            // Body port exits downward; use vertical first control, horizontal approach to target
            double vOff = Math.Max(60.0, Math.Abs(to.Y - from.Y) * 0.5);
            double hOff = Math.Max(50.0, Math.Abs(to.X - from.X) * 0.4);
            cp1 = new Point(from.X, from.Y + vOff);
            cp2 = new Point(to.X - hOff, to.Y);
        }
        else
        {
            // Next port exits to the right; S-curve that works for all relative positions
            double hOff = Math.Max(80.0, Math.Abs(to.X - from.X) * 0.5);
            cp1 = new Point(from.X + hOff, from.Y);
            cp2 = new Point(to.X - hOff, to.Y);
        }

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = from, IsClosed = false, IsFilled = false };
        fig.Segments.Add(new BezierSegment(cp1, cp2, to, isStroked: true));
        geo.Figures.Add(fig);

        // Arrow — direction based on tangent at 'to' (from cp2 toward to)
        double dx = to.X - cp2.X, dy = to.Y - cp2.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { dx = 1; dy = 0; len = 1; }
        double nx = dx / len, ny = dy / len;
        const double arrowLen = 10, arrowHalf = 4;
        var p1 = new Point(to.X - arrowLen * nx - arrowHalf * ny,
                           to.Y - arrowLen * ny + arrowHalf * nx);
        var p2 = new Point(to.X - arrowLen * nx + arrowHalf * ny,
                           to.Y - arrowLen * ny - arrowHalf * nx);

        var arrow = new StreamGeometry();
        using (var ctx = arrow.Open())
        {
            ctx.BeginFigure(to, isFilled: true, isClosed: true);
            ctx.LineTo(p1, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(p2, isStroked: true, isSmoothJoin: false);
        }
        arrow.Freeze();

        var combined = new GeometryGroup();
        combined.Children.Add(geo);
        combined.Children.Add(arrow);

        VisualPath = new Path
        {
            Data = combined,
            Stroke = new SolidColorBrush(connColor),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(connColor),
            IsHitTestVisible = false
        };

        // Wide transparent path for hit-testing (easier to click than 2px stroke)
        var hitGeo = new PathGeometry();
        var hitFig = new PathFigure { StartPoint = from, IsClosed = false };
        hitFig.Segments.Add(new BezierSegment(cp1, cp2, to, isStroked: true));
        hitGeo.Figures.Add(hitFig);

        HitPath = new Path
        {
            Data = hitGeo,
            Stroke = Brushes.Transparent,
            StrokeThickness = 10,
            Fill = Brushes.Transparent,
            IsHitTestVisible = true,
            Cursor = Cursors.Hand
        };

        var menu = new ContextMenu();
        var del = new MenuItem { Header = "Usuń połączenie" };
        del.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(del);
        HitPath.ContextMenu = menu;
    }
}

// ── Extension helpers ─────────────────────────────────────────────────────────

internal static class ObservableCollectionExtensions
{
    public static void RemoveWhere<T>(this ObservableCollection<T> col, Func<T, bool> pred)
    {
        foreach (var item in col.Where(pred).ToList()) col.Remove(item);
    }
}
