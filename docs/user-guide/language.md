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
| `Strings.en.xaml` | English | 134 |
| `Strings.pl.xaml` | Polish | 134 |

Each file is a standard WPF `ResourceDictionary` containing `<sys:String>` entries:

```xml
<!-- Strings.en.xaml (excerpt) -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <sys:String x:Key="AddInstrument">Add Instrument</sys:String>
    <sys:String x:Key="Run">Run</sys:String>
    <sys:String x:Key="Pause">Pause</sys:String>
    ...
</ResourceDictionary>
```

All XAML views bind to string resources with `{DynamicResource Key}`:

```xml
<Button Content="{DynamicResource AddInstrument}" />
```

Using `DynamicResource` (instead of `StaticResource`) is what enables the live switch — WPF re-evaluates all dynamic bindings when the resource dictionary is swapped.

### LocalizationService

`src/InstrumentControl.App/Services/LocalizationService.cs` is a singleton that:

- Loads the saved language from `settings.json` on startup (`App.xaml.cs` calls `LocalizationService.Initialize()`)
- Swaps `App.Current.Resources.MergedDictionaries` to the correct `Strings.xx.xaml` when `SetLanguage(code)` is called
- Persists the new choice back to `settings.json`

### Adding a Third Language

1. Copy `Strings.en.xaml` to `Strings.xx.xaml` (where `xx` is the BCP 47 language tag, e.g. `de` for German)
2. Translate all 134 string values
3. In `LocalizationService.SetLanguage()`, add `"xx"` to the list of supported codes and map it to the new file path
4. Add the language option to the `AboutWindow` XAML combo-box items
