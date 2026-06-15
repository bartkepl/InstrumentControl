using System.Windows;
using System.Windows.Controls;
using InstrumentControl.App.ViewModels;

namespace InstrumentControl.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowContext _ctx;

    public MainWindow()
    {
        InitializeComponent();

        _ctx = new MainWindowContext(
            App.VisaService,
            App.PluginLoader,
            App.DataManager);

        DataContext = _ctx;

        // Start with a default empty sequence
        _ctx.SeqVm.NewSequenceCommand.Execute(null);

        // Populate toolbox with only built-in blocks on startup;
        // instrument-specific blocks appear only when instrument is connected
        _ctx.SeqVm.RefreshAvailableBlocks(Enumerable.Empty<InstrumentControl.Core.Interfaces.IInstrumentDriver>());

        // Refresh toolbox whenever the connected instrument list changes (connect OR disconnect)
        _ctx.MainVm.ConnectedInstruments.CollectionChanged += (_, _) =>
            _ctx.SeqVm.RefreshAvailableBlocks(
                _ctx.MainVm.ConnectedInstruments.Select(i => i.Driver));
    }

    private void MenuAddInstrument_Click(object sender, RoutedEventArgs e)
    {
        var vm = new ConnectionManagerViewModel(App.VisaService, App.PluginLoader);
        var dlg = new ConnectionManagerWindow(vm) { Owner = this };
        if (dlg.ShowDialog() == true && vm.ConnectedDriver != null)
        {
            _ctx.MainVm.AddConnectedInstrument(vm.ConnectedDriver);
            MainTabControl.SelectedIndex = 0; // Switch to front panel
        }
    }

    private void InstrumentItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ConnectedInstrumentVm vm)
        {
            _ctx.MainVm.SelectedInstrument = vm;
            MainTabControl.SelectedIndex = 0;
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.VisaService.Dispose();
    }
}

public class MainWindowContext
{
    public MainWindowViewModel MainVm { get; }
    public SequenceEditorViewModel SeqVm { get; }
    public DataViewerViewModel DataVm { get; }

    public MainWindowContext(
        InstrumentControl.Core.Services.VisaService visaService,
        InstrumentControl.Core.Services.PluginLoader pluginLoader,
        InstrumentControl.Core.Services.DataManager dataManager)
    {
        MainVm = new MainWindowViewModel(visaService, pluginLoader, dataManager);
        var engine = new InstrumentControl.Core.Services.SequenceEngine();
        SeqVm = new SequenceEditorViewModel(engine, dataManager, MainVm);
        DataVm = new DataViewerViewModel(dataManager);
    }
}
