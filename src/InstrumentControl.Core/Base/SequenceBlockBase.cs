using System.Text.Json;
using System.Windows.Media;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Base;

public abstract class SequenceBlockBase : ISequenceBlock
{
    public string BlockId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public abstract string BlockType { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract Color BlockColor { get; }
    public virtual string Category => "Ogólne";

    public double X { get; set; } = 50;
    public double Y { get; set; } = 50;
    public string? NextBlockId { get; set; }

    public Dictionary<string, object?> Properties { get; set; } = new();
    public abstract IEnumerable<BlockPropertyDefinition> PropertyDefinitions { get; }

    public abstract Task<BlockExecutionResult> ExecuteAsync(SequenceContext context);

    protected T GetProp<T>(string key, T defaultValue = default!)
    {
        if (!Properties.TryGetValue(key, out var val)) return defaultValue;
        if (val is T typed) return typed;
        if (val is JsonElement je)
        {
            try { return je.Deserialize<T>() ?? defaultValue; } catch { }
        }
        try { return (T)Convert.ChangeType(val, typeof(T))!; }
        catch { return defaultValue; }
    }

    protected string GetPropStr(string key, string defaultValue = "") =>
        GetProp<string>(key, defaultValue) ?? defaultValue;

    public virtual BlockData Serialize() => new()
    {
        BlockId = BlockId,
        BlockType = BlockType,
        X = X,
        Y = Y,
        NextBlockId = NextBlockId,
        Properties = Properties.ToDictionary(
            kv => kv.Key,
            kv => JsonSerializer.SerializeToElement(kv.Value))
    };

    public virtual void Deserialize(BlockData data)
    {
        BlockId = data.BlockId;
        X = data.X;
        Y = data.Y;
        NextBlockId = data.NextBlockId;
        foreach (var kv in data.Properties)
            Properties[kv.Key] = kv.Value;
    }

    public virtual ISequenceBlock Clone()
    {
        var clone = (SequenceBlockBase)MemberwiseClone();
        clone.BlockId = Guid.NewGuid().ToString("N")[..8];
        clone.Properties = new Dictionary<string, object?>(Properties);
        return clone;
    }
}
