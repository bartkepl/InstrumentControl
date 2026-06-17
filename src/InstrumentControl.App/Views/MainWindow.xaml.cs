using System.Windows;
using System.Windows.Controls;
using InstrumentControl.App.ViewModels;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Services;

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
            App.DataManager,
            App.LogService);

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

        // Auto-scroll each log TextBox to bottom when new text arrives
        LogBoxAll.TextChanged        += (_, _) => LogScrollAll.ScrollToBottom();
        LogBoxSeq.TextChanged        += (_, _) => LogScrollSeq.ScrollToBottom();
        LogBoxVisa.TextChanged       += (_, _) => LogScrollVisa.ScrollToBottom();
        LogBoxSerial.TextChanged     += (_, _) => LogScrollSerial.ScrollToBottom();
        LogBoxEvent.TextChanged      += (_, _) => LogScrollEvent.ScrollToBottom();
        LogBoxInstrument.TextChanged += (_, _) => LogScrollInstrument.ScrollToBottom();
        LogBoxDebug.TextChanged      += (_, _) => LogScrollDebug.ScrollToBottom();
    }

    private void MenuAddInstrument_Click(object sender, RoutedEventArgs e)
    {
        var vm = new ConnectionManagerViewModel(App.VisaService, App.PluginLoader, _ctx.MainVm);
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

    private void AboutButton_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.VisaService.Dispose();
        App.LogService.Dispose();
    }
}

public class MainWindowContext
{
    public MainWindowViewModel MainVm { get; }
    public SequenceEditorViewModel SeqVm { get; }
    public DataViewerViewModel DataVm { get; }
    public LogViewModel LogVm { get; }

    public MainWindowContext(
        VisaService visaService,
        PluginLoader pluginLoader,
        DataManager dataManager,
        ILogService logService)
    {
        LogVm  = new LogViewModel(logService);
        MainVm = new MainWindowViewModel(visaService, pluginLoader, dataManager, logService);
        var engine = new SequenceEngine();
        SeqVm  = new SequenceEditorViewModel(engine, dataManager, MainVm, logService);
        DataVm = new DataViewerViewModel(dataManager);
    }
}
