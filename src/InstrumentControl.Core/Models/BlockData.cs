using System.Text.Json;

namespace InstrumentControl.Core.Models;

public class BlockData
{
    public string BlockId { get; set; } = string.Empty;
    public string BlockType { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string? NextBlockId { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = new();

    public T? GetProperty<T>(string key, T? defaultValue = default)
    {
        if (!Properties.TryGetValue(key, out var el)) return defaultValue;
        try { return el.Deserialize<T>(); }
        catch { return defaultValue; }
    }
}

public class SequenceDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Nowa sekwencja";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public List<BlockData> Blocks { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static SequenceDefinition? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<SequenceDefinition>(json); }
        catch { return null; }
    }
}
