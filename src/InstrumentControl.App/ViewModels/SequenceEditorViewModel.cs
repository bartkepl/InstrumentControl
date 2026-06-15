using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.App.Controls;
using InstrumentControl.App.Views;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App.ViewModels;

public partial class SequenceBlockVm : ObservableObject
{
    public ISequenceBlock Block { get; }

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isExecuting;

    public string DisplayName => Block.DisplayName;
    public string BlockType => Block.BlockType;
    public string Category => Block.Category;
    public System.Windows.Media.Color Color => Block.BlockColor;
    public System.Windows.Media.SolidColorBrush ColorBrush =>
        new(Block.BlockColor);

    public SequenceBlockVm(ISequenceBlock block, double x = 0, double y = 0)
    {
        Block = block;
        _x = x; _y = y;
        Block.X = x; Block.Y = y;
    }

    partial void OnXChanged(double value) => Block.X = value;
    partial void OnYChanged(double value) => Block.Y = value;
}

public class ConnectionVm
{
    public string FromBlockId { get; set; } = string.Empty;
    public string ToBlockId { get; set; } = string.Empty;
    public string PortType { get; set; } = "Next";
}

public partial class SequenceEditorViewModel : ViewModelBase
{
    private static readonly HashSet<string> _builtinCategories =
        new(StringComparer.OrdinalIgnoreCase) { "Sterowanie", "Ogólne", "Dane" };

    private readonly SequenceEngine _engine;
    private readonly DataManager _dataManager;
    private MainWindowViewModel _mainVm;
    private readonly Stack<SequenceDefinition> _undoStack = new();

    [ObservableProperty] private ObservableCollection<SequenceBlockVm> _blocks = new();
    [ObservableProperty] private ObservableCollection<ConnectionVm> _connections = new();
    [ObservableProperty] private SequenceBlockVm? _selectedBlock;
    [ObservableProperty] private string _sequenceName = "Nowa sekwencja";
    [ObservableProperty] private string _currentFilePath = string.Empty;
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private string _sequenceStatus = "Gotowy";
    [ObservableProperty] private string _logText = string.Empty;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _executingBlockId = string.Empty;

    public ObservableCollection<ISequenceBlock> AvailableBlockTemplates { get; } = new();

    // Set of driver categories (DriverName with spaces stripped) that are currently connected.
    public HashSet<string> ConnectedCategories { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Instrument names for InstrumentSelector ComboBoxes.
    public ObservableCollection<string> ConnectedInstrumentNames { get; } = new();

    public SequenceEditorViewModel(SequenceEngine engine, DataManager dataManager, MainWindowViewModel mainVm)
    {
        _engine = engine;
        _dataManager = dataManager;
        _mainVm = mainVm;

        _engine.BlockExecuting += (_, id) => RunOnUi(() =>
        {
            ExecutingBlockId = id;
            foreach (var b in Blocks) b.IsExecuting = b.Block.BlockId == id;
        });
        _engine.BlockCompleted += (_, _) => RunOnUi(() =>
        {
            foreach (var b in Blocks) b.IsExecuting = false;
        });
        _engine.LogMessage += (_, msg) => RunOnUi(() => LogText += msg + "\n");
        _engine.StateChanged += (_, state) => RunOnUi(() =>
        {
            IsRunning = state == SequenceState.Running || state == SequenceState.Paused;
            IsPaused = state == SequenceState.Paused;
            SequenceStatus = state.ToString();
        });
        _engine.Error += (_, ex) => RunOnUi(() =>
        {
            LogText += $"BŁĄD: {ex.Message}\n";
            ErrorWindow.ShowException("Błąd sekwencji", ex);
        });
    }

    public void RefreshAvailableBlocks(IEnumerable<IInstrumentDriver> drivers)
    {
        var driverList = drivers.ToList();

        ConnectedCategories = driverList
            .Select(d => d.DriverName.Replace(" ", ""))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ConnectedInstrumentNames.Clear();
        foreach (var name in _mainVm.GetInstrumentNames())
            ConnectedInstrumentNames.Add(name);

        AvailableBlockTemplates.Clear();
        var addedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in BlockRegistry.GetRegisteredTypes())
        {
            var block = BlockRegistry.Create(type);
            if (block != null && addedTypes.Add(block.BlockType))
                AvailableBlockTemplates.Add(block);
        }

        foreach (var driver in driverList)
        {
            foreach (var block in driver.GetAvailableBlocks())
            {
                if (addedTypes.Add(block.BlockType))
                    AvailableBlockTemplates.Add(block);
            }
        }
    }

    public bool IsCategoryConnected(string category) =>
        _builtinCategories.Contains(category) || ConnectedCategories.Contains(category);

    public void AddBlock(ISequenceBlock template, double x, double y)
    {
        var newBlock = template.Clone();
        newBlock.X = x; newBlock.Y = y;
        var vm = new SequenceBlockVm(newBlock, x, y);
        Blocks.Add(vm);
        SelectedBlock = vm;
        IsModified = true;
    }

    public void RemoveBlock(SequenceBlockVm vm)
    {
        Connections.RemoveWhere(c => c.FromBlockId == vm.Block.BlockId || c.ToBlockId == vm.Block.BlockId);
        foreach (var b in Blocks)
        {
            if (b.Block.NextBlockId == vm.Block.BlockId)
                b.Block.NextBlockId = null;
            if (b.Block is IHasBodyOutput bbo && bbo.BodyBlockId == vm.Block.BlockId)
                bbo.BodyBlockId = null;
        }
        Blocks.Remove(vm);
        if (SelectedBlock == vm) SelectedBlock = null;
        IsModified = true;
    }

    public void ConnectBlocks(string fromId, string toId, string portType = "Next")
    {
        var from = Blocks.FirstOrDefault(b => b.Block.BlockId == fromId);
        if (from == null) return;

        Connections.RemoveWhere(c => c.FromBlockId == fromId && c.PortType == portType);

        if (portType == "Body" && from.Block is IHasBodyOutput bodyBlock)
            bodyBlock.BodyBlockId = toId;
        else
            from.Block.NextBlockId = toId;

        Connections.Add(new ConnectionVm { FromBlockId = fromId, ToBlockId = toId, PortType = portType });
        IsModified = true;
    }

    public void RemoveConnection(string fromBlockId, string portType)
    {
        var from = Blocks.FirstOrDefault(b => b.Block.BlockId == fromBlockId);
        if (from == null) return;

        if (portType == "Body" && from.Block is IHasBodyOutput bbo)
            bbo.BodyBlockId = null;
        else
            from.Block.NextBlockId = null;

        Connections.RemoveWhere(c => c.FromBlockId == fromBlockId && c.PortType == portType);
        IsModified = true;
    }

    [RelayCommand]
    private async Task RunSequence()
    {
        if (IsRunning) return;

        var error = ValidateSequence();
        if (error != null)
        {
            LogText = $"[WALIDACJA] {error}\n";
            return;
        }

        LogText = string.Empty;
        var def = BuildDefinition();
        var instruments = _mainVm.GetInstrumentsDictionary();
        await _engine.RunAsync(def, instruments, _dataManager);
    }

    private string? ValidateSequence()
    {
        if (!Blocks.Any())
            return "Sekwencja jest pusta. Dodaj bloki z panelu BLOKI.";

        if (!Blocks.Any(b => b.BlockType == "StartBlock"))
            return "Brak bloku Start. Dodaj blok 'Start' do sekwencji.";

        return null;
    }

    [RelayCommand]
    private void PauseSequence()
    {
        if (IsPaused) _engine.Resume(); else _engine.Pause();
    }

    [RelayCommand]
    private void StopSequence() => _engine.Stop();

    [RelayCommand]
    private void NewSequence()
    {
        Blocks.Clear(); Connections.Clear();
        SequenceName = "Nowa sekwencja";
        CurrentFilePath = string.Empty;
        LogText = string.Empty;
        IsModified = false;

        var startBlock = BlockRegistry.Create("StartBlock");
        if (startBlock != null) AddBlock(startBlock, 80, 200);
    }

    [RelayCommand]
    private void SaveSequence()
    {
        if (string.IsNullOrEmpty(CurrentFilePath)) { SaveSequenceAs(); return; }
        var def = BuildDefinition();
        File.WriteAllText(CurrentFilePath, def.ToJson());
        IsModified = false;
    }

    [RelayCommand]
    private void SaveSequenceAs()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Sekwencje InstrumentControl (*.iseq)|*.iseq|Wszystkie pliki (*.*)|*.*",
            DefaultExt = ".iseq",
            FileName = SequenceName
        };
        if (dlg.ShowDialog() == true)
        {
            CurrentFilePath = dlg.FileName;
            SequenceName = Path.GetFileNameWithoutExtension(dlg.FileName);
            SaveSequence();
        }
    }

    [RelayCommand]
    private void OpenSequence()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Sekwencje InstrumentControl (*.iseq)|*.iseq|Wszystkie pliki (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        var json = File.ReadAllText(dlg.FileName);
        var def = SequenceDefinition.FromJson(json);
        if (def == null) { MessageBox.Show("Nie można wczytać pliku sekwencji."); return; }

        LoadDefinition(def);
        CurrentFilePath = dlg.FileName;
        SequenceName = def.Name;
        IsModified = false;
    }

    public SequenceDefinition BuildDefinition()
    {
        var def = new SequenceDefinition { Name = SequenceName };
        foreach (var vm in Blocks)
            def.Blocks.Add(vm.Block.Serialize());
        return def;
    }

    private void LoadDefinition(SequenceDefinition def)
    {
        Blocks.Clear(); Connections.Clear();
        foreach (var bd in def.Blocks)
        {
            var block = BlockRegistry.Create(bd.BlockType);
            if (block == null) continue;
            block.Deserialize(bd);
            Blocks.Add(new SequenceBlockVm(block, bd.X, bd.Y));

            if (bd.NextBlockId != null)
                Connections.Add(new ConnectionVm
                    { FromBlockId = bd.BlockId, ToBlockId = bd.NextBlockId, PortType = "Next" });

            if (block is IHasBodyOutput bbo && bbo.BodyBlockId != null)
                Connections.Add(new ConnectionVm
                    { FromBlockId = bd.BlockId, ToBlockId = bbo.BodyBlockId, PortType = "Body" });
        }
    }

    private SequenceDefinition BuildDefinitionFromUI()
    {
        foreach (var vm in Blocks)
        {
            vm.Block.X = vm.X;
            vm.Block.Y = vm.Y;
        }
        return BuildDefinition();
    }
}
