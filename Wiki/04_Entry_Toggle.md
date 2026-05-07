# Entry: ToggleEntry

`ToggleEntry` is a boolean on/off control for feature flags.

Use it when a setting has two states only: enabled/disabled.

## Constructor

```csharp
new ToggleEntry(string label, Func<bool> get, Action<bool> set)
new ToggleEntry(string label, Func<bool> get, Action<bool> set, string prefKey)
```

## Parameters

- `label` - row title shown in MOD SETTINGS.
- `get` - returns current boolean value.
- `set` - applies new boolean value from UI interaction.
- `prefKey` (optional) - enables automatic persistence through `MelonPreferences`.

## Minimal example

```csharp
private static bool _godMode;

new ToggleEntry(
    "God Mode",
    () => _godMode,
    v => _godMode = v
);
```

## Persisted example

```csharp
private static bool _showDamageNumbers = true;

new ToggleEntry(
    "Show Damage Numbers",
    () => _showDamageNumbers,
    v => _showDamageNumbers = v,
    "ShowDamageNumbers"
);
```

## Callback behavior

- `set` is called immediately when user clicks the toggle.
- During runtime, `get()` is polled to keep UI synchronized if value changes externally.
- Internal callback suppression prevents recursive/double `set` calls when UI is being synchronized from external state.

This means you can safely change `_godMode` from other code, and UI should still reflect it.

## Persistence initialization detail

When `prefKey` is provided, `ToggleEntry` does this on bind:

- creates a preference entry with current value as default
- immediately applies loaded preference value to runtime via `set(pref.Value)`
- wraps future `set` calls to auto-save with `MelonPreferences.Save()`

## Good usage patterns

- **Feature switches**:
  - enable/disable system
  - show/hide overlay
  - allow/disallow automation
- **Safety toggles**:
  - debug mode
  - verbose logging
  - development cheats

## Anti-patterns

- Using `ToggleEntry` for multi-state data ("Low/Medium/High"): use `OptionsSliderEntry` instead.
- Running heavy logic directly in `set`: call lightweight state update and let main loop handle expensive work.

## Practical integration tip

Prefer wrapping side effects:

```csharp
private static bool _enabled;

private static void SetEnabled(bool value)
{
    _enabled = value;
    ApplyRuntimeState();
}

new ToggleEntry("Enable", () => _enabled, SetEnabled, "Enable");
```

This keeps UI code and runtime behavior cleanly separated.
