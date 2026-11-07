# Entry: SliderEntry

`SliderEntry` is a numeric control for continuous or stepped values.

It supports:

- min/max clamping
- value formatting (`fmt`)
- optional persistence (`prefKey`)
- origin-based fill visualization
- whole-number snapping
- optional step point markers
- inline numeric text input
- adjustable row column proportions

## Full constructor

```csharp
new SliderEntry(
    string label,
    Func<float> get,
    Action<float> set,
    float min,
    float max,
    Func<float, string> fmt = null,
    string prefKey = null,
    float originValue = 0f,
    bool showFill = true,
    bool wholeNumbers = false,
    int stepPointsCount = 0
)
```

## Parameter guide

- `label` - row title.
- `get` - current numeric value.
- `set` - apply value from UI.
- `min`, `max` - valid range.
- `fmt` - display formatter (default like `F1`).
- `prefKey` - optional persistence key.
- `originValue` - visual fill anchor point.
- `showFill` - show/hide fill bar.
- `wholeNumbers` - force integer snapping.
- `stepPointsCount` - draw visual tick points (`>= 2`).

Runtime notes from implementation:

- `originValue` is clamped into `[min, max]` before fill rendering.
- `stepPointsCount` is sanitized to non-negative; points render only when count is `>= 2`.
- Step points are sized in whole screen pixels (about 5 px at 1080p), so every dot renders
  the same size at any resolution, and the end dots sit just inside the track.

## Column width tuning (`LabelFraction`, `ValueFraction`)

Default column proportions for the `[Label] [Value] [Slider]` layout — optional, change only when a specific row needs different spacing:

- `LabelFraction` — label width (default `0.35`)
- `ValueFraction` — value box width (default `0.15`)
- slider takes the remaining width

Example:

```csharp
new SliderEntry("Speed", () => _speed, v => _speed = v, 0f, 10f)
{
    LabelFraction = 0.45f,
    ValueFraction = 0.10f
};
```

## Example A: simple percentage

```csharp
private static float _volume = 0.75f;

new SliderEntry(
    "Volume",
    () => _volume,
    v => _volume = v,
    0f,
    1f,
    v => $"{v * 100:F0}%",
    "Volume"
);
```

## Example B: suffix formatting (`s`)

```csharp
private static float _durationSeconds = 43f;

new SliderEntry(
    "Duration",
    () => _durationSeconds,
    v => _durationSeconds = v,
    5f,
    180f,
    v => $"{v:F0}s",
    "DurationSeconds",
    5f,
    true,
    true
);
```

## Example C: bidirectional fill from zero

```csharp
private static float _temperatureOffset;

new SliderEntry(
    "Temperature Offset",
    () => _temperatureOffset,
    v => _temperatureOffset = v,
    -50f,
    50f,
    v => $"{v:F1}",
    "TemperatureOffset",
    originValue: 0f
);
```

## Example D: integer steps with point markers

```csharp
private static int _points = 2;

new SliderEntry(
    "Points",
    () => _points,
    v => _points = (int)v,
    0f,
    8f,
    v => $"{v:F0}",
    "Points",
    originValue: 0f,
    showFill: false,
    wholeNumbers: true,
    stepPointsCount: 9
);
```

## Inline input behavior

The slider value box can be edited directly:

- click value box
- type number
- `Enter` to confirm
- `Escape` to cancel
- click outside to confirm

Parsing behavior is permissive:

- supports both `.` and `,` decimals
- extracts first numeric fragment from mixed text
- clamps result to `[min, max]`
- rounds when `wholeNumbers = true`

Value is clamped to `[min, max]`.  
Formatter output is re-applied after confirmation.

Note: while slider text input is active, other keyboard-capture flows (for example keybind listening) are suppressed intentionally.

## Recommended usage patterns

- **Sensitivity/Speed/Scale**: standard continuous slider.
- **Durations and cooldowns**: slider + suffix formatter (`s`, `ms`).
- **Offsets around neutral**: origin at `0`.
- **Discrete tuning points**: `wholeNumbers` + `stepPointsCount`.

## Anti-patterns

- Using slider for named modes (`Easy/Normal/Hard`): use `OptionsSliderEntry`.
- Returning stale values in `get`: causes UI desync.
- Mixing incompatible format and parse expectations.
- Setting `LabelFraction + ValueFraction >= 1`: leaves no usable width for slider track.

## Validation checklist

- Range is correct (`min < max`).
- Default value sits inside range.
- `wholeNumbers` used for integer-backed fields.
- `prefKey` present only when persistence is desired.
