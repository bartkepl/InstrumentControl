using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstrumentControl.App.Controls;
using InstrumentControl.App.Services;
using InstrumentControl.App.Views;
using InstrumentControl.Core.Enums;
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

    public string DisplayName =>
        System.Windows.Application.Current?.TryFindResource($"Block_{Block.BlockType}") as string
        ?? Block.DisplayName;
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
        new(StringComparer.OrdinalIgnoreCase) { "Control", "General", "Data" };

    private readonly SequenceEngine _engine;
    private readonly DataManager _dataManager;
    private MainWindowViewModel _mainVm;
    private readonly ILogService _logService;
    private readonly Stack<SequenceDefinition> _undoStack = new();

    [ObservableProperty] private ObservableCollection<SequenceBlockVm> _blocks = new();
    [ObservableProperty] private ObservableCollection<ConnectionVm> _connections = new();
    [ObservableProperty] private SequenceBlockVm? _selectedBlock;
    [ObservableProperty] private string _sequenceName = "";
    [ObservableProperty] private string _currentFilePath = string.Empty;
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private string _sequenceStatus = "";

    public string SequenceStatusDisplay =>
        $"{LocalizationService.Get("StatusBar_SequenceLabel")} {SequenceStatus}";
    // Embedded console in SequenceEditorView — shows only Sequence-source entries
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

    public SequenceEditorViewModel(SequenceEngine engine, DataManager dataManager, MainWindowViewModel mainVm, ILogService logService)
    {
        _engine = engine;
        _dataManager = dataManager;
        _mainVm = mainVm;
        _logService = logService;

        _engine.BlockExecuting += (_, id) => RunOnUi(() =>
        {
            ExecutingBlockId = id;
            foreach (var b in Blocks) b.IsExecuting = b.Block.BlockId == id;
        });
        _engine.BlockCompleted += (_, _) => RunOnUi(() =>
        {
            foreach (var b in Blocks) b.IsExecuting = false;
        });
        _engine.LogMessage += (_, msg) => _logService.Log(LogSource.Sequence, msg);
        _engine.StateChanged += (_, state) => RunOnUi(() =>
        {
            IsRunning = state == SequenceState.Running || state == SequenceState.Paused;
            IsPaused = state == SequenceState.Paused;
            SequenceStatus = state.ToString();
            OnPropertyChanged(nameof(SequenceStatusDisplay));
        });
        _engine.Error += (_, ex) =>
        {
            _logService.Log(LogSource.Sequence, $"{LocalizationService.Get("VM_ErrorLogPrefix")} {ex.Message}");
            RunOnUi(() => ErrorWindow.ShowException("Sequence error", ex));
        };

        // Mirror Sequence-source entries into the embedded console (SequenceEditorView panel)
        logService.EntryAdded += (_, entry) =>
        {
            if (entry.Source == LogSource.Sequence)
                RunOnUi(() => LogText += entry.Formatted + "\n");
        };

        SequenceName = LocalizationService.Get("VM_NewSequenceName");
        SequenceStatus = LocalizationService.Get("VM_SeqStatus_Ready");

        LocalizationService.LanguageChanged += (_, _) => RunOnUi(() =>
        {
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(SequenceStatusDisplay));
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
            if (b.Block is IHasConditionOutputs cond)
            {
                if (cond.TrueBlockId == vm.Block.BlockId) cond.TrueBlockId = null;
                if (cond.FalseBlockId == vm.Block.BlockId) cond.FalseBlockId = null;
            }
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
        else if (portType == "True" && from.Block is IHasConditionOutputs trueBlock)
            trueBlock.TrueBlockId = toId;
        else if (portType == "False" && from.Block is IHasConditionOutputs falseBlock)
            falseBlock.FalseBlockId = toId;
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
        else if (portType == "True" && from.Block is IHasConditionOutputs trueBlock)
            trueBlock.TrueBlockId = null;
        else if (portType == "False" && from.Block is IHasConditionOutputs falseBlock)
            falseBlock.FalseBlockId = null;
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
            _logService.Log(LogSource.Sequence, $"[WALIDACJA] {error}");
            return;
        }

        var def = BuildDefinition();
        var instruments = _mainVm.GetInstrumentsDictionary();
        await Task.Run(() => _engine.RunAsync(def, instruments, _dataManager));
    }

    private string? ValidateSequence()
    {
        if (!Blocks.Any())
            return LocalizationService.Get("VM_SequenceEmpty");

        if (!Blocks.Any(b => b.BlockType == "StartBlock"))
            return LocalizationService.Get("VM_SequenceNoStart");

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
        SequenceName = LocalizationService.Get("VM_NewSequenceName");
        CurrentFilePath = string.Empty;
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
            Filter = LocalizationService.Get("VM_FilterSeq"),
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
            Filter = LocalizationService.Get("VM_FilterSeq")
        };
        if (dlg.ShowDialog() != true) return;

        var json = File.ReadAllText(dlg.FileName);
        var def = SequenceDefinition.FromJson(json);
        if (def == null) { MessageBox.Show(LocalizationService.Get("VM_SeqLoadError")); return; }

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

            // ConditionBlock uses True/False ports — skip creating "Next" for it
            if (bd.NextBlockId != null && block is not IHasConditionOutputs)
                Connections.Add(new ConnectionVm
                    { FromBlockId = bd.BlockId, ToBlockId = bd.NextBlockId, PortType = "Next" });

            if (block is IHasBodyOutput bbo && bbo.BodyBlockId != null)
                Connections.Add(new ConnectionVm
                    { FromBlockId = bd.BlockId, ToBlockId = bbo.BodyBlockId, PortType = "Body" });

            if (block is IHasConditionOutputs cond)
            {
                if (cond.TrueBlockId != null)
                    Connections.Add(new ConnectionVm
                        { FromBlockId = bd.BlockId, ToBlockId = cond.TrueBlockId, PortType = "True" });
                if (cond.FalseBlockId != null)
                    Connections.Add(new ConnectionVm
                        { FromBlockId = bd.BlockId, ToBlockId = cond.FalseBlockId, PortType = "False" });
            }
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

