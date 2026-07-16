# Changelog

## 1.0.9

**Native config window.** The MOD SETTINGS panel is now a real injected
`NetworkMenuState` that the game switches to via `NetworkMenu.SetState`, the same
mechanism used for its own Settings/Credits/etc. screens. The game hides the
underlying menu on its own and treats the panel as the current screen instead of
an overlay drawn on top of it, so Esc/cancel behaves like a native screen
transition rather than two separate systems racing each other. Falls back
automatically to the previous plain-overlay behavior if the injected state can't
be registered.

**Keybind rework.** `KeyEntry` supports up to **3 universal slots** per action —
any slot accepts a keyboard key or a mouse button (`KeyCode.Mouse0`–`Mouse6`),
no fixed keyboard/mouse split. One slot by default; a mod opts into more via the
new constructor `KeyEntry(label, get(slot), set(slot, value), prefKey, slotCount)`.
Slots sit right-aligned in a fixed 3-column grid, each with an inline clear
button. The old single-key constructors still work exactly as before (source-
and binary-compatible with 1.0.x mods). Slot 1 persists under the same `prefKey`
as before, slots 2/3 under `prefKey_2`/`prefKey_3`. Also fixed: starting a
rebind by clicking a slot could immediately capture that same mouse click as
the bound button.

**Vanilla Binds tab.** A new "VANILLA BINDS" entry at the top of the sidebar
shows the game's own controls — every user-assignable action with its keyboard
(up to 2) and mouse (1) binds, read live from Rewired each time the tab opens.
Binds are **editable**: click a slot, press a key (keyboard columns) or a mouse
button (mouse column), and the change is written straight into the vanilla
input system via the same Rewired API the game's Controls screen uses, then
saved and **verified by re-reading** — the row always ends up showing the real
state, and a warning is logged if an assignment did not stick. Keyboard/mouse
column typing matches how the game itself stores binds, so nothing can be
assigned here that the vanilla screen couldn't display.

**Restore Defaults.** Every mod page now has a RESTORE DEFAULTS button at the
bottom that resets all of that mod's settings to the values the mod registered
with (captured before saved preferences were applied), and persists the reset.

**Pause menu & UI fixes.**
- The pause-menu MODS button is now a real `NetworkMenuButton` injected at the top of the pause button column — native yellow selector, hover, click sound and gamepad navigation, and the column layout spaces it automatically (same approach as the main menu since 1.0.7). The old floating overlay button remains only as a fallback if the injection target is not found.
- Fixed: pressing Esc to close the MOD SETTINGS panel in the pause menu no longer also closes the pause menu itself with the same key press.
- Toggle checkboxes are now square (22×22) instead of rectangular, and the yellow checkmark is drawn as a centred square so it no longer looks unevenly inset at fractional UI scales.

## 1.0.8

- Fixed: hovering the MODS button in the main menu now shows the selector and yellow highlight exactly like the native buttons. The cloned button is now instantiated directly under the Buttons Panel so it resolves its internal `NetworkMenu` reference; the old manual hover workaround was removed.
- Fixed: pressing Esc to cancel a search edit, a slider value edit, or a key-binding capture no longer also closes the whole panel with the same key press.
- Added: Esc now cancels key-binding capture in `KeyEntry` (previously only the Cancel button worked).
- Fixed: the sidebar mod list snaps back to the top when it is rebuilt (e.g. while typing a search query after scrolling down).

## 1.0.7

- The MODS button in the main menu is now a real `NetworkMenuButton` injected into the in-game Buttons Panel (between START and SETTINGS) instead of a floating overlay. It matches the game's button style, sound, and selection visuals.
- The pause-menu MODS button keeps the floating-overlay approach for now.
- Vertical scrollbar fixed: the thumb is now draggable on both the mod list (sidebar) and the settings list (content), and clicking on the empty track centres the thumb on the cursor and starts a drag from that point. Mouse wheel now scrolls only the section the cursor is over.
- Switching between mods in the sidebar resets the content scroll position back to the top.
- Mouse and keyboard input on the underlying main/pause menu is suppressed while the config panel is open (Enter no longer triggers main-menu buttons by accident).
- Pressing Esc now closes the config panel.

## 1.0.6

- Added a full developer Wiki for `POGConfig` with entry-by-entry docs, architecture notes, runtime behavior details, persistence guidance, and troubleshooting pages.
- Included `CHANGELOG.md` in the package build to ensure release notes are visible on Thunderstore.

## 1.0.3

- `SliderEntry` now exposes `LabelFraction` and `ValueFraction` properties so mod authors can override the default column widths (35 % / 15 % / 50 %) via object initializer syntax.
- `KeyEntry` now shows a **Clear** button to remove the keybind (`KeyCode.None`). Unbound keys display as `— none —`.

## 1.0.1

- Added `OptionsSliderEntry` for discrete string option lists.
- Added `KeyEntry.AllowMouseButtons` opt-in property to allow mouse button binding.
- Added `POGConfig.PanelOpen` public property; other mods can suppress hotkeys while the panel is open.
- Added vertical scrollbar; scroll wheel supported.
- Added marquee text animation for long labels and value strings (hover to scroll).

## 1.0.0

- Initial release: in-game settings UI framework for Pit of Goblin mods.
- `ToggleEntry`, `SliderEntry`, `KeyEntry`.
- MelonPreferences persistence via `prefKey`.
