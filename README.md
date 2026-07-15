# POGConfig

In-game configuration UI framework for **Pit of Goblin** mods (MelonLoader, IL2CPP).

`POGConfig` gives mod authors a shared MOD SETTINGS panel with ready-to-use controls:
- `ToggleEntry`
- `SliderEntry` (editable numeric box + optional suffix formatting)
- `OptionsSliderEntry` (discrete mode selector)
- `KeyEntry` (runtime key rebinding)

**Repo:** [github.com/LargoScript/POGConfig](https://github.com/LargoScript/POGConfig) · **Wiki:** [`Wiki/`](Wiki/) ([on GitHub](https://github.com/LargoScript/POGConfig/tree/main/Wiki))

---

## Installation (for players)

1. Install MelonLoader for Pit of Goblin.
2. Put `POGConfig.dll` into the game's `Mods` folder.
3. Install mods that register entries through `POGConfig.Register(...)`.

---

## Dev helper

`build_and_deploy.py` — builds this project (`dotnet build -c Release`) and
copies the resulting DLL straight into `../../Mods/`. Convenience only, no
required arguments:

```powershell
python build_and_deploy.py
```

---

## Quick Start (for developers)

Add a reference to `POGConfig.dll`, then register your entries in `OnInitializeMelon()`:

```csharp
POGConfig.Register("My Mod", new List<ConfigEntry>
{
    new ToggleEntry("Enable Feature", () => enabled, v => enabled = v),
    new SliderEntry("Speed", () => speed, v => speed = v, 0f, 10f, x => $"{x:F1}"),
    new KeyEntry("Hotkey", () => hotkey, v => hotkey = v),
});
```

---

## For Developers
<details>
<summary><strong>Click To Expand</strong></summary>

### 1) Entry Types

- **ToggleEntry**
  - Boolean value with instant ON/OFF update.
- **SliderEntry**
  - Float/int ranges.
  - Numeric text input is editable.
  - Supports formatting suffixes (example: `43s`).
  - Supports optional origin-based fill and optional discrete points.
- **OptionsSliderEntry**
  - Fixed list of string options (`Easy`, `Normal`, `Hard`).
  - Slider snaps to indices.
- **KeyEntry**
  - Capture and persist key bindings from the UI.

### 2) Range / Origin / Steps (from POGTestMod patterns)

```csharp
new SliderEntry(
    "Temperature Offset (origin=0)",
    () => temperature,
    v => temperature = v,
    -50f, 50f, v => $"{v:F1}",
    "TemperatureOffset", 0f, true, false);
```

- `originValue = 0f` makes fill render from center/outward.

```csharp
new SliderEntry(
    "Point Selector (whole numbers)",
    () => points,
    v => points = v,
    -2f, 6f, v => $"{v:F0}",
    "PointSelector", 0f, false, true, 9);
```

- `wholeNumbers = true` makes step snapping.
- `stepPointsCount = 9` draws subtle point markers on the track.

```csharp
new SliderEntry(
    "Duration With Suffix",
    () => durationSeconds,
    v => durationSeconds = v,
    5f, 180f, v => $"{v:F0}s",
    "DurationSeconds", 5f, true, true);
```

- On focus, the input strips suffix and shows numeric text only.
- On Enter, value is applied and formatted back (`43s`).

### 3) Options Example

```csharp
new OptionsSliderEntry(
    "Difficulty",
    () => difficulty,
    v => difficulty = v,
    new[] { "Easy", "Normal", "Hard" },
    "Difficulty");
```

### 4) Preferences / Persistence

Each entry can optionally receive `prefKey`. Values are saved via `MelonPreferences` and restored on next launch.

### 5) Integration Notes

- UI is opened from MOD SETTINGS panel.
- Hotkey-based features should usually be disabled while the panel is open:
  `if (!POGConfig.PanelOpen) { ... }`
- Keep heavy work out of per-frame callbacks; use entries to update cached runtime values.

</details>
