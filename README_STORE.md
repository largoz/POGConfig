# POG Config

One shared **MOD SETTINGS** panel for Pit of Goblin mods, plus an editor for the game's own key binds.

Mods that support POG Config add their settings to this panel, so you get one place to change everything instead of editing config files.

**Install:** Thunderstore Mod Manager / r2modman, or put `POGConfig.dll` into the game's `Mods` folder (MelonLoader required).

**Source and developer Wiki:** [github.com/largoz/POGConfig](https://github.com/largoz/POGConfig) ([Wiki](https://github.com/largoz/POGConfig/tree/main/Wiki)) · [Thunderstore Wiki](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/wiki)

---

## For players

- A **MODS** button in the main menu and in the pause menu, styled like the game's own buttons, with mouse, keyboard and gamepad navigation.
- A sidebar with every mod that registered settings, with a search box.
- **VANILLA BINDS** tab: the game's own controls, editable. Each action has two keyboard slots and one mouse slot, exactly like the game's Controls screen. Changes go straight into the game's settings.
- **RESTORE DEFAULTS** on every page, including the vanilla binds.
- Settings are saved automatically.
- `Esc` cancels what you are doing (typing a value, waiting for a key, an open list) and otherwise closes the panel.
- Settings with many choices open a **list** with search and filter buttons. Type to search; arrows, Enter and Esc work too.

---

## For mod authors

<details>
<summary><strong>Click to expand</strong></summary>

### Reference

```xml
<Reference Include="POGConfig">
  <HintPath>..\..\Mods\POGConfig.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Add `using POGMods.Config;` and declare a dependency on `Largo-POG_Config` in your Thunderstore manifest.

### Registering

Register once, usually from `OnInitializeMelon()`. Load order does not matter: `Register` only stores your entries, and the panel is built the first time it opens.

Wrapping the call in a `NoInlining` helper keeps POG Config optional: your mod still loads if POG Config is not installed.

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using POGMods.Config;

public override void OnInitializeMelon()
{
    try { TryRegisterConfig(); } catch { }
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static void TryRegisterConfig()
{
    POGConfig.Register("My Mod", new List<ConfigEntry>
    {
        new HeaderEntry("General"),
        new ToggleEntry("Enable Feature", () => _enabled, v => _enabled = v, "Enabled"),
        new SliderEntry("Speed", () => _speed, v => _speed = v, 0f, 10f, v => $"{v:F1}", "Speed"),
        new HeaderEntry("Input"),
        new KeyEntry("Hotkey", () => _key, v => _key = v, "Hotkey"),
    }, new ModInfo("1.0.0", "Short description", "You"));
}
```

- The mod name is the sidebar label and the `MelonPreferences` category (spaces become `_`). Keep it stable across releases.
- The values your fields hold when you call `Register` are the defaults that RESTORE DEFAULTS goes back to. Set them first.
- Calling `Register` again with the same name replaces your earlier page instead of adding a second one.

### Entry types

| Type | What it shows |
|---|---|
| `HeaderEntry(label)` | A section heading. No value. |
| `ToggleEntry(label, get, set[, prefKey])` | A checkbox. |
| `SliderEntry(label, get, set, min, max[, fmt, prefKey, originValue, showFill, wholeNumbers, stepPointsCount])` | A slider with a value box you can type into. |
| `OptionsSliderEntry(label, get, set, options[, prefKey])` | A slider that picks one of several named options. |
| `ChoiceRow(label, get, set, options…)` | A choice from a list that opens over the page, with search and filters. New in 1.1.0. |
| `KeyEntry(label, get, set[, prefKey])` | One key bind slot. |
| `KeyEntry(label, get(slot), set(slot, key), prefKey, slotCount)` | Up to 3 key bind slots. |
| `ButtonEntry(label, buttonText, onClick)` | A button that runs an action. New in 1.0.12. |

`get` is polled while the panel is open, so the row follows changes your mod makes on its own. `set` is called when the player changes the value.

#### `SliderEntry`

```csharp
new SliderEntry(
    string label, Func<float> get, Action<float> set,
    float min, float max,
    Func<float, string> fmt = null,   // display text; default v => v.ToString("F1")
    string prefKey          = null,
    float  originValue      = 0f,     // the fill bar grows from here, in both directions
    bool   showFill         = true,
    bool   wholeNumbers     = false,  // snap to integers
    int    stepPointsCount  = 0)      // 2 or more draws evenly spaced dots on the track
```

- Click the value box to type a number. `Enter` or a click elsewhere applies it, `Esc` cancels. A suffix from your formatter (`43s`, `80%`) is ignored when parsing. Values are clamped to `[min, max]`.
- Column widths default to 35 % label, 15 % value box, the rest slider. Override with `{ LabelFraction = 0.45f, ValueFraction = 0.10f }`.

```csharp
new SliderEntry("Volume", () => vol, v => vol = v, 0f, 1f, v => $"{v * 100:F0}%", "Volume")

new SliderEntry("Points", () => pts, v => pts = v, 0f, 8f, v => $"{v:F0}",
    "Points", 0f, false, true, 9)   // whole numbers, 9 dots, no fill
```

#### `OptionsSliderEntry`

```csharp
new OptionsSliderEntry("Difficulty", () => _difficulty, v => _difficulty = v,
    new[] { "Easy", "Normal", "Hard" }, "Difficulty")
```

The value is the option index. Saved as an `int`.

#### `KeyEntry`

```csharp
// One slot
new KeyEntry("Toggle Key", () => _toggleKey, v => _toggleKey = v, "ToggleKey")

// Up to three slots
private static readonly KeyCode[] _fire = { KeyCode.Mouse0, KeyCode.None, KeyCode.None };
new KeyEntry("Fire", slot => _fire[slot], (slot, v) => _fire[slot] = v, "Fire", 3)
```

- Click a slot, then press a key or a mouse button. `Esc` cancels. The small **x** unbinds the slot (`KeyCode.None`).
- Every slot accepts keyboard keys and mouse buttons. Set `{ AllowMouseButtons = false }` for a keyboard-only entry.
- Slot 1 is saved under `prefKey`, slots 2 and 3 under `prefKey_2` and `prefKey_3`.
- Treat `KeyCode.None` as "no hotkey": `if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))`.

#### `ChoiceRow`

A setting picked from a list. The row shows the current item; a click opens the list over the page.

```csharp
new ChoiceRow("Difficulty", () => _diff, v => _diff = v, "Easy", "Normal", "Hard")
    .Saved("Difficulty")

new ChoiceRow("Font", () => _font, v => _font = v, new List<Option>
{
    new Option("goblin", "Goblin Menu", "Cyrillic letters in the menu style", "Font mods"),
    new Option("roboto", "Roboto", "The game's plain font", "Game fonts"),
})
    .Searchable()                        // search field (shown by itself from 10 items on)
    .Filters("Font mods", "Game fonts")  // filter buttons, matched against Option tags
    .Description("Font for the main menu")
```

- The value is the item's **key** (`string`), so adding, removing or reordering items never turns a saved choice into another one.
- For a list that changes while the game runs, pass `() => CurrentOptions()` instead of a fixed list.
- `.Saved(key)` keeps the choice in `MelonPreferences.cfg`; `.Description(text)` adds a line under the label that wraps if it is long.
- Only the visible items are drawn, so long lists stay fast.

#### `ButtonEntry`

```csharp
new ButtonEntry("Statistics", "RESET", () => _stats.Clear())
```

Runs `onClick` when pressed. It has no value, is not saved, and RESTORE DEFAULTS leaves it alone. An exception thrown by `onClick` is logged, not rethrown.

### Panel state and events

```csharp
public static bool PanelOpen { get; }
public static event Action PanelOpened;
public static event Action PanelClosed;
```

Check `PanelOpen` before handling your hotkeys, so keys pressed inside the panel don't trigger game actions:

```csharp
if (!POGConfig.PanelOpen && Input.GetKeyDown(_toggleKey)) Toggle();
```

`PanelClosed` fires after any value still being typed has been applied. Use it for settings that are expensive to apply one change at a time:

```csharp
POGConfig.PanelClosed += RebuildOverlay;
```

### Saving

Entries with a `prefKey` load their saved value during `Register` and save on change.

| Entry | Saved as |
|---|---|
| `ToggleEntry` | `bool` |
| `SliderEntry` | `float` |
| `OptionsSliderEntry` | `int` (option index) |
| `KeyEntry` | `string` (key name) |

Changes are written to `UserData/MelonPreferences.cfg` half a second after the last change, when the panel closes, and when the game quits. Dragging a slider no longer writes the file every frame.

Only changes made through the panel are saved. If your mod changes a value itself (for example through a hotkey), the panel shows the new value but your mod is responsible for saving it.

### Custom entry types

`ConfigEntry`'s build and update hooks are internal, so other assemblies cannot add new entry types. If you need one, open an issue on GitHub.

</details>
