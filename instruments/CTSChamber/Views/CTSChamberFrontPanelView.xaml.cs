using System.Windows.Controls;

namespace CTSChamber.Views;

public partial class CTSChamberFrontPanelView : UserControl
{
    public CTSChamberDriver Driver { get; }

    public CTSChamberFrontPanelView(CTSChamberDriver driver)
    {
        Driver = driver;
        InitializeComponent();
        DataContext = new CTSChamberFrontPanelViewModel(driver);
    }
}
