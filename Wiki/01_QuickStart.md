# POGConfig Quick Start

This page shows the minimum setup to register config entries in your mod.

## 1) Add references

In your mod `.csproj`, add a reference to `POGConfig.dll` (path example):

```xml
<Reference Include="POGConfig">
  <HintPath>..\..\Mods\POGConfig.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Also include your normal MelonLoader/Unity references as usual.

## 2) Add namespace

```csharp
using POGMods.Config;
```

## 3) Register entries

Minimal example:

```csharp
using System.Collections.Generic;
using MelonLoader;
using POGMods.Config;

[assembly: MelonInfo(typeof(MyMod.MyMelon), "My Mod", "1.0.0", "You")]

namespace MyMod
{
    public class MyMelon : MelonMod
    {
        private static bool _enabled = true;
        private static float _speed = 5f;

        public override void OnInitializeMelon()
        {
            POGConfig.Register("My Mod", new List<ConfigEntry>
            {
                new ToggleEntry(
                    "Enable Feature",
                    () => _enabled,
                    v  => _enabled = v,
                    "EnableFeature"
                ),
                new SliderEntry(
                    "Speed",
                    () => _speed,
                    v  => _speed = v,
                    0f,
                    10f,
                    v => v.ToString("F1"),
                    "Speed"
                )
            });
        }
    }
}
```

## 4) How callbacks work

- `() => _enabled` returns the current value for UI sync.
- `v => _enabled = v` applies user changes from UI.
- `prefKey` (example: `"EnableFeature"`) enables persistence for that entry.

## 5) Common tips

- Keep setting fields static if accessed from static callbacks.
- Use clear labels; users see them directly.
- Start with one toggle + one slider, then expand.
- If a setting should not persist, omit `prefKey`.

## 6) Next step

Continue with `03_Architecture.md` for:

- internal flow (`Register` -> UI lifecycle -> callbacks)
- real-world usage patterns
- advanced slider and hotkey integration examples
