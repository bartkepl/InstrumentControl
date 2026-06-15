using System.Windows;
using InstrumentControl.App.ViewModels;

namespace InstrumentControl.App.Views;

public partial class ConnectionManagerWindow : Window
{
    public ConnectionManagerWindow(ConnectionManagerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
