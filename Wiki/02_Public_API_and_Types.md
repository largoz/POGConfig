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

Calling `Register` again with a name that is already registered replaces that page
(entries and `ModInfo`) instead of adding a second one, and reuses the existing preference
keys. Defaults for RESTORE DEFAULTS are captured again from the values at that call.

## `PanelOpen`

```csharp
public static bool PanelOpen { get; }
```

- `true` while MOD SETTINGS panel is open
- use it to suppress your gameplay hotkeys during UI interaction

## `PanelOpened` / `PanelClosed` (since 1.0.12)

```csharp
public static event Action PanelOpened;
public static event Action PanelClosed;
```

- raised after the panel opens / closes, on the main thread
- by the time `PanelClosed` fires, a value still being typed has been applied and pending
  preference changes have been written
- an exception in your handler is logged and does not stop other handlers

```csharp
POGConfig.PanelClosed += RebuildOverlay; // apply expensive settings once, not per change
```

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
- `ButtonEntry` (since 1.0.12)

## `HeaderEntry` (section label row)

Use it as a visual separator/header in long config groups.

```csharp
new HeaderEntry("Movement")
```

It has:

- no value
- no callbacks
- no persistence

## `ButtonEntry` (action row, since 1.0.12)

```csharp
new ButtonEntry(string label, string buttonText, Action onClick)
```

```csharp
new ButtonEntry("Statistics", "RESET", () => _stats.Clear())
```

- runs `onClick` when the button is pressed
- no value, no persistence; RESTORE DEFAULTS skips it
- an exception thrown by `onClick` is logged as a warning, not rethrown

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

## Restore Defaults (automatic)

Every mod page shows a **RESTORE DEFAULTS** button. When the panel first registers your
entries, POGConfig captures each entry's current value as its "default" (before any saved
preference is loaded), and the button re-applies and persists those values. This is
built in for all entry types — you do not implement or register anything.

Implication: the value your fields hold at `Register` time **is** the default. Set your
fields to their intended defaults before registering.

## Vanilla Binds tab

A **VANILLA BINDS** entry sits at the top of the sidebar. It is a live, editable mirror of
the game's own Controls screen (read from / written to Rewired directly). It is provided by
POGConfig itself — mods do not register it and cannot add rows to it.

## Notes for stability

- Keep `modName` stable across releases.
- Keep `prefKey` stable for persisted entries.
- Add new entries freely; old ones remain compatible if keys are unchanged.
- `KeyEntry` gained a multi-slot constructor in 1.0.9; the old single-slot constructors
  are unchanged and remain source/binary compatible. See `07_Entry_Key.md`.
