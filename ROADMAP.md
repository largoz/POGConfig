# POGConfig — Roadmap

Історія вже випущених версій — у
[`../../ThunderstorePackages/POGConfig/CHANGELOG.md`](../../ThunderstorePackages/POGConfig/CHANGELOG.md).

## Готово (1.0.7)

- [x] `ToggleEntry`, `SliderEntry` (з редагованим текстовим полем, суфіксами,
      origin-fill, step points), `OptionsSliderEntry`, `KeyEntry`.
- [x] Персистентність через `MelonPreferences` + `prefKey`.
- [x] MODS-кнопка як справжня `NetworkMenuButton`, інжектована в Buttons Panel
      головного меню (не floating overlay).
- [x] Робочий скролбар (drag, click-to-center) окремо для сайдбару і контенту.
- [x] Блокування інпуту на меню під панеллю, поки конфіг відкритий; `Esc` закриває панель.
- [x] Повний Developer Wiki (`Wiki/`).

## Заплановано / ідеї

- [ ] Перевести pause-menu MODS-кнопку на той самий підхід "справжньої
      NetworkMenuButton", що вже зроблено для головного меню (зараз
      pause-версія лишається floating-overlay — див. CHANGELOG 1.0.7).
- [ ] Нові типи `ConfigEntry`: текстове поле (string input), color picker.
- [ ] Групування записів мода в колапсовані секції, якщо список стає довгим
      (зараз лише `HeaderEntry` як візуальний розділювач без згортання).
- [ ] Пам'ятати позицію скролу при перевідкритті панелі (зараз скидається).

## Відомі проблеми

- Немає репортованих на 1.0.7.

## Нагадування про сумісність

При додаванні нового параметра до існуючого конструктора **завжди** давати
overload зі старою сигнатурою (`this(..., defaultValue)`), інакше DLL старих
модів, скомпільованих проти попередньої версії, впадуть з
`MissingMethodException` — див. `_research/PogModdingNotes.md`
§"Бінарна сумісність".
