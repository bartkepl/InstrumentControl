using System.Windows.Controls;

namespace CTSChamber.Views;

public partial class CTSChamberFrontPanelView : UserControl
{
    public CTSChamberDriver Driver { get; }

    public CTSChamberFrontPanelView(CTSChamberDriver driver)
    {
        Driver = driver;
        InitializeComponent();
        var vm = new CTSChamberFrontPanelViewModel(driver);
        DataContext = vm;
        Unloaded += (_, _) => vm.Detach();
    }
}
