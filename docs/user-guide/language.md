# Language / UI Localization

InstrumentControl supports two interface languages: **English** and **Polish** (Polski). You can switch between them at any time without restarting the application.

---

## Switching the Language

1. Click the **About** button in the toolbar (the ℹ icon, top-right area)
2. In the About dialog, find the **Language** drop-down
3. Select **English** or **Polski**
4. The entire UI updates instantly — all menus, buttons, labels, and status messages switch to the chosen language

The selection is saved automatically and restored on the next launch.

---

## Persisted Setting

The language preference is stored in a JSON file:

```
%LocalAppData%\InstrumentControl\settings.json
```

Example content:

```json
{ "language": "pl" }
```

Valid values are `"en"` (English) and `"pl"` (Polish). If the file is missing or contains an unknown value, the application defaults to English.

---

## Default Language

English is the default. On a fresh installation (before any language selection) the app starts in English.

---

## Developer Notes

### Resource Files

UI strings are stored in XAML resource dictionaries under `src/InstrumentControl.App/Resources/`:

| File | Language | Entries |
|---|---|---|
| `Strings.en.xaml` | English | 282 |
| `Strings.pl.xaml` | Polish | 282 |

Each file is a standard WPF `ResourceDictionary` containing `<sys:String>` entries:

```xml
<!-- Strings.en.xaml (excerpt) -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <sys:String x:Key="Toolbar_AddInstrument">➕ Add Instrument</sys:String>
    <sys:String x:Key="Toolbar_Run">▶ Run</sys:String>
    <sys:String x:Key="DriverDesc_HP34401A">6½-digit digital multimeter</sys:String>
    ...
</ResourceDictionary>
```

All XAML views bind to string resources with `{DynamicResource Key}`:

```xml
<Button Content="{DynamicResource Toolbar_AddInstrument}" />
```

Using `DynamicResource` (instead of `StaticResource`) is what enables the live switch — WPF re-evaluates all dynamic bindings when the resource dictionary is swapped. C# code that needs a localized string reads it through `LocalizationService.Get("Key")` (or `TryGet`, which returns `null` for missing keys).

### Intentionally English-only text

Some strings are kept in English in **both** language files because they are technical:

- **Log tab headers** (`LogTab_All`, `LogTab_Sequence`, `LogTab_VISA`, `LogTab_Serial`, `LogTab_Events`, `LogTab_Instruments`, `LogTab_Debug`) and the log content itself — instrument communication logs are read in English.

### Localized instrument descriptions

The driver descriptions shown in the **Add Instrument** dialog come from `DriverDesc_<DriverName>` keys (e.g. `DriverDesc_HP34401A`), one per installed driver, so they switch language with the rest of the UI. If a key is missing, the dialog falls back to the driver's built-in `Description` property.

### LocalizationService

`src/InstrumentControl.App/Services/LocalizationService.cs` is a singleton that:

- Loads the saved language from `settings.json` on startup (`App.xaml.cs` calls `LocalizationService.Initialize()`)
- Swaps `App.Current.Resources.MergedDictionaries` to the correct `Strings.xx.xaml` when `SetLanguage(code)` is called
- Persists the new choice back to `settings.json`

### Adding a Third Language

1. Copy `Strings.en.xaml` to `Strings.xx.xaml` (where `xx` is the BCP 47 language tag, e.g. `de` for German)
2. Translate all 282 string values (leave the `LogTab_*` keys in English)
3. In `LocalizationService.SetLanguage()`, add `"xx"` to the list of supported codes and map it to the new file path
4. Add the language option to the `AboutWindow` XAML combo-box items
