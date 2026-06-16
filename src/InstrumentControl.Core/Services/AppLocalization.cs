namespace InstrumentControl.Core.Services;

/// <summary>
/// Process-wide localization event bus. App raises RaiseLanguageChanged();
/// plugin assemblies subscribe to LanguageChanged to refresh their strings.
/// </summary>
public static class AppLocalization
{
    public static event EventHandler? LanguageChanged;
    public static void RaiseLanguageChanged() => LanguageChanged?.Invoke(null, EventArgs.Empty);
}
