using System.Windows.Controls;

namespace ItechIT6922B.Views;

public partial class ItechIT6922BFrontPanelView : UserControl
{
    public ItechIT6922BDriver Driver { get; }

    public ItechIT6922BFrontPanelView(ItechIT6922BDriver driver)
    {
        Driver = driver;
        InitializeComponent();
        DataContext = new ItechIT6922BFrontPanelViewModel(driver);
    }
}
