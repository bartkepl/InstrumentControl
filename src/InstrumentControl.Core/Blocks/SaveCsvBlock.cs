using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class SaveCsvBlock : SequenceBlockBase
{
    public override string BlockType => "SaveCsvBlock";
    public override string DisplayName => "Zapisz CSV";
    public override string Description => "Zapisuje wyniki pomiarów do pliku CSV";
    public override Color BlockColor => Color.FromRgb(0x29, 0x80, 0xB9);
    public override string Category => "Dane";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.FilePath("FilePath", "Ścieżka pliku CSV"),
        BlockPropertyDefinition.Check("AppendMode", "Dopisuj do istniejącego", true),
        BlockPropertyDefinition.Check("SaveAll", "Zapisz wszystkie wyniki (nie tylko nowe)", false),
    ];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string path = GetPropStr("FilePath", "pomiary.csv");
        bool append = GetProp<bool>("AppendMode", true);
        bool saveAll = GetProp<bool>("SaveAll", false);

        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(BlockExecutionResult.Fail("Ścieżka pliku CSV jest pusta"));

        try
        {
            var dm = new DataManager();
            var toSave = saveAll ? context.Results : GetNewResults(context);
            dm.ExportToCsv(path, toSave, append);
            context.Log?.Invoke($"Zapisano {toSave.Count()} wyników do: {path}");
            return Task.FromResult(BlockExecutionResult.Ok(NextBlockId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BlockExecutionResult.Fail($"Błąd zapisu CSV: {ex.Message}"));
        }
    }

    private IEnumerable<MeasurementResult> GetNewResults(SequenceContext context)
    {
        string key = $"__csv_idx_{BlockId}";
        int lastIdx = context.GetVariable<int>(key);
        var newResults = context.Results.Skip(lastIdx).ToList();
        context.SetVariable(key, context.Results.Count);
        return newResults;
    }

    static SaveCsvBlock() => BlockRegistry.Register("SaveCsvBlock", () => new SaveCsvBlock());
}
