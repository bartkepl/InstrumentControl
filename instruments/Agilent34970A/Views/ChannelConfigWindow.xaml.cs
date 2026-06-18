using System.Windows;

namespace Agilent34970A.Views;

public partial class ChannelConfigWindow : Window
{
    public ChannelConfigWindow()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
