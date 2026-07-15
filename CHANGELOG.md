# Changelog

## 1.0.9

- The pause-menu MODS button is now a real `NetworkMenuButton` injected at the top of the pause button column — native yellow selector, hover, click sound and gamepad navigation, and the column layout spaces it automatically (same approach as the main menu since 1.0.7). The old floating overlay button remains only as a fallback if the injection target is not found.
- Fixed: pressing Esc to close the MOD SETTINGS panel in the pause menu no longer also closes the pause menu itself with the same key press.
- Toggle checkboxes are now square (22×22) instead of rectangular, and the yellow checkmark is drawn as a centred square so it no longer looks unevenly inset at fractional UI scales.
- Groundwork for upcoming releases: a more native settings window (game-aware state so Esc/back behaves like vanilla screens), a reworked keybind system with up to three universal keyboard/mouse slots per action, a read-only "Vanilla" tab mirroring the game's own binds, and bind-conflict detection with a quick-fix popup.

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
