# Changelog

## 1.0.13

**New**

- **The MODS button speaks the game's language.** The button in the main and pause menus now
  follows the language picked in the game's settings: МОДИ in Ukrainian, МОДЫ in Russian,
  模组 in Simplified Chinese, MOD in Italian and Japanese, MODDAR in Swedish, and GOBBO HACKS
  in Goblin. English, French, German and Spanish keep MODS. A language the game adds later
  shows the English label until it gets its own. The label changes in the same frame as the
  game's own buttons.

## 1.0.12

**Fixes**

- **Esc cancels before it closes.** Since 1.0.11 the game's own cancel input closed the
  whole panel on every Esc, even when that Esc was meant to cancel a key listen, a value
  being typed or the search box. Esc now cancels that first; a second Esc closes the panel.
- **Hidden rows no longer react to clicks.** On a page long enough to scroll, rows scrolled
  out of view were only hidden: clicking the mod title or the header area above the list
  could start editing a hidden slider value, start listening on a hidden key slot, or clear
  a hidden bind. A row now only counts as clicked when the click is inside the visible list.
  The sidebar mod list had the same problem under the panel title and is fixed the same way.
- **Closing the panel ends any edit in progress.** Closing with DONE, or the game leaving
  the menu, while a slider value was being typed or the search box was active left that
  edit open: the panel reopened still in edit mode with the typed text neither applied nor
  discarded. Closing now applies the typed value, cancels a key listen and leaves search
  edit mode. Switching pages does the same.
- **Registering the same mod name twice no longer breaks saving.** The second call added a
  duplicate page, and MelonLoader refused to create the already existing preference keys,
  so that page's changes were never saved. A repeat `Register` now replaces the earlier
  page and reuses the existing keys.
- **Fewer disk writes.** Settings were written to `MelonPreferences.cfg` on every slider tick
  while dragging. Changes are now saved half a second after the last change, when the panel
  closes, and when the game quits.
- Slider handles stay on the track at the minimum and maximum. At the maximum the handle
  used to hang half off the end, where the list clipped it and it looked squashed.
- Step-point dots render at the same size at every resolution and sit exactly where the
  handle stops for each step.
- Key capture only scans keys on frames where something was pressed, instead of polling
  every key code each frame while a slot is listening.

**New for mod authors**

- `ButtonEntry(label, buttonText, onClick)`: a row with a button that runs an action, for
  things like "Reset stats" or "Reload".
- `POGConfig.PanelOpened` and `POGConfig.PanelClosed` events. `PanelClosed` fires after any
  value still being typed has been applied, so it is the place to apply settings that are
  expensive to change one at a time.

Fully compatible with mods built against 1.0.x: the public API only gained members.

## 1.0.11

**Fixed: the MOD SETTINGS panel would not open after the Pit of Goblin v1.0 update.**
The update removed `NetworkMenuState.m_overrideCancelInput`; setting it threw a
`MissingMethodException` on every MODS-button click, so the panel never appeared. The panel
no longer touches that field. Cancel/Esc while the panel is the active native state is now
handled by overriding `PogConfigState.OnCancelButton` to drive our own close, so the panel
behaves like a native menu without depending on internal fields that older game versions had.
Thanks to GlitchApotamus for the `OnCancelButton` approach.

## 1.0.10

**Fixed: the MODS button disappeared after changing the game language.** Two
causes, both in how the button was injected:

- The template button was located by its **label text** (`START`, `SETTINGS`).
  Those labels are localized, so after a language switch the template could no
  longer be found and the button was never re-injected — which is why it stayed
  gone instead of coming back. Templates are now matched on their persistent
  `onClick` handler names (`OnStartButton`, `OnSettingsButton`, `OnInviteButton`),
  which are serialized prefab data and never localized. Label matching remains as
  a fallback.
- The cloned button inherited the donor's I2 `Localize` component, which
  re-applies the **donor's** term on every language change — overwriting the
  "MODS" label with a second translated "START"/"SETTINGS". Those components are
  now stripped from the clone, and the label is re-asserted defensively while the
  menu is on screen.

Template lookup also now skips POGConfig's own clones, so a clone can never be
picked as the template for itself.

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
