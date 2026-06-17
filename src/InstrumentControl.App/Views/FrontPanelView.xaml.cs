using System.Windows;
using System.Windows.Controls;
using InstrumentControl.App.Services;
using InstrumentControl.App.ViewModels;

namespace InstrumentControl.App.Views;

public partial class FrontPanelView : UserControl
{
    public FrontPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ConnectedInstrumentVm vm)
        {
            ShowInstrumentPanel(vm);
        }
        else
        {
            NoInstrumentPanel.Visibility = Visibility.Visible;
            InstrumentPanelContainer.Visibility = Visibility.Collapsed;
            FrontPanelContent.Content = null;
        }
    }

    private void ShowInstrumentPanel(ConnectedInstrumentVm vm)
    {
        InstrumentTitle.Text = vm.DisplayName;
        InstrumentResource.Text = vm.ResourceName;
        NoInstrumentPanel.Visibility = Visibility.Collapsed;
        InstrumentPanelContainer.Visibility = Visibility.Visible;

        try
        {
            FrontPanelContent.Content = vm.Driver.CreateFrontPanel();
        }
        catch (Exception ex)
        {
            FrontPanelContent.Content = new TextBlock
            {
                Text = $"{LocalizationService.Get("VM_ErrorLoading")} {ex.Message}",
                Foreground = System.Windows.Media.Brushes.Red,
                Margin = new Thickness(16)
            };
        }
    }
}
