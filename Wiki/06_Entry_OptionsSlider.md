# Entry: OptionsSliderEntry

`OptionsSliderEntry` is for discrete named choices (string options).

Use it when values are semantic modes, not numeric measurements.

## Constructor

```csharp
new OptionsSliderEntry(
    string label,
    Func<int> get,
    Action<int> set,
    string[] options,
    string prefKey = null
)
```

## Parameters

- `label` - row title.
- `get` - returns current selected index.
- `set` - receives selected index after user input.
- `options` - display labels by index.
- `prefKey` - optional persistence key.

Implementation details:

- `options == null` becomes empty array internally.
- current index is clamped to valid bounds on read/write.

## Example

```csharp
private static int _difficulty = 1; // 0 Easy, 1 Normal, 2 Hard

new OptionsSliderEntry(
    "Difficulty",
    () => _difficulty,
    v => _difficulty = v,
    new[] { "Easy", "Normal", "Hard" },
    "Difficulty"
);
```

## How it differs from SliderEntry

- Always discrete (integer index).
- Always bound to option labels.
- Intended for mode selection UX.
- Avoids fake numeric representations for non-numeric settings.

## Good usage patterns

- Difficulty mode
- Quality preset
- AI behavior profile
- UI style preset

## Common mistakes

- Passing empty `options` array.
- Assuming index is stable after reordering options across releases.
- Using this for free-form numeric values.

If options are empty, UI falls back to `-` text and value updates are effectively inert.

## Stability tip

If you persist by index, keep options order stable between versions to avoid remapping user selections unintentionally.

Also avoid deleting old options in the middle of the array between releases; append new modes to the end when possible.
