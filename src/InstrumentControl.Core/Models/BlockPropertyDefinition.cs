namespace InstrumentControl.Core.Models;

public enum PropertyEditorType
{
    TextBox,
    NumberBox,
    ComboBox,
    CheckBox,
    FilePath,
    InstrumentSelector,
    VariableName
}

public class BlockPropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PropertyEditorType EditorType { get; set; } = PropertyEditorType.TextBox;
    public object? DefaultValue { get; set; }
    public List<string> Options { get; set; } = new();
    public bool IsRequired { get; set; } = false;
    public string? GroupName { get; set; }

    public static BlockPropertyDefinition Text(string name, string display, string? defaultVal = null, bool required = false) =>
        new() { Name = name, DisplayName = display, EditorType = PropertyEditorType.TextBox, DefaultValue = defaultVal, IsRequired = required };

    public static BlockPropertyDefinition Number(string name, string display, double defaultVal = 0) =>
        new() { Name = name, DisplayName = display, EditorType = PropertyEditorType.NumberBox, DefaultValue = defaultVal };

    public static BlockPropertyDefinition Combo(string name, string display, List<string> options, string? defaultVal = null) =>
        new() { Name = name, DisplayName = display, EditorType = PropertyEditorType.ComboBox, Options = options, DefaultValue = defaultVal ?? options.FirstOrDefault() };

    public static BlockPropertyDefinition Check(string name, string display, bool defaultVal = false) =>
        new() { Name = name, DisplayName = display, EditorType = PropertyEditorType.CheckBox, DefaultValue = defaultVal };

    public static BlockPropertyDefinition FilePath(string name, string display) =>
        new() { Name = name, DisplayName = display, EditorType = PropertyEditorType.FilePath };

    public static BlockPropertyDefinition Instrument(string name = "InstrumentName") =>
        new() { Name = name, DisplayName = "Instrument", EditorType = PropertyEditorType.InstrumentSelector, IsRequired = true };

    public static BlockPropertyDefinition Variable(string name, string display, string? defaultVal = null) =>
        new() { Name = name, DisplayName = display, EditorType = PropertyEditorType.VariableName, DefaultValue = defaultVal };
}
