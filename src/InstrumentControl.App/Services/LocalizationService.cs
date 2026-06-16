using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using InstrumentControl.Core.Services;

namespace InstrumentControl.App.Services;

public static class LocalizationService
{
    public const string EnglishCode = "en";
    public const string PolishCode  = "pl";

    private static readonly string[] SupportedLanguages = { EnglishCode, PolishCode };

    public static string CurrentLanguage { get; private set; } = EnglishCode;

    public static event EventHandler? LanguageChanged;

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InstrumentControl", "settings.json");

    public static void Initialize()
    {
        var saved = LoadSavedLanguage();
        Apply(saved ?? EnglishCode);
    }

    public static void SetLanguage(string languageCode)
    {
        if (languageCode == CurrentLanguage) return;
        Apply(languageCode);
        SaveLanguage(languageCode);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
        AppLocalization.RaiseLanguageChanged();
    }

    public static string Get(string key) =>
        Application.Current?.Resources[key] as string ?? key;

    private static void Apply(string languageCode)
    {
        CurrentLanguage = languageCode;
        var uri = new Uri(
            $"pack://application:,,,/Resources/Strings.{languageCode}.xaml",
            UriKind.Absolute);
        var newDict = new ResourceDictionary { Source = uri };

        var appDicts = Application.Current.Resources.MergedDictionaries;
        var existing = appDicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Resources/Strings.") == true);
        if (existing != null) appDicts.Remove(existing);
        appDicts.Add(newDict);
    }

    private static string? LoadSavedLanguage()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return null;
            var json = File.ReadAllText(SettingsFilePath);
            var match = Regex.Match(json, "\"language\"\\s*:\\s*\"(\\w+)\"");
            if (match.Success && SupportedLanguages.Contains(match.Groups[1].Value))
                return match.Groups[1].Value;
        }
        catch { }
        return null;
    }

    private static void SaveLanguage(string languageCode)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            File.WriteAllText(SettingsFilePath, $"{{\"language\":\"{languageCode}\"}}");
        }
        catch { }
    }
}
