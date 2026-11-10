using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace POGMods.Config
{
    /// <summary>
    /// Look and size of the panel, in one place.
    ///
    /// <para><b>Scale.</b> Everything in the panel is laid out in canvas units on a canvas whose
    /// reference resolution is 1920x1080 divided by <see cref="Scale"/>, so this one number grows or
    /// shrinks the whole panel, text included. The player sets it in
    /// <c>UserData/MelonPreferences.cfg</c>, <c>[POG_Config] UIScale</c>; it applies at once.</para>
    ///
    /// <para><b>Styles cascade like CSS.</b> A property (<c>font-size</c>, <c>row-height</c>…) comes
    /// from the strongest level that sets it: POG Config's defaults, then the player, then a mod's page,
    /// then a row type, then a single row. This class holds the first level; the others arrive with the
    /// modular rows (docs/design/modular-rows.md §7). Sizes are in units before <see cref="Scale"/>.</para>
    /// </summary>
    public static class Theme
    {
        public const float DefaultScale = 1.35f;
        public const float MinScale = 0.75f;
        public const float MaxScale = 2f;
        internal static readonly Vector2 BaseReference = new Vector2(1920f, 1080f);

        public static float Scale { get; private set; } = DefaultScale;

        /// <summary>Raised after <see cref="Scale"/> changed.</summary>
        public static event Action ScaleChanged;

        /// <summary>The canvas reference resolution that produces <see cref="Scale"/>.</summary>
        internal static Vector2 ReferenceResolution => BaseReference / Scale;

        /// <summary>Level 1 of the cascade: POG Config's own defaults, in units before <see cref="Scale"/>.</summary>
        public static readonly IReadOnlyDictionary<string, float> Defaults = new Dictionary<string, float>
        {
            ["font-size-title"] = 22f,
            ["font-size-header"] = 13f,
            ["font-size-label"] = 14f,
            ["font-size-value"] = 13f,
            ["font-size-small"] = 12f,
            ["row-height"] = 40f,
            ["header-height"] = 28f,
            ["row-gap"] = 2f,
        };

        private static MelonPreferences_Entry<float> _scalePref;

        /// <summary>Reads the player's scale and follows later edits of it.</summary>
        internal static void Load()
        {
            try
            {
                var cat = MelonPreferences.CreateCategory("POG_Config", "POG Config");
                _scalePref = cat.GetEntry<float>("UIScale") ?? cat.CreateEntry("UIScale", DefaultScale, "UI scale",
                    $"Size of the MOD SETTINGS panel and its text: 1 = original size, {DefaultScale} = default, {MinScale}–{MaxScale}.");
                Apply(_scalePref.Value);
                _scalePref.OnEntryValueChanged.Subscribe((_, v) => Apply(v));
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] UI scale setting unavailable, using {DefaultScale}: {ex.Message}"); }
        }

        private static void Apply(float value)
        {
            float s = float.IsNaN(value) ? DefaultScale : Mathf.Clamp(value, MinScale, MaxScale);
            if (Math.Abs(s - Scale) < 0.0001f) return;
            Scale = s;
            ScaleChanged?.Invoke();
        }
    }
}
