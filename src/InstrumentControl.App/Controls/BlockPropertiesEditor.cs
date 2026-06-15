using System.Windows;
using System.Windows.Controls;
using InstrumentControl.App.ViewModels;
using InstrumentControl.Core.Models;

namespace InstrumentControl.App.Controls;

public class BlockPropertiesEditor : ContentControl
{
    public static readonly DependencyProperty SelectedBlockProperty =
        DependencyProperty.Register(nameof(SelectedBlock), typeof(SequenceBlockVm),
            typeof(BlockPropertiesEditor), new PropertyMetadata(null, OnRebuild));

    public static readonly DependencyProperty InstrumentNamesProperty =
        DependencyProperty.Register(nameof(InstrumentNames), typeof(IEnumerable<string>),
            typeof(BlockPropertiesEditor), new PropertyMetadata(null, OnRebuild));

    public SequenceBlockVm? SelectedBlock
    {
        get => (SequenceBlockVm?)GetValue(SelectedBlockProperty);
        set => SetValue(SelectedBlockProperty, value);
    }

    public IEnumerable<string>? InstrumentNames
    {
        get => (IEnumerable<string>?)GetValue(InstrumentNamesProperty);
        set => SetValue(InstrumentNamesProperty, value);
    }

    private static void OnRebuild(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((BlockPropertiesEditor)d).RebuildEditor();

    private void RebuildEditor()
    {
        if (SelectedBlock == null)
        {
            Content = new TextBlock
            {
                Text = "Zaznacz blok na canvasie",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            return;
        }

        var block = SelectedBlock.Block;
        var props = block.PropertyDefinitions.ToList();

        if (props.Count == 0)
        {
            Content = new TextBlock
            {
                Text = $"Blok '{block.DisplayName}'\n(brak właściwości)",
                FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            return;
        }

        // Header
        var headerBorder = new Border
        {
            Background = SelectedBlock.ColorBrush,
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 4)
        };
        headerBorder.Child = new TextBlock
        {
            Text = block.DisplayName,
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        };

        var panel = new StackPanel();
        panel.Children.Add(headerBorder);

        foreach (var propDef in props)
        {
            var rowPanel = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            rowPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = propDef.DisplayName + ":",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                MaxWidth = 120,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = propDef.DisplayName
            };
            Grid.SetColumn(label, 0);

            var currentValue = block.Properties.TryGetValue(propDef.Name, out var v) ? v : propDef.DefaultValue;
            UIElement editor = CreateEditor(propDef, currentValue, block, propDef.Name);
            Grid.SetColumn(editor, 1);

            rowPanel.Children.Add(label);
            rowPanel.Children.Add(editor);
            panel.Children.Add(rowPanel);
        }

        Content = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private UIElement CreateEditor(BlockPropertyDefinition propDef, object? currentValue,
        InstrumentControl.Core.Interfaces.ISequenceBlock block, string propName)
    {
        string strVal = currentValue?.ToString() ?? propDef.DefaultValue?.ToString() ?? "";

        switch (propDef.EditorType)
        {
            case PropertyEditorType.InstrumentSelector:
            {
                var names = InstrumentNames?.ToList() ?? new List<string>();
                var cb = new ComboBox { FontSize = 11, Height = 22, Padding = new Thickness(4, 0, 0, 0) };
                // Preserve saved name even if instrument is currently not connected
                if (!string.IsNullOrEmpty(strVal) && !names.Contains(strVal))
                    cb.Items.Insert(0, strVal);
                foreach (var n in names) cb.Items.Add(n);
                cb.SelectedItem = !string.IsNullOrEmpty(strVal) ? strVal : names.FirstOrDefault();
                cb.SelectionChanged += (_, _) => block.Properties[propName] = cb.SelectedItem?.ToString();
                return cb;
            }

            case PropertyEditorType.ComboBox:
            {
                var cb = new ComboBox { FontSize = 11, Height = 22, Padding = new Thickness(4, 0, 0, 0) };
                foreach (var opt in propDef.Options) cb.Items.Add(opt);
                cb.SelectedItem = propDef.Options.Contains(strVal) ? strVal : propDef.Options.FirstOrDefault();
                cb.SelectionChanged += (_, _) => block.Properties[propName] = cb.SelectedItem?.ToString();
                return cb;
            }

            case PropertyEditorType.CheckBox:
            {
                bool boolVal = strVal is "True" or "true" or "1";
                var chk = new CheckBox { IsChecked = boolVal, VerticalAlignment = VerticalAlignment.Center };
                chk.Checked += (_, _) => block.Properties[propName] = true;
                chk.Unchecked += (_, _) => block.Properties[propName] = false;
                return chk;
            }

            case PropertyEditorType.NumberBox:
            {
                var tb = new TextBox
                {
                    Text = strVal, FontSize = 11, Height = 22,
                    Padding = new Thickness(4, 0, 0, 0), MaxWidth = 100
                };
                tb.LostFocus += (_, _) =>
                {
                    if (double.TryParse(tb.Text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double d))
                        block.Properties[propName] = d;
                };
                return tb;
            }

            case PropertyEditorType.FilePath:
            {
                var fp = new StackPanel { Orientation = Orientation.Horizontal };
                var tb = new TextBox
                {
                    Text = strVal, FontSize = 11, Height = 22,
                    MinWidth = 60, MaxWidth = 160, Padding = new Thickness(4, 0, 0, 0)
                };
                tb.TextChanged += (_, _) => block.Properties[propName] = tb.Text;
                var btn = new Button
                {
                    Content = "…", Height = 22, Width = 24, Margin = new Thickness(2, 0, 0, 0),
                    FontSize = 11
                };
                btn.Click += (_, _) =>
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv|Wszystkie|*.*" };
                    if (dlg.ShowDialog() == true) { tb.Text = dlg.FileName; block.Properties[propName] = dlg.FileName; }
                };
                fp.Children.Add(tb);
                fp.Children.Add(btn);
                return fp;
            }

            default: // TextBox / VariableName
            {
                var tb = new TextBox
                {
                    Text = strVal, FontSize = 11, Height = 22,
                    Padding = new Thickness(4, 0, 0, 0), MaxWidth = 200
                };
                tb.TextChanged += (_, _) => block.Properties[propName] = tb.Text;
                return tb;
            }
        }
    }
}
