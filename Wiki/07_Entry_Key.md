# Entry: KeyEntry

`KeyEntry` is a runtime keybind control.

It lets user rebind a key directly from MOD SETTINGS without editing config files manually.

## Constructor

```csharp
new KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set)
new KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set, string prefKey)
```

## Parameters

- `label` - row title.
- `get` - returns currently bound key.
- `set` - applies new key after user input.
- `prefKey` (optional) - enables persistence across restarts.

## Basic example

```csharp
private static KeyCode _toggleKey = KeyCode.F6;

new KeyEntry(
    "Toggle Key",
    () => _toggleKey,
    v => _toggleKey = v,
    "ToggleKey"
);
```

## Runtime behavior

- Click **Change** -> entry enters listening mode.
- Next key press (`Input.GetKeyDown`) becomes new binding.
- `Esc` cancels listening without changing key.
- **Clear** sets key to `KeyCode.None` (unbound state).
- If another key entry is already waiting, new one will not enter listening mode until previous is finished.
- While slider/search text input is active, key listening waits (no capture).

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

UI detail: unbound key is displayed as `— none —`.

## Interaction with `POGConfig.PanelOpen`

Avoid gameplay hotkey handling while panel is open:

```csharp
if (!POGConfig.PanelOpen && _toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
{
    ToggleFeature();
}
```

This prevents accidental feature toggles while user is editing settings.

## Optional mouse buttons

`KeyEntry` supports `AllowMouseButtons` toggle (opt-in pattern in docs/source):

```csharp
new KeyEntry("Attack Key", () => _attackKey, v => _attackKey = v)
{
    AllowMouseButtons = true
};
```

Use this only when mouse-button binding is truly needed.

## Keys that are intentionally not captured

During listen mode:

- `KeyCode.None` is ignored
- `KeyCode.Escape` is reserved for canceling listen mode
- mouse buttons are ignored unless `AllowMouseButtons = true`

## Persistence loading detail

With `prefKey`, keybind is stored as string (`KeyCode.ToString()`), then parsed back with `Enum.TryParse<KeyCode>` on load.

If stored value is invalid, runtime key stays at your current default.

## Common mistakes

- Handling bound key every frame without `PanelOpen` guard.
- Forgetting `KeyCode.None` checks.
- Binding keys in two systems at once (duplicate sources of truth).

## Recommended pattern

- Keep one field for keybind value.
- Route all runtime hotkey checks through that field.
- If persisted, give stable `prefKey` that never changes across releases.
