using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HP34401A.Views;

public partial class HP34401AFrontPanelView : UserControl
{
    public HP34401ADriver Driver { get; }

    private HP34401AFrontPanelViewModel ViewModel =>
        (HP34401AFrontPanelViewModel)DataContext;

    public HP34401AFrontPanelView(HP34401ADriver driver)
    {
        Driver = driver;
        InitializeComponent();
        DataContext = new HP34401AFrontPanelViewModel(driver);

        // Set initial checked state after DataContext is set
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetCheckedInGroup(FunctionButtonsPanel, ViewModel.SelectedFunction);
        SetCheckedInGroup(RangeButtonsPanel, ViewModel.SelectedRange);
        SetCheckedInGroup(NplcButtonsPanel, ViewModel.SelectedNplc);
        SetCheckedInGroup(MathButtonsPanel, ViewModel.SelectedMathMode);
    }

    // ── Function button group ────────────────────────────────────────────────

    private void FunctionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            string tag = btn.Tag?.ToString() ?? string.Empty;
            DeselectOthers(FunctionButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedFunction = tag;
        }
    }

    // ── Range button group ───────────────────────────────────────────────────

    private void RangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            string tag = btn.Tag?.ToString() ?? string.Empty;
            DeselectOthers(RangeButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedRange = tag;
        }
    }

    // ── NPLC button group ────────────────────────────────────────────────────

    private void NplcButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            string tag = btn.Tag?.ToString() ?? string.Empty;
            DeselectOthers(NplcButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedNplc = tag;
        }
    }

    // ── MATH button group ────────────────────────────────────────────────────

    private void MathButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            string tag = btn.Tag?.ToString() ?? string.Empty;
            bool wasChecked = btn.IsChecked == true;
            DeselectOthers(MathButtonsPanel, btn);
            if (wasChecked)
            {
                btn.IsChecked = false;
                ViewModel.SetMathModeCommand.Execute("OFF");
            }
            else
            {
                btn.IsChecked = true;
                ViewModel.SetMathModeCommand.Execute(tag);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DeselectOthers(Panel panel, ToggleButton selected)
    {
        foreach (var child in panel.Children)
        {
            if (child is ToggleButton tb && !ReferenceEquals(tb, selected))
                tb.IsChecked = false;
        }
    }

    private static void SetCheckedInGroup(Panel panel, string tag)
    {
        foreach (var child in panel.Children)
        {
            if (child is ToggleButton tb)
                tb.IsChecked = tb.Tag?.ToString() == tag;
        }
    }
}
