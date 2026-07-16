# Entry: KeyEntry

`KeyEntry` is a runtime keybind control with **up to 3 universal slots** per action.

It lets the user rebind directly from MOD SETTINGS without editing config files manually.
Every slot is universal: it accepts **either a keyboard key or a mouse button**
(`KeyCode.Mouse0`–`Mouse6`) — there is no fixed keyboard/mouse split. By default an
entry has a single slot; a mod can opt into more.

## Constructors

```csharp
// Single-slot (default) — unchanged since 1.0.x, fully source/binary compatible
new KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set)
new KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set, string prefKey)

// Multi-slot — up to 3 universal slots
new KeyEntry(string label,
             Func<int, KeyCode> get,        // get(slotIndex)
             Action<int, KeyCode> set,      // set(slotIndex, value)
             string prefKey,
             int slotCount)                 // clamped to 1..3
```

## Parameters

- `label` - row title.
- `get` / `set` - single-slot: read/write the one bound key. Multi-slot: read/write by
  `slotIndex` (0-based).
- `prefKey` (optional) - enables persistence across restarts.
- `slotCount` (multi-slot) - number of slots, 1 to 3.

## Single-slot example

```csharp
private static KeyCode _toggleKey = KeyCode.F6;

new KeyEntry(
    "Toggle Key",
    () => _toggleKey,
    v => _toggleKey = v,
    "ToggleKey"
);
```

## Multi-slot example

```csharp
private static readonly KeyCode[] _fireKeys = { KeyCode.Mouse0, KeyCode.None, KeyCode.None };

new KeyEntry(
    "Fire",
    slot => _fireKeys[slot],
    (slot, v) => _fireKeys[slot] = v,
    "FireKey",   // slot 0 → "FireKey", slot 1 → "FireKey_2", slot 2 → "FireKey_3"
    3
);

// Runtime: react to any bound slot
private static bool FirePressed()
{
    foreach (var k in _fireKeys)
        if (k != KeyCode.None && Input.GetKeyDown(k)) return true;
    return false;
}
```

## Runtime behavior

- Click a **slot** -> it enters listening mode (`…`).
- Next key or mouse-button press (`Input.GetKeyDown`) becomes that slot's binding.
- `Esc` cancels listening without changing the slot.
- The inline **x** in a slot's corner sets it to `KeyCode.None` (unbound).
- If another slot (any entry, including the Vanilla tab) is already waiting, a new slot
  will not enter listening mode until the previous one finishes.
- While slider/search text input is active, key listening waits (no capture).
- Slots are laid out right-aligned in a fixed 3-column grid; a single-slot entry sits in
  the rightmost column.

## Unbound flow (`KeyCode.None`)

Treat `KeyCode.None` as intentionally disabled hotkey:

```csharp
if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
{
    ToggleFeature();
}
```

You can also make this the default startup state:

```csharp
private static KeyCode _toggleKey = KeyCode.None; // default: no key selected
```

In this mode, the feature has no hotkey until user sets one in MOD SETTINGS.

## `Clear` button semantics

`Clear` is not the same as cancel:

- **Cancel (`Esc`)** -> keep previous key
- **Clear** -> explicitly set `KeyCode.None` and persist that state (if `prefKey` is used)

UI detail: an unbound slot is displayed as `—`, and its inline **x** (clear) button is
hidden while the slot is empty.

## Interaction with `POGConfig.PanelOpen`

Avoid gameplay hotkey handling while panel is open:

```csharp
if (!POGConfig.PanelOpen && _toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
{
    ToggleFeature();
}
```

This prevents accidental feature toggles while user is editing settings.

## Mouse buttons

Slots accept mouse buttons **by default** (`AllowMouseButtons = true` on every
constructor). A pressed mouse button is stored as `KeyCode.Mouse0`–`Mouse6`. To make an
entry keyboard-only, opt out explicitly:

```csharp
new KeyEntry("Menu Key", () => _menuKey, v => _menuKey = v)
{
    AllowMouseButtons = false  // keyboard only
};
```

> Changed in 1.0.9: mouse buttons used to be opt-in (`AllowMouseButtons` defaulted to
> `false`). Slots are now universal, so it defaults to `true`.

## Keys that are intentionally not captured

During listen mode:

- `KeyCode.None` is ignored
- `KeyCode.Escape` is reserved for canceling listen mode
- mouse buttons are ignored only if you set `AllowMouseButtons = false`

## Persistence loading detail

With `prefKey`, each slot is stored as a string (`KeyCode.ToString()`) and parsed back
with `Enum.TryParse<KeyCode>` on load. Slot 0 uses `prefKey` as-is (same format as the
old single-slot entries), slots 1 and 2 use `prefKey_2` and `prefKey_3`.

If a stored value is invalid, that slot stays at your current default.

## Restore Defaults

Every mod page has a **RESTORE DEFAULTS** button at the bottom. It resets all of that
mod's entries — including every `KeyEntry` slot — to the values the mod was registered
with (captured before saved preferences were applied), and persists the reset. You don't
need to wire anything; it works for all entry types automatically.

## Vanilla Binds tab

The sidebar has a **VANILLA BINDS** tab that mirrors the game's own Controls screen. It is
read live from Rewired and is **editable**: click a slot and press a key (keyboard columns)
or mouse button (mouse column) to rebind the vanilla action; changes are written into the
game's real input system and verified. This is separate from your mod's `KeyEntry` slots —
you don't register anything for it.

## Common mistakes

- Handling bound key every frame without `PanelOpen` guard.
- Forgetting `KeyCode.None` checks.
- Binding keys in two systems at once (duplicate sources of truth).

## Recommended pattern

- Keep one field for keybind value.
- Route all runtime hotkey checks through that field.
- If persisted, give stable `prefKey` that never changes across releases.
