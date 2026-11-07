# POGConfig Overview

`POGConfig` is an in-game settings UI framework for Pit of Goblin mods.

It provides one shared "MOD SETTINGS" panel where mods can register settings entries.
Instead of building your own menu UI, you define entries and callbacks, and `POGConfig` handles rendering and interaction.

## What it gives you

- A shared settings panel for multiple mods, opened from a **MODS** button injected
  natively into both the main menu and the in-game pause menu.
- Standardized setting entry types:
  - section header
  - toggle
  - slider
  - options slider
  - keybind (up to 3 universal keyboard/mouse slots)
  - button (runs an action)
- `PanelOpened` / `PanelClosed` events for settings that are expensive to apply.
- Built-in runtime update flow for entry values.
- Optional persistence through `MelonPreferences` via `prefKey`.
- A **RESTORE DEFAULTS** button on every mod page (resets that mod to its registered
  defaults — no wiring needed).
- A **VANILLA BINDS** tab: a live, editable mirror of the game's own Controls screen,
  backed directly by Rewired.

## Native window (since 1.0.9)

The panel is a real injected `NetworkMenuState`, so the game treats it as a first-class
screen: it hides the underlying menu itself, and Esc/Back behave like a native screen
transition. It falls back to a plain overlay if the injected state cannot be registered.

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
  - `HeaderEntry`
  - `ToggleEntry`
  - `SliderEntry`
  - `OptionsSliderEntry`
  - `KeyEntry`
  - `ButtonEntry`

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
