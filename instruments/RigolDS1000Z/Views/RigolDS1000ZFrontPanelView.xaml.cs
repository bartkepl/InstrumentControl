using System.Windows.Controls;

namespace RigolDS1000Z.Views;

public partial class RigolDS1000ZFrontPanelView : UserControl
{
    public RigolDS1000ZFrontPanelView(RigolDS1000ZDriver driver)
    {
        InitializeComponent();
        var vm = new RigolDS1000ZFrontPanelViewModel(driver);
        DataContext = vm;
        Unloaded += (_, _) => vm.Detach();
    }
}
