using System.Windows;
using System.Windows.Controls;
using InstrumentControl.App.Controls;
using InstrumentControl.App.ViewModels;
using InstrumentControl.Core.Interfaces;

namespace InstrumentControl.App.Views;

public partial class SequenceEditorView : UserControl
{
    private StackPanel? _toolboxPanel;
    private bool _toolboxRebuildPending;

    public SequenceEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SequenceEditorViewModel vm)
        {
            BuildToolbox(vm);
            vm.AvailableBlockTemplates.CollectionChanged += (_, _) => ScheduleToolboxRebuild(vm);
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SequenceEditorViewModel.LogText))
                    LogTextBox.ScrollToEnd();
            };
        }
    }

    private void ScheduleToolboxRebuild(SequenceEditorViewModel vm)
    {
        if (_toolboxRebuildPending) return;
        _toolboxRebuildPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            BuildToolbox(vm);
            _toolboxRebuildPending = false;
        });
    }

    private void BuildToolbox(SequenceEditorViewModel vm)
    {
        if (_toolboxPanel == null)
        {
            _toolboxPanel = new StackPanel();
            ToolboxScrollViewer.Content = _toolboxPanel;
        }

        _toolboxPanel.Children.Clear();

        if (!vm.AvailableBlockTemplates.Any())
        {
            _toolboxPanel.Children.Add(new TextBlock
            {
                Text = "Brak bloków.\nUruchom aplikację z instrumentami.",
                Foreground = System.Windows.Media.Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(8, 16, 8, 8),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var grouped = vm.AvailableBlockTemplates
            .GroupBy(b => b.Category)
            .OrderBy(g => g.Key switch
            {
                "Sterowanie" => 0,
                "Ogólne" => 1,
                "Dane" => 2,
                _ => 99
            });

        foreach (var group in grouped)
        {
            var header = new Border
            {
                Background = System.Windows.Media.Brushes.WhiteSmoke,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            header.Child = new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Gray
            };
            _toolboxPanel.Children.Add(header);

            foreach (var block in group)
                _toolboxPanel.Children.Add(CreateToolboxItem(block, vm));
        }
    }

    private UIElement CreateToolboxItem(ISequenceBlock block, SequenceEditorViewModel vm)
    {
        var color = block.BlockColor;
        bool connected = vm.IsCategoryConnected(block.Category);

        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));

        var border = new Border
        {
            Margin = new Thickness(4, 2, 4, 2),
            Padding = new Thickness(8, 5, 8, 5),
            CornerRadius = new CornerRadius(4),
            Background = brush,
            Opacity = connected ? 1.0 : 0.35,
            Cursor = connected
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.No,
            ToolTip = connected
                ? block.Description
                : $"Instrument '{block.Category}' nie jest podłączony"
        };

        border.Child = new TextBlock
        {
            Text = block.DisplayName,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };

        if (!connected)
            return border; // grayed — no drag/drop or double-click

        border.MouseMove += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var data = new DataObject("SequenceBlock", block);
                DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
            }
        };

        border.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
                vm.AddBlock(block, 200 + new Random().Next(0, 300), 100 + new Random().Next(0, 200));
        };

        return border;
    }

    private void TheBlockCanvas_BlockDropped(object sender, BlockDroppedEventArgs e)
    {
        if (DataContext is SequenceEditorViewModel vm)
            vm.AddBlock(e.Block, e.X, e.Y);
    }

    private void TheBlockCanvas_BlockConnected(object sender, BlockConnectedEventArgs e)
    {
        if (DataContext is SequenceEditorViewModel vm)
            vm.ConnectBlocks(e.FromBlockId, e.ToBlockId, e.PortType);
    }

    private void TheBlockCanvas_ConnectionDeleted(object sender, ConnectionDeletedEventArgs e)
    {
        if (DataContext is SequenceEditorViewModel vm)
            vm.RemoveConnection(e.FromBlockId, e.PortType);
    }

    private void TheBlockCanvas_BlockDeleted(object sender, BlockDeletedEventArgs e)
    {
        if (DataContext is SequenceEditorViewModel vm)
        {
            var blockVm = vm.Blocks.FirstOrDefault(b => b.Block.BlockId == e.BlockId);
            if (blockVm != null) vm.RemoveBlock(blockVm);
        }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SequenceEditorViewModel vm)
            vm.LogText = string.Empty;
    }
}
