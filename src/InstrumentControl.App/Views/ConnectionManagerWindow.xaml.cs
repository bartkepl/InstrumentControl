using System.Windows;
using InstrumentControl.App.ViewModels;

namespace InstrumentControl.App.Views;

public partial class ConnectionManagerWindow : Window
{
    public ConnectionManagerWindow(ConnectionManagerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Closed += (_, _) => vm.CancelPending();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionManagerViewModel vm)
            vm.RefreshResourcesCommand.Execute(null);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
