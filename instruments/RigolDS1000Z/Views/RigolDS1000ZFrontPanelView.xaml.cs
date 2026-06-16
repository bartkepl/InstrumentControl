using System.Windows.Controls;

namespace RigolDS1000Z.Views;

public partial class RigolDS1000ZFrontPanelView : UserControl
{
    public RigolDS1000ZFrontPanelView(RigolDS1000ZDriver driver)
    {
        InitializeComponent();
        DataContext = new RigolDS1000ZFrontPanelViewModel(driver);
    }
}
