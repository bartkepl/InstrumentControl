using System.Windows.Controls;

namespace Agilent34970A.Views;

public partial class Agilent34970AFrontPanelView : UserControl
{
    private readonly Agilent34970ADriver _driver;

    public Agilent34970AFrontPanelView(Agilent34970ADriver driver)
    {
        _driver = driver;
        InitializeComponent();
        DataContext = new Agilent34970AFrontPanelViewModel(driver);
    }
}
