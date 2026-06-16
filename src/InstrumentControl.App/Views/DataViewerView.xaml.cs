using System.Windows.Controls;
using InstrumentControl.App.Services;

namespace InstrumentControl.App.Views;

public partial class DataViewerView : UserControl
{
    public DataViewerView()
    {
        InitializeComponent();
        LocalizationService.LanguageChanged += (_, _) => SetColumnHeaders();
    }

    private void ResultsGrid_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SetColumnHeaders();
    }

    private void SetColumnHeaders()
    {
        if (ResultsGrid.Columns.Count < 7) return;
        ResultsGrid.Columns[0].Header = LocalizationService.Get("DataViewer_ColTime");
        ResultsGrid.Columns[1].Header = LocalizationService.Get("DataViewer_ColInstrument");
        ResultsGrid.Columns[2].Header = LocalizationService.Get("DataViewer_ColChannel");
        ResultsGrid.Columns[3].Header = LocalizationService.Get("DataViewer_ColParameter");
        ResultsGrid.Columns[4].Header = LocalizationService.Get("DataViewer_ColValue");
        ResultsGrid.Columns[5].Header = LocalizationService.Get("DataViewer_ColUnit");
        ResultsGrid.Columns[6].Header = LocalizationService.Get("DataViewer_ColFunction");
    }
}
