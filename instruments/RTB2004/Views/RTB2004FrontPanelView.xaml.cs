using System.Windows.Controls;

namespace RTB2004.Views;

public partial class RTB2004FrontPanelView : UserControl
{
    public RTB2004Driver Driver { get; }

    public RTB2004FrontPanelView(RTB2004Driver driver)
    {
        Driver = driver;
        InitializeComponent();
        var vm = new RTB2004FrontPanelViewModel(driver);
        DataContext = vm;
        Unloaded += (_, _) => vm.Detach();
    }
}
