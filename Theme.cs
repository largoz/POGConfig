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
    /// <para><b>One look for every mod.</b> Sizes are named properties (<c>font-size-label</c>,
    /// <c>row-height</c>…) kept here instead of numbers spread through the code, and they are the same on
    /// every mod's page: a mod does not set its own sizes or colours. Only the player changes the look, and
    /// only all of it at once, through <see cref="Scale"/>, like zoom in a browser
    /// (docs/design/modular-rows.md §7). Sizes are in units before <see cref="Scale"/>.</para>
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

        /// <summary>The panel's sizes, in units before <see cref="Scale"/>. The same for every mod.</summary>
        public static readonly IReadOnlyDictionary<string, float> Defaults = new Dictionary<string, float>
        {
            ["font-size-title"] = 22f,
            ["font-size-header"] = 13f,
            ["font-size-label"] = 14f,
            ["font-size-value"] = 13f,
            ["font-size-small"] = 12f,
            ["row-height"] = 40f,
            ["header-height"] = 28f,
            ["header-text-bottom"] = 6f,   // gap between a section heading's text and the row's bottom edge
            ["header-line-offset"] = 1f,   // the heading's underline, from the row's bottom edge
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
