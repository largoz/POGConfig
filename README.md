# POGConfig

In-game configuration UI framework for **Pit of Goblin** mods (MelonLoader, IL2CPP).

`POGConfig` gives mod authors a shared MOD SETTINGS panel with ready-to-use controls:
- `HeaderEntry` (section heading)
- `ToggleEntry`
- `SliderEntry` (editable numeric box + optional suffix formatting)
- `OptionsSliderEntry` (discrete mode selector)
- `KeyEntry` (runtime key rebinding, up to 3 keyboard/mouse slots)
- `ButtonEntry` (a button that runs an action)

It also has a VANILLA BINDS tab that edits the game's own controls, and a RESTORE DEFAULTS
button on every page.

**Repo:** [github.com/largoz/POGConfig](https://github.com/largoz/POGConfig) · **Wiki:** [`Wiki/`](Wiki/) ([on GitHub](https://github.com/largoz/POGConfig/tree/main/Wiki))

**Thunderstore:** [package page](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/) · [Thunderstore Wiki](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/wiki)

---

## Installation (for players)

1. Install MelonLoader for Pit of Goblin.
2. Put `POGConfig.dll` into the game's `Mods` folder.
3. Install mods that register entries through `POGConfig.Register(...)`.

---

## Dev helper

`build_and_deploy.py` builds this project (`dotnet build -c Release`) and copies the DLL
into the game's `Mods` folder. It finds the game as the first parent folder holding
`Pit of Goblin.exe`, or takes it from the `POG_GAME_DIR` environment variable:

```sh
POG_GAME_DIR="/path/to/Pit of Goblin" python build_and_deploy.py
```

Building by hand works the same way: `dotnet build -c Release -p:GameDir="/path/to/Pit of Goblin"`.
The csproj reads MelonLoader and the game's interop assemblies from `$(GameDir)/MelonLoader`.

---

## Quick Start (for developers)

Add a reference to `POGConfig.dll`, then register your entries in `OnInitializeMelon()`:

```csharp
POGConfig.Register("My Mod", new List<ConfigEntry>
{
    new HeaderEntry("General"),
    new ToggleEntry("Enable Feature", () => enabled, v => enabled = v, "Enabled"),
    new SliderEntry("Speed", () => speed, v => speed = v, 0f, 10f, x => $"{x:F1}", "Speed"),
    new KeyEntry("Hotkey", () => hotkey, v => hotkey = v, "Hotkey"),
    new ButtonEntry("Statistics", "RESET", () => stats.Clear()),
});

POGConfig.PanelClosed += ApplyExpensiveSettings; // optional
```

Calling `Register` again with the same name replaces the earlier page.

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
  - One slot by default, up to 3 with `KeyEntry(label, get(slot), set(slot, key), prefKey, slotCount)`.
- **ButtonEntry**
  - Runs an action when pressed. No value, not saved.
- **HeaderEntry**
  - A section heading between groups of entries.

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
Saving happens half a second after the last change, when the panel closes and when the game quits.

### 5) Integration Notes

- UI is opened from MOD SETTINGS panel.
- Hotkey-based features should usually be disabled while the panel is open:
  `if (!POGConfig.PanelOpen) { ... }`
- `POGConfig.PanelOpened` / `POGConfig.PanelClosed` fire after the panel opens / closes.
  A value still being typed has been applied by the time `PanelClosed` fires.
- Keep heavy work out of per-frame callbacks; use entries to update cached runtime values.

</details>
