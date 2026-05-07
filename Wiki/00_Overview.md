# POGConfig Overview

`POGConfig` is an in-game settings UI framework for Pit of Goblin mods.

It provides one shared "MOD SETTINGS" panel where mods can register settings entries.
Instead of building your own menu UI, you define entries and callbacks, and `POGConfig` handles rendering and interaction.

## What it gives you

- A shared settings panel for multiple mods.
- Standardized setting entry types:
  - toggle
  - slider
  - options slider
  - keybind
- Built-in runtime update flow for entry values.
- Optional persistence through `MelonPreferences` via `prefKey`.

## Core concept

Your mod calls:

```csharp
POGConfig.Register("My Mod Name", entries);
```

Where `entries` is a `List<ConfigEntry>` (or derived entry types).

Each entry provides:

- `get` callback: returns current value
- `set` callback: applies new value when user changes UI
- optional `prefKey`: enables automatic load/save

## Entry model (high level)

- `ConfigEntry` is the abstract base type.
- Concrete entry types inherit from it:
  - `ToggleEntry`
  - `SliderEntry`
  - `OptionsSliderEntry`
  - `KeyEntry`

These are classes, not just visual labels.  
Each class contains behavior for rendering, value updates, and user interaction.

## Typical workflow

1. Add reference to `POGConfig.dll` in your mod project.
2. Prepare local fields for setting values.
3. Build and register entries in `OnInitializeMelon()`.
4. React to values through your callbacks.
5. Optionally persist values with `prefKey`.

## Scope and intent

`POGConfig` is designed for developer productivity:

- less duplicate UI code across mods
- consistent user experience
- easier iteration when adding new mod settings

For implementation details and copy-paste setup, continue with `01_QuickStart.md`.
