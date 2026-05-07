# POGConfig Architecture

This page explains how `POGConfig` is structured internally and how to use that design safely from your own mod.

## 1) Main building blocks

Core types in `ConfigPlugin.cs`:

- `POGConfig` (static API surface)
  - registration entrypoint
  - shared registry
  - panel open state (`PanelOpen`)
- `ConfigEntry` (abstract base class)
  - common interface for all row types
- Entry implementations:
  - `ToggleEntry`
  - `SliderEntry`
  - `OptionsSliderEntry`
  - `KeyEntry`
- `ConfigBehaviour` (`MonoBehaviour`)
  - runtime controller for panel lifecycle and updates

## 2) Runtime flow

High-level flow:

1. Your mod starts (`OnInitializeMelon`).
2. Your mod calls `POGConfig.Register(...)`.
3. `POGConfig` stores entries in shared registry.
4. UI is built/updated by `ConfigBehaviour` when panel is opened.
5. While panel is open, entries update via callbacks (`get`/`set`).
6. If `prefKey` is set, values are persisted through `MelonPreferences`.

## 3) Why this pattern works

`POGConfig` separates concerns cleanly:

- your mod owns game logic values
- `POGConfig` owns UI rendering and interaction
- entries act as adapters between both

That means you can evolve gameplay logic without rewriting UI widgets every time.

## 4) Registration patterns (recommended)

### A) Safe optional dependency pattern

If you want your mod to stay loadable even when `POGConfig` is absent, use a no-inlining wrapper:

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MelonLoader;
using POGMods.Config;

public class MyMelon : MelonMod
{
    private static bool _enabled = true;
    private static float _speed = 5f;

    public override void OnInitializeMelon()
    {
        try { TryRegisterConfig(); } catch { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TryRegisterConfig()
    {
        POGConfig.Register("My Mod", new List<ConfigEntry>
        {
            new ToggleEntry("Enable", () => _enabled, v => _enabled = v, "Enable"),
            new SliderEntry("Speed", () => _speed, v => _speed = v, 0f, 10f, v => $"{v:F1}", "Speed")
        });
    }
}
```

### B) Direct required dependency pattern

If your mod must have `POGConfig`, register directly in `OnInitializeMelon()` and fail fast when unavailable.

## 5) `PanelOpen` usage pattern

Use `POGConfig.PanelOpen` to avoid hotkey conflicts while the config UI is focused:

```csharp
void Update()
{
    if (!POGConfig.PanelOpen && Input.GetKeyDown(_toggleKey))
        ToggleFeature();
}
```

Practical effect: editing config won't accidentally trigger gameplay actions.

## 6) Entry behavior model

All entries follow the same conceptual contract:

- **Read path (`get`)**: UI pulls current value
- **Write path (`set`)**: UI pushes user changes
- **Persistence (`prefKey`)**: optional, managed by framework

This makes entries deterministic and easy to debug.

## 7) Advanced `SliderEntry` usage examples

### A) Value suffix (`43s`) + typed input

```csharp
new SliderEntry(
    "Duration",
    () => _durationSec,
    v => _durationSec = v,
    5f, 180f,
    v => $"{v:F0}s",
    "DurationSec",
    5f, true, true
);
```

### B) Bidirectional fill from origin

```csharp
new SliderEntry(
    "Temperature Offset",
    () => _tempOffset,
    v => _tempOffset = v,
    -50f, 50f,
    v => $"{v:F1}",
    "TempOffset",
    originValue: 0f
);
```

### C) Step-like integer slider

```csharp
new SliderEntry(
    "Points",
    () => _points,
    v => _points = (int)v,
    0f, 8f,
    v => $"{v:F0}",
    "Points",
    originValue: 0f,
    showFill: false,
    wholeNumbers: true,
    stepPointsCount: 9
);
```

## 8) Common integration mistakes

- Registering before your backing fields are initialized.
- Doing heavy game logic inside `set` callbacks.
- Ignoring `PanelOpen` and causing hotkey overlap.
- Using same `prefKey` for unrelated values.

## 9) Suggested application patterns

- **Gameplay toggles**: `ToggleEntry` + bool field.
- **Tunable numeric systems**: `SliderEntry` + float/int field.
- **Mode selectors**: `OptionsSliderEntry` with fixed names.
- **Runtime controls**: `KeyEntry` for user remapping.

If you keep these mapped cleanly, your config layer stays maintainable as the mod grows.
