using System.Windows;

namespace Agilent34970A.Views;

public partial class ScanConfigWindow : Window
{
    public ScanConfigWindow()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
