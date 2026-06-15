using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Keithley2000.Views;

public partial class Keithley2000FrontPanelView : UserControl
{
    public Keithley2000Driver Driver { get; }

    private Keithley2000FrontPanelViewModel ViewModel =>
        (Keithley2000FrontPanelViewModel)DataContext;

    public Keithley2000FrontPanelView(Keithley2000Driver driver)
    {
        Driver = driver;
        InitializeComponent();
        DataContext = new Keithley2000FrontPanelViewModel(driver);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetCheckedInGroup(FunctionButtonsPanel, ViewModel.SelectedFunction);
        SetCheckedInGroup(RangeButtonsPanel, ViewModel.SelectedRange);
        SetCheckedInGroup(NplcButtonsPanel, ViewModel.SelectedNplc);
        SetCheckedInGroup(TcButtonsPanel, ViewModel.SelectedTcType);
        SetCheckedInGroup(MathButtonsPanel, ViewModel.SelectedMathMode);
    }

    // ── Function button group ────────────────────────────────────────────────

    private void FunctionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            DeselectOthers(FunctionButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedFunction = btn.Tag?.ToString() ?? string.Empty;
        }
    }

    // ── Range button group ───────────────────────────────────────────────────

    private void RangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            DeselectOthers(RangeButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedRange = btn.Tag?.ToString() ?? string.Empty;
        }
    }

    // ── NPLC button group ────────────────────────────────────────────────────

    private void NplcButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            DeselectOthers(NplcButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedNplc = btn.Tag?.ToString() ?? string.Empty;
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

    // ── TC type button group ─────────────────────────────────────────────────

    private void TcButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            DeselectOthers(TcButtonsPanel, btn);
            btn.IsChecked = true;
            ViewModel.SelectedTcType = btn.Tag?.ToString() ?? "K";
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
