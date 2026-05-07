# Public API and Types

This page documents `POGConfig` public-facing API and key data types.

## Namespace

```csharp
using POGMods.Config;
```

## Main API class: `POGConfig`

### `Register` overloads

```csharp
public static void Register(string modName, List<ConfigEntry> entries)
public static void Register(string modName, List<ConfigEntry> entries, ModInfo info)
```

## `modName` matters

`modName` is used for:

- section name shown in UI sidebar
- persistence category name (`modName.Replace(" ", "_")`)

So avoid changing it after release unless you intentionally want a new prefs category.

## `PanelOpen`

```csharp
public static bool PanelOpen { get; }
```

- `true` while MOD SETTINGS panel is open
- use it to suppress your gameplay hotkeys during UI interaction

## Base type: `ConfigEntry`

```csharp
public abstract class ConfigEntry
```

All entry classes derive from it.
You usually instantiate concrete entries only.

## Concrete entries

- `HeaderEntry`
- `ToggleEntry`
- `SliderEntry`
- `OptionsSliderEntry`
- `KeyEntry`

## `HeaderEntry` (section label row)

Use it as a visual separator/header in long config groups.

```csharp
new HeaderEntry("Movement")
```

It has:

- no value
- no callbacks
- no persistence

## `ModInfo` metadata

```csharp
public class ModInfo
{
    public string Version;
    public string Description;
    public string Author;
}
```

Example:

```csharp
POGConfig.Register(
    "My Mod",
    entries,
    new ModInfo("1.2.0", "Advanced movement tuning", "You")
);
```

This metadata is shown in content header area.

## Recommended registration structure

```csharp
POGConfig.Register("My Mod", new List<ConfigEntry>
{
    new HeaderEntry("General"),
    new ToggleEntry("Enabled", () => _enabled, v => _enabled = v, "Enabled"),
    new HeaderEntry("Input"),
    new KeyEntry("Toggle Key", () => _toggleKey, v => _toggleKey = v, "ToggleKey")
}, new ModInfo("1.0.0", "Config for runtime controls", "You"));
```

## Notes for stability

- Keep `modName` stable across releases.
- Keep `prefKey` stable for persisted entries.
- Add new entries freely; old ones remain compatible if keys are unchanged.
