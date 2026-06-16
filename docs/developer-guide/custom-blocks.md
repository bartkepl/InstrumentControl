# Custom Blocks

This page covers advanced topics when writing sequence blocks: multi-output blocks, property editor types, variable substitution, and cross-block communication.

---

## Block Property Editor Types

`BlockPropertyDefinition` controls how a property is displayed in the **Properties Panel** when the user double-clicks a block.

```csharp
// Factory methods on BlockPropertyDefinition:
BlockPropertyDefinition.Text(name, displayName, description, defaultValue)
BlockPropertyDefinition.Number(name, displayName, description, defaultValue)
BlockPropertyDefinition.Combo(name, displayName, description, options[], defaultValue)
BlockPropertyDefinition.Check(name, displayName, description, defaultValue)
BlockPropertyDefinition.FilePath(name, displayName, description)
BlockPropertyDefinition.Instrument(name, displayName, description)
BlockPropertyDefinition.Variable(name, displayName, description, defaultValue)
```

| Type | Control rendered | Stored as |
|---|---|---|
| `Text` | TextBox | `string` |
| `Number` | NumberBox (numeric-only TextBox) | `string` (parseable as `double`) |
| `Combo` | ComboBox | `string` (one of the options) |
| `Check` | CheckBox | `"true"` / `"false"` |
| `FilePath` | TextBox + Browse button | `string` (absolute path) |
| `Instrument` | ComboBox populated with connected instrument names | `string` (DriverName) |
| `Variable` | TextBox with auto-complete from known variables | `string` (variable name) |

### Reading Properties in ExecuteAsync

```csharp
// Read as string (always works)
string name = GetPropStr("VariableName");

// Parse to typed value
double range  = GetProp<double>("Range");       // throws if parse fails
bool   enable = GetProp<bool>("Enabled");
int    count  = GetProp<int>("Iterations");

// With fallback for missing/invalid values
double delay = GetProp<double>("Delay", defaultValue: 1000.0);
```

### Grouping Properties

Use `GroupName` to visually group related properties in the panel:

```csharp
new BlockPropertyDefinition
{
    Name        = "Voltage",
    DisplayName = "Voltage (V)",
    EditorType  = EditorType.Number,
    GroupName   = "Output Settings",
    DefaultValue = "0",
}
```

---

## Multi-Output Blocks

Blocks with two execution paths (ConditionBlock, LoopBlock) implement `IHasBodyOutput` and expose two block IDs:

```csharp
public class MyBranchBlock : SequenceBlockBase, IHasBodyOutput
{
    // Primary output (from ISequenceBlock.NextBlockId)
    // Used as the "False" / "After loop" output
    public override string? NextBlockId { get; set; }

    // Secondary output (from IHasBodyOutput)
    // Used as the "True" / "Body" output
    public string? BodyBlockId { get; set; }

    public override async Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        bool condition = EvaluateCondition(context);

        // Return BodyBlockId for the "true" path, NextBlockId for "false"
        string? next = condition ? BodyBlockId : NextBlockId;
        return BlockExecutionResult.Ok(next);
    }
}
```

`BlockCanvas` detects `IHasBodyOutput` and draws a second output dot with a different colour. `SequenceEditorViewModel` sets both `NextBlockId` and `BodyBlockId` when the user drags connections.

---

## Variable Substitution in Properties

Some blocks support `{varname}` placeholders in text properties. Implement this by calling `context.GetVariable<T>()` at runtime:

```csharp
// Simple variable lookup:
string raw = GetPropStr("Temperature");    // could be "85" or "{targetTemp}"
double temp = raw.StartsWith("{") && raw.EndsWith("}")
    ? context.GetVariableAsDouble(raw.Trim('{', '}'))
    : double.Parse(raw, CultureInfo.InvariantCulture);
```

`SequenceBlockBase` provides a convenience helper for this pattern:

```csharp
double temp = ResolveDouble("Temperature", context);
```

For message strings with multiple substitutions (like LogMessageBlock):

```csharp
string msg = GetPropStr("Message");   // "Voltage = {v} V, Temp = {t} °C"
foreach (var (name, value) in context.Variables)
    msg = msg.Replace("{" + name + "}", value?.ToString() ?? "");
```

---

## Emitting Measurements

Every block that produces a numeric reading should call `context.OnMeasurement()`. This fires `DataManager.ResultAdded`, which updates the Data Viewer chart and table in real time.

```csharp
context.OnMeasurement(new MeasurementResult
{
    Timestamp      = DateTime.UtcNow,
    InstrumentName = "MyInstrument",
    ChannelId      = "CH1",         // empty string for single-channel
    ParameterName  = "DCV",
    Value          = 3.141592,
    Unit           = "V",
    Function       = "DCV",
    IsValid        = true,
    Metadata       = new Dictionary<string, string> { ["range"] = "10" }
});
```

`ChannelId` is used by multi-channel instruments (e.g. Agilent 34970A) to separate data series per channel. Leave it empty for single-channel instruments.

---

## Block Serialization

`SequenceBlockBase` handles serialization automatically via `Serialize()` / `Deserialize()`. Both methods read and write `Properties` as a JSON object inside `BlockData`.

If your block has state that is not stored in `Properties` (e.g. computed caches), override `Serialize()` and `Deserialize()`:

```csharp
public override BlockData Serialize()
{
    var data = base.Serialize();
    // Add extra fields to data.Properties if needed
    data.Properties["_cache"] = JsonSerializer.SerializeToElement(MyCache);
    return data;
}

public override void Deserialize(BlockData data)
{
    base.Deserialize(data);
    if (data.Properties.TryGetValue("_cache", out var el))
        MyCache = JsonSerializer.Deserialize<MyType>(el);
}
```

---

## Clone Pattern

`Clone()` must return a new instance with independent `Properties` but with a **new BlockId**. `SequenceBlockBase.Clone()` does this automatically:

```csharp
public override ISequenceBlock Clone()
{
    var clone = (SequenceBlockBase)MemberwiseClone();
    clone._blockId = GenerateId();  // new 8-char ID
    clone.Properties = new Dictionary<string, object?>(Properties);
    clone.NextBlockId = null;       // connections are not cloned
    return clone;
}
```

If your block has reference-type properties that need deep-copying, override `Clone()`:

```csharp
public override ISequenceBlock Clone()
{
    var clone = (MyBlock)base.Clone();
    clone.MyList = new List<string>(MyList);
    return clone;
}
```

---

## Unit Testing Blocks

Blocks are easy to unit-test because `ExecuteAsync` only depends on `SequenceContext`:

```csharp
[Fact]
public async Task MeasureDCV_StoresResultInVariable()
{
    // Arrange
    var driver = new FakeDriver { FakeVoltage = 5.0 };
    var context = new SequenceContext(
        instruments: new() { ["TestDMM"] = driver },
        variables: new(),
        allBlocks: new(),
        results: new(),
        ct: CancellationToken.None);

    var block = new YourInstrument_MeasureDCV();
    block.Properties["Instrument"]   = "TestDMM";
    block.Properties["Range"]        = "10";
    block.Properties["VariableName"] = "voltage";

    // Act
    var result = await block.ExecuteAsync(context);

    // Assert
    Assert.True(result.Success);
    Assert.Equal(5.0, context.GetVariable<double>("voltage"));
}
```

Use a `FakeDriver` that returns fixed values for `MeasureDCVAsync()`. The `IConnectionProvider` abstraction means you never need a real instrument in tests.
