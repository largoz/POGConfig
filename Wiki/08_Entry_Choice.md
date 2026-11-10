# ChoiceRow

A setting picked from a list (new in 1.1.0). The row shows the current item; a click opens a list
over the page with search, filter buttons and a line of explanation per item.

## Constructors

```csharp
ChoiceRow(string label, Func<string> get, Action<string> set, params string[] options)   // key = label
ChoiceRow(string label, Func<string> get, Action<string> set, IEnumerable<Option> options)
ChoiceRow(string label, Func<string> get, Action<string> set, Func<IReadOnlyList<Option>> options) // read each time it opens
```

The value is the chosen item's `Option.Key`. Keys stay stable when labels are reworded or items are
added, removed or reordered, so a saved choice keeps meaning the same thing.

## Option

```csharp
new Option(string key, string label = null, string description = null, params string[] tags)
```

- `Label`: what the player reads (defaults to the key).
- `Description`: one line under the label in the list.
- `Tags`: matched by the filter buttons and by search.

## Modules

| Call | Effect |
|---|---|
| `.Saved(prefKey)` | keep the choice in `MelonPreferences.cfg` under the mod's category |
| `.Searchable(bool on = true)` | search field (shown by itself from 10 items on) |
| `.Filters(params string[] tags)` | one filter button per tag; several can be on at once |
| `.Description(text)` | a line under the row's label; wraps and makes the row taller if needed |

Calls return the row, so they chain: `new ChoiceRow(...).Saved("Key").Filters("A", "B")`.

## Behaviour

- Typing goes into the search field; arrows, Page Up/Down, Home/End move; Enter picks; the wheel
  scrolls; Esc or a click outside closes without a change.
- Esc closes the list first; only the next Esc closes the panel.
- Only rows in view exist, so thousands of items cost the same as ten.
- RESTORE DEFAULTS sets the key the row had when it was registered.
