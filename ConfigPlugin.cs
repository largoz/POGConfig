using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2CppGame.Menu;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRewired;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(POGMods.Config.PogConfigMelon), "POG Config", POGMods.Config.PogConfigMelon.VERSION, "largo", "")]

namespace POGMods.Config
{
    internal static class Clicks
    {
        internal static readonly Dictionary<RectTransform, Action> Map = new();
        internal static void Register(RectTransform rt, Action cb) => Map[rt] = cb;
        internal static void Unregister(RectTransform rt) { if (rt != null) Map.Remove(rt); }
        internal static void Clear() => Map.Clear();
    }

    internal static class UI
    {
        // Square slider handle edge. Its travel is inset by half of it on each side.
        internal const float SliderHandleSize = 14f;

        private static Sprite _white;
        internal static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var t = new Texture2D(1, 1);
                t.SetPixel(0, 0, Color.white);
                // Point filtering keeps flat-color rects hard-edged; bilinear sampling of a
                // 1x1 sprite softens edges unevenly under non-integer canvas scale.
                t.filterMode = FilterMode.Point;
                t.Apply();
                _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }

        internal static GameObject Make(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static RectTransform Rt(GameObject go) => go.GetComponent<RectTransform>();

        internal static void Stretch(RectTransform rt, float l = 0, float r = 0, float b = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        internal static Image AddImage(GameObject go, Color color, bool raycast = true)
        {
            var img = go.AddComponent<Image>();
            img.sprite = White; img.color = color; img.raycastTarget = raycast;
            return img;
        }

        internal static TextMeshProUGUI AddText(GameObject go, string text, TMP_FontAsset font,
            int size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text; tmp.fontSize = size; tmp.color = color;
            tmp.alignment = align; tmp.raycastTarget = false;
            return tmp;
        }

        // Anchors rt to top of parent. yTop = distance from parent top. height = item height.
        internal static void PlaceFromTop(RectTransform rt, float yTop, float height,
            float padL = 0, float padR = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(padL,  -(yTop + height));
            rt.offsetMax = new Vector2(-padR, -yTop);
        }

        // Flat button pinned to the right edge of `parent`, vertically centred. Returns its
        // rect so the caller can register the click.
        internal static RectTransform AddButton(Transform parent, string label, TMP_FontAsset font,
            Color bg, Color fg, float width = 170f, float height = 26f)
        {
            var go = Make("Btn", parent);
            var rt = Rt(go);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            AddImage(go, bg);
            var txtGo = Make("Text", go.transform);
            Stretch(Rt(txtGo), 6, 6, 0, 0);
            var tmp = AddText(txtGo, label, font, 12, fg, TextAlignmentOptions.Center);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return rt;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public abstract class ConfigEntry
    {
        public string Label { get; protected set; }
        protected ConfigEntry(string label) { Label = label; }

        public virtual void Draw(float x, float y, float width, GUIStyle labelStyle) { }
        public virtual float Height => 36f;

        // The row's height once it is built and its width is known (wrapped text grows it).
        // Called right after BuildRowInto; the default keeps the height the row was placed with.
        internal virtual float MeasureHeight(float width, float placedHeight) => placedHeight;

        internal abstract void BuildRowInto(GameObject row, TMP_FontAsset font);
        internal virtual void BuildRow(Transform parent, TMP_FontAsset font) { }
        internal virtual void OnUpdate() { }
        internal virtual void BindPrefs(MelonPreferences_Category cat) { }

        // Ends an interaction still in progress (typing a value, listening for a key).
        // Called before the rows are rebuilt and when the panel closes, so nothing is
        // left half-open on rows that are gone or hidden.
        internal virtual void EndInteraction() { }

        // Restore-defaults support. CaptureDefault runs at Register time BEFORE BindPrefs
        // loads any saved value, so "default" = whatever the mod hardcoded, not whatever
        // happens to be saved. RestoreDefault re-applies it through the wrapped setter,
        // which also persists; row UI resyncs via the entry's own OnUpdate.
        internal virtual void CaptureDefault() { }
        internal virtual void RestoreDefault() { }
    }

    /// <summary>Visual section header within a mod's settings list. Has no value, no persistence.</summary>
    public class HeaderEntry : ConfigEntry
    {
        public override float Height => 28f;
        public HeaderEntry(string label) : base(label) { }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            var rRt = UI.Rt(row);
            // Underline
            var lineGo = UI.Make("Underline", row.transform);
            var lRt = UI.Rt(lineGo);
            lRt.anchorMin = new Vector2(0, 0); lRt.anchorMax = new Vector2(1, 0);
            lRt.pivot = new Vector2(0.5f, 0); lRt.sizeDelta = new Vector2(0, 1);
            lRt.anchoredPosition = new Vector2(0, 4);
            UI.AddImage(lineGo, new Color(1f, 0.82f, 0.3f, 0.25f), false);

            var lblGo = UI.Make("Header", row.transform);
            var lblRt = UI.Rt(lblGo);
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
            var t = UI.AddText(lblGo, Label, font, 13, new Color(1f, 0.82f, 0.3f),
                               TextAlignmentOptions.BottomLeft);
            t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        }
    }

    /// <summary>
    /// A row with a button that runs an action, e.g. "Reset stats" or "Reload config".
    /// Holds no value, is not persisted, and RESTORE DEFAULTS leaves it alone.
    /// </summary>
    public class ButtonEntry : ConfigEntry
    {
        private readonly string _buttonText;
        private readonly Action _onClick;

        public ButtonEntry(string label, string buttonText, Action onClick) : base(label)
        {
            _buttonText = string.IsNullOrEmpty(buttonText) ? "RUN" : buttonText;
            _onClick = onClick;
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            var lbl = UI.Make("Label", row.transform);
            UI.Rt(lbl).anchorMin = Vector2.zero;
            UI.Rt(lbl).anchorMax = new Vector2(0.60f, 1f);
            UI.Rt(lbl).offsetMin = UI.Rt(lbl).offsetMax = Vector2.zero;
            var lblTmp = UI.AddText(lbl, Label, font, 14, new Color(0.88f, 0.88f, 0.88f));
            lblTmp.enableWordWrapping = false;
            lblTmp.overflowMode = TextOverflowModes.Ellipsis;

            var btnRt = UI.AddButton(row.transform, _buttonText, font,
                                     new Color(0.24f, 0.20f, 0.10f), new Color(1f, 0.82f, 0.3f));
            Clicks.Register(btnRt, Run);
        }

        private void Run()
        {
            try { _onClick?.Invoke(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] Button '{Label}': {ex.Message}"); }
        }
    }

    /// <summary>Optional metadata supplied by a mod when registering. All fields optional.</summary>
    public class ModInfo
    {
        public string Version;
        public string Description;
        public string Author;
        public ModInfo() { }
        public ModInfo(string version, string description = null, string author = null)
        { Version = version; Description = description; Author = author; }
    }

    public class ToggleEntry : ConfigEntry
    {
        private readonly Func<bool> _get;
        private Action<bool> _set;
        private readonly string _prefKey;
        private Toggle _toggle;
        private bool _lastValue;
        private bool _suppressToggleCallback;

        private bool _defaultValue;

        public ToggleEntry(string label, Func<bool> get, Action<bool> set)
            : this(label, get, set, null) { }
        public ToggleEntry(string label, Func<bool> get, Action<bool> set, string prefKey) : base(label)
        { _get = get; _set = set; _prefKey = prefKey; }

        internal override void CaptureDefault() => _defaultValue = _get();
        internal override void RestoreDefault() { _set(_defaultValue); _lastValue = _defaultValue; }

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var pref = Prefs.Entry(cat, _prefKey, _get());
            _set(pref.Value);
            var orig = _set;
            _set = v => { orig(v); pref.Value = v; Prefs.MarkDirty(); };
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            var lbl = UI.Make("Label", row.transform);
            UI.Rt(lbl).anchorMin = Vector2.zero;
            UI.Rt(lbl).anchorMax = new Vector2(0.78f, 1f);
            UI.Rt(lbl).offsetMin = UI.Rt(lbl).offsetMax = Vector2.zero;
            UI.AddText(lbl, Label, font, 14, new Color(0.88f, 0.88f, 0.88f));

            var tGo = UI.Make("Toggle", row.transform);
            var tRt = UI.Rt(tGo);
            tRt.anchorMin = tRt.anchorMax = new Vector2(1f, 0.5f);
            tRt.pivot = new Vector2(1f, 0.5f);
            tRt.sizeDelta = new Vector2(22, 22); // square checkbox
            tRt.anchoredPosition = Vector2.zero;

            _toggle = tGo.AddComponent<Toggle>();
            var bg = UI.Make("BG", tGo.transform);
            UI.Stretch(UI.Rt(bg));
            var bgImg = UI.AddImage(bg, new Color(0.18f, 0.18f, 0.2f));
            // Checkmark: fixed-size square centred in the box (not stretch-with-insets) —
            // centred rects stay symmetric under fractional canvas scale, insets don't.
            var ck = UI.Make("Check", bg.transform);
            var ckRt = UI.Rt(ck);
            ckRt.anchorMin = ckRt.anchorMax = new Vector2(0.5f, 0.5f);
            ckRt.pivot = new Vector2(0.5f, 0.5f);
            ckRt.sizeDelta = new Vector2(14, 14);
            ckRt.anchoredPosition = Vector2.zero;
            var ckImg = UI.AddImage(ck, new Color(1f, 0.82f, 0.3f));

            _toggle.targetGraphic = bgImg;
            _toggle.graphic = ckImg;
            _lastValue = _get();
            _toggle.SetIsOnWithoutNotify(_lastValue);
            _toggle.onValueChanged.AddListener((Action<bool>)OnToggleChanged);
        }

        private void OnToggleChanged(bool value)
        {
            if (_suppressToggleCallback) return;
            if (value == _lastValue) return;
            _lastValue = value;
            _set(value);
        }

        internal override void OnUpdate()
        {
            if (_toggle == null) return;
            bool ext = _get();
            if (_toggle.isOn == ext) return;
            _suppressToggleCallback = true;
            _toggle.SetIsOnWithoutNotify(ext);
            _suppressToggleCallback = false;
            _lastValue = ext;
        }
    }

    public class SliderEntry : ConfigEntry
    {
        private readonly Func<float> _get;
        private Action<float> _set;
        private readonly float _min, _max;
        private readonly Func<float, string> _fmt;
        private readonly string _prefKey;
        private readonly float _originValue;
        private readonly bool _showFill;
        private readonly bool _wholeNumbers;
        private readonly int _stepPointsCount;
        // Column width fractions (must sum to < 1; slider takes the rest).
        public float LabelFraction { get; set; } = 0.35f;
        public float ValueFraction { get; set; } = 0.15f;

        private Slider _slider;
        private TextMeshProUGUI _inputText;
        private RectTransform _valueBgRt;
        private Image _valueBgImg;
        private RectTransform _fillRt;
        private bool _editingInput;
        private string _inputBuffer = "";
        private RectTransform _labelMaskRt;
        private RectTransform _valueMaskRt;
        private RectTransform _labelTextRt;
        private RectTransform _valueTextRt;
        private TextMeshProUGUI _labelTmp;
        private float _lastValue;
        private float _marqueeTimer;
        private const float MarqueeSpeed = 52f;

        // The slider whose value box is being typed into, if any. "Some text input is
        // active" is derived from this and the search box instead of a separate flag, so
        // it cannot stay stuck true after the row or the panel goes away mid-edit.
        private static SliderEntry s_editing;
        internal static bool AnyInputActive => s_editing != null || ConfigBehaviour.SearchEditing;

        public SliderEntry(string label, Func<float> get, Action<float> set, float min, float max,
                           Func<float, string> fmt = null) : this(label, get, set, min, max, fmt, null) { }
        public SliderEntry(string label, Func<float> get, Action<float> set, float min, float max,
                           Func<float, string> fmt, string prefKey)
            : this(label, get, set, min, max, fmt, prefKey, 0f, true, false, 0) { }
        public SliderEntry(string label, Func<float> get, Action<float> set, float min, float max,
                           Func<float, string> fmt, string prefKey, float originValue, bool showFill, bool wholeNumbers)
            : this(label, get, set, min, max, fmt, prefKey, originValue, showFill, wholeNumbers, 0) { }
        public SliderEntry(string label, Func<float> get, Action<float> set, float min, float max,
                           Func<float, string> fmt, string prefKey, float originValue, bool showFill, bool wholeNumbers, int stepPointsCount) : base(label)
        {
            _get = get;
            _set = set;
            _min = min;
            _max = max;
            _fmt = fmt ?? (v => v.ToString("F1"));
            _prefKey = prefKey;
            _originValue = Mathf.Clamp(originValue, min, max);
            _showFill = showFill;
            _wholeNumbers = wholeNumbers;
            _stepPointsCount = Math.Max(0, stepPointsCount);
        }

        private float _defaultValue;
        internal override void CaptureDefault() => _defaultValue = _get();
        internal override void RestoreDefault() => ApplyValue(_defaultValue, true);
        internal override void EndInteraction() { if (_editingInput) StopEditing(true); }

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var pref = Prefs.Entry(cat, _prefKey, _get());
            _set(pref.Value);
            var orig = _set;
            _set = v => { orig(v); pref.Value = v; Prefs.MarkDirty(); };
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            float lEnd = LabelFraction;
            float vEnd = lEnd + ValueFraction;
            float sStart = vEnd + 0.02f;

            var lblMask = UI.Make("LabelMask", row.transform);
            _labelMaskRt = UI.Rt(lblMask);
            _labelMaskRt.anchorMin = Vector2.zero;
            _labelMaskRt.anchorMax = new Vector2(lEnd, 1f);
            _labelMaskRt.offsetMin = _labelMaskRt.offsetMax = Vector2.zero;
            lblMask.AddComponent<RectMask2D>();

            var lbl = UI.Make("Label", lblMask.transform);
            _labelTextRt = UI.Rt(lbl);
            _labelTextRt.anchorMin = Vector2.zero;
            _labelTextRt.anchorMax = Vector2.one;
            _labelTextRt.offsetMin = _labelTextRt.offsetMax = Vector2.zero;
            _labelTmp = UI.AddText(lbl, Label, font, 14, new Color(0.88f, 0.88f, 0.88f));
            _labelTmp.enableWordWrapping = false;
            _labelTmp.overflowMode = TextOverflowModes.Ellipsis;

            var valMask = UI.Make("ValueMask", row.transform);
            _valueMaskRt = UI.Rt(valMask);
            _valueMaskRt.anchorMin = new Vector2(lEnd, 0f);
            _valueMaskRt.anchorMax = new Vector2(vEnd, 1f);
            _valueMaskRt.offsetMin = _valueMaskRt.offsetMax = Vector2.zero;
            valMask.AddComponent<RectMask2D>();

            var valBg = UI.Make("ValueBG", valMask.transform);
            _valueBgRt = UI.Rt(valBg);
            _valueBgRt.anchorMin = new Vector2(0f, 0.18f);
            _valueBgRt.anchorMax = new Vector2(1f, 0.82f);
            _valueBgRt.offsetMin = new Vector2(2f, 0f);
            _valueBgRt.offsetMax = new Vector2(-2f, 0f);
            _valueBgImg = UI.AddImage(valBg, new Color(0.16f, 0.16f, 0.18f, 0.95f));
            Clicks.Register(_valueBgRt, StartEditing);

            var inputArea = UI.Make("ValueInput", valBg.transform);
            _valueTextRt = UI.Rt(inputArea);
            _valueTextRt.anchorMin = Vector2.zero;
            _valueTextRt.anchorMax = Vector2.one;
            _valueTextRt.offsetMin = new Vector2(6f, 0f);
            _valueTextRt.offsetMax = new Vector2(-6f, 0f);

            _inputText = UI.AddText(inputArea, _fmt(_get()), font, 13, new Color(1f, 0.82f, 0.3f),
                                    TextAlignmentOptions.MidlineRight);
            _inputText.enableWordWrapping = false;
            _inputText.overflowMode = TextOverflowModes.Ellipsis;

            var sGo = UI.Make("Slider", row.transform);
            UI.Rt(sGo).anchorMin = new Vector2(sStart, 0.25f);
            UI.Rt(sGo).anchorMax = new Vector2(1f, 0.75f);
            UI.Rt(sGo).offsetMin = UI.Rt(sGo).offsetMax = Vector2.zero;

            _slider = sGo.AddComponent<Slider>();
            _slider.minValue = _min; _slider.maxValue = _max;
            _slider.wholeNumbers = _wholeNumbers;
            _slider.direction = Slider.Direction.LeftToRight;

            var track = UI.Make("Track", sGo.transform);
            UI.Stretch(UI.Rt(track));
            UI.AddImage(track, new Color(0.2f, 0.2f, 0.22f));
            BuildStepPoints(sGo.transform);

            var fillArea = UI.Make("FillArea", sGo.transform);
            UI.Rt(fillArea).anchorMin = new Vector2(0f, 0.25f);
            UI.Rt(fillArea).anchorMax = new Vector2(1f, 0.75f);
            UI.Rt(fillArea).offsetMin = UI.Rt(fillArea).offsetMax = Vector2.zero;
            var fill = UI.Make("Fill", fillArea.transform);
            UI.Rt(fill).anchorMin = Vector2.zero;
            UI.Rt(fill).anchorMax = new Vector2(0f, 1f);
            UI.Rt(fill).offsetMin = UI.Rt(fill).offsetMax = Vector2.zero;
            UI.AddImage(fill, new Color(1f, 0.72f, 0.1f));
            _fillRt = UI.Rt(fill);
            fillArea.SetActive(_showFill);

            // The handle travels between half-handle insets, so at min/max it stays on the
            // track instead of hanging half off the end, where the list's mask clipped it.
            var handleArea = UI.Make("HandleArea", sGo.transform);
            float half = UI.SliderHandleSize * 0.5f;
            UI.Stretch(UI.Rt(handleArea), half, half, 0, 0);
            var handle = UI.Make("Handle", handleArea.transform);
            UI.Rt(handle).anchorMin = UI.Rt(handle).anchorMax = new Vector2(0f, 0.5f);
            UI.Rt(handle).sizeDelta = new Vector2(UI.SliderHandleSize, UI.SliderHandleSize);
            var hImg = UI.AddImage(handle, new Color(1f, 0.82f, 0.3f));

            _slider.fillRect = null;
            _slider.handleRect = UI.Rt(handle);
            _slider.targetGraphic = hImg;
            _lastValue = _get();
            _slider.SetValueWithoutNotify(_lastValue);
            _slider.onValueChanged.AddListener((Action<float>)OnSliderChanged);
            UpdateFillFromOrigin(_lastValue);
        }

        private const float StepDotUnits = 5f;
        private static readonly Color StepDotColor = new Color(0.62f, 0.62f, 0.68f, 0.9f);

        private void BuildStepPoints(Transform sliderTransform)
        {
            if (_stepPointsCount < 2) return;

            // Dot size in whole screen pixels. A quad N px wide covers exactly N pixel centres
            // wherever it sits, so every dot renders the same size; a fractional size (4 canvas
            // units = 2.8 px at 1366x768) came out 2 or 3 px depending on its position.
            var canvas = sliderTransform.GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;
            float size = Mathf.Max(3f, Mathf.Round(StepDotUnits * scale)) / scale;
            // Same inset as the handle's travel: each dot sits exactly where the handle stops
            // for that step, and the end dots stay inside the track.
            float inset = UI.SliderHandleSize * 0.5f;

            var pointsGo = UI.Make("StepPoints", sliderTransform);
            var pointsRt = UI.Rt(pointsGo);
            pointsRt.anchorMin = new Vector2(0f, 0.5f);
            pointsRt.anchorMax = new Vector2(1f, 0.5f);
            pointsRt.offsetMin = new Vector2(inset, -1f);
            pointsRt.offsetMax = new Vector2(-inset, 1f);

            for (int i = 0; i < _stepPointsCount; i++)
            {
                float t = i / (float)(_stepPointsCount - 1);
                var dot = UI.Make("P" + i, pointsGo.transform);
                var dotRt = UI.Rt(dot);
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(t, 0.5f);
                dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.sizeDelta = new Vector2(size, size);
                dotRt.anchoredPosition = Vector2.zero;
                UI.AddImage(dot, StepDotColor, false);
            }
        }

        private void StartEditing()
        {
            if (AnyInputActive) return;
            _editingInput = true;
            s_editing = this;
            _marqueeTimer = 0f;
            _inputBuffer = ToEditableNumericString(_lastValue);
            if (_valueBgImg != null) _valueBgImg.color = new Color(0.28f, 0.22f, 0.08f);
            if (_inputText != null) _inputText.text = _inputBuffer + "|";
        }

        private void StopEditing(bool commit)
        {
            _editingInput = false;
            if (s_editing == this) s_editing = null;
            if (_valueBgImg != null) _valueBgImg.color = new Color(0.16f, 0.16f, 0.18f, 0.95f);
            if (commit && TryParseValue(_inputBuffer, out var parsed))
                ApplyValue(parsed, true);
            if (_inputText != null) _inputText.text = _fmt(_lastValue);
        }

        private bool TryParseValue(string text, out float parsed)
        {
            text = (text ?? string.Empty).Trim();
            parsed = 0f;
            if (text.Length == 0) return false;
            var m = Regex.Match(text, @"[-+]?\d+(?:[.,]\d+)?");
            if (m.Success) text = m.Value;
            string normalized = text.Replace(',', '.');
            bool ok = float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
            if (!ok) ok = float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);
            if (!ok) return false;
            parsed = Mathf.Clamp(parsed, _min, _max);
            if (_wholeNumbers) parsed = Mathf.Round(parsed);
            return true;
        }

        private string ToEditableNumericString(float value)
        {
            if (_wholeNumbers) return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void OnSliderChanged(float value) => ApplyValue(value, true);

        private void ApplyValue(float value, bool writeBack)
        {
            value = Mathf.Clamp(value, _min, _max);
            if (_wholeNumbers) value = Mathf.Round(value);
            _lastValue = value;
            if (_slider != null && Math.Abs(_slider.value - value) > 0.0001f) _slider.SetValueWithoutNotify(value);
            if (!_editingInput && _inputText != null) _inputText.text = _fmt(value);
            UpdateFillFromOrigin(value);
            if (writeBack) _set(value);
        }

        private void UpdateFillFromOrigin(float current)
        {
            if (_fillRt == null || !_showFill) return;
            float originNorm = Mathf.InverseLerp(_min, _max, _originValue);
            float currentNorm = Mathf.InverseLerp(_min, _max, current);
            _fillRt.anchorMin = new Vector2(Mathf.Min(originNorm, currentNorm), 0f);
            _fillRt.anchorMax = new Vector2(Mathf.Max(originNorm, currentNorm), 1f);
            _fillRt.offsetMin = Vector2.zero;
            _fillRt.offsetMax = Vector2.zero;
        }

        private void UpdateMarquee(RectTransform maskRt, RectTransform textRt, TextMeshProUGUI tmp, bool hovered)
        {
            if (maskRt == null || textRt == null || tmp == null) return;
            bool overflow = tmp.preferredWidth > maskRt.rect.width + 1f;
            if (!overflow)
            {
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                textRt.anchoredPosition = Vector2.zero;
                return;
            }

            if (!hovered)
            {
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                textRt.anchoredPosition = Vector2.zero;
                _marqueeTimer = 0f;
                return;
            }

            tmp.overflowMode = TextOverflowModes.Overflow;
            _marqueeTimer += Time.unscaledDeltaTime * MarqueeSpeed;
            float maxShift = Mathf.Max(0f, tmp.preferredWidth - maskRt.rect.width + 12f);
            float shift = Mathf.PingPong(_marqueeTimer, maxShift);
            textRt.anchoredPosition = new Vector2(-shift, 0f);
        }

        internal override void OnUpdate()
        {
            if (_slider == null) return;

            if (_editingInput)
            {
                // click outside → commit
                if (Input.GetMouseButtonDown(0) && !ConfigBehaviour.HitTest(_valueBgRt, Input.mousePosition))
                { StopEditing(true); return; }

                if (Input.GetKeyDown(KeyCode.Escape))
                { ConfigBehaviour.ConsumeEsc(); StopEditing(false); return; }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                { StopEditing(true); return; }

                foreach (char c in Input.inputString)
                {
                    if (c == '\b')
                    { if (_inputBuffer.Length > 0) _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1); }
                    else if (c == '\n' || c == '\r') { StopEditing(true); return; }
                    else if (c >= ' ') _inputBuffer += c;
                }
                if (_inputText != null) _inputText.text = _inputBuffer + "|";
                return;
            }

            float ext = _get();
            if (Math.Abs(ext - _lastValue) > 0.0001f) ApplyValue(ext, false);

            Vector2 mouse = Input.mousePosition;
            UpdateMarquee(_labelMaskRt, _labelTextRt, _labelTmp, ConfigBehaviour.HitTest(_labelMaskRt, mouse));
            UpdateMarquee(_valueMaskRt, _valueTextRt, _inputText, ConfigBehaviour.HitTest(_valueMaskRt, mouse));
        }
    }

    public class OptionsSliderEntry : ConfigEntry
    {
        private readonly Func<int> _get;
        private Action<int> _set;
        private readonly string[] _options;
        private readonly string _prefKey;
        private Slider _slider;
        private TextMeshProUGUI _displayText;
        private int _lastIndex;

        public OptionsSliderEntry(string label, Func<int> get, Action<int> set, string[] options, string prefKey = null)
            : base(label)
        {
            _get = get;
            _set = set;
            _options = options ?? Array.Empty<string>();
            _prefKey = prefKey;
        }

        private int _defaultIndex;
        internal override void CaptureDefault() => _defaultIndex = _get();
        internal override void RestoreDefault()
            => _set(Mathf.Clamp(_defaultIndex, 0, Math.Max(0, _options.Length - 1)));

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null || _options.Length == 0) return;
            var pref = Prefs.Entry(cat, _prefKey, Mathf.Clamp(_get(), 0, _options.Length - 1));
            _set(Mathf.Clamp(pref.Value, 0, _options.Length - 1));
            var orig = _set;
            _set = i => { orig(i); pref.Value = i; Prefs.MarkDirty(); };
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            var lbl = UI.Make("Label", row.transform);
            UI.Rt(lbl).anchorMin = Vector2.zero;
            UI.Rt(lbl).anchorMax = new Vector2(0.26f, 1f);
            UI.Rt(lbl).offsetMin = UI.Rt(lbl).offsetMax = Vector2.zero;
            var lblTmp = UI.AddText(lbl, Label, font, 14, new Color(0.88f, 0.88f, 0.88f));
            lblTmp.enableWordWrapping = false;
            lblTmp.overflowMode = TextOverflowModes.Ellipsis;

            var valueTextGo = UI.Make("ValueText", row.transform);
            var valueTextRt = UI.Rt(valueTextGo);
            valueTextRt.anchorMin = new Vector2(0.26f, 0f);
            valueTextRt.anchorMax = new Vector2(0.52f, 1f);
            valueTextRt.offsetMin = valueTextRt.offsetMax = Vector2.zero;
            _displayText = UI.AddText(valueTextGo, "", font, 13, new Color(1f, 0.82f, 0.3f), TextAlignmentOptions.MidlineRight);
            _displayText.enableWordWrapping = false;
            _displayText.overflowMode = TextOverflowModes.Ellipsis;

            var sGo = UI.Make("Slider", row.transform);
            UI.Rt(sGo).anchorMin = new Vector2(0.54f, 0.25f);
            UI.Rt(sGo).anchorMax = new Vector2(1f, 0.75f);
            UI.Rt(sGo).offsetMin = UI.Rt(sGo).offsetMax = Vector2.zero;

            _slider = sGo.AddComponent<Slider>();
            _slider.minValue = 0;
            _slider.maxValue = Mathf.Max(0, _options.Length - 1);
            _slider.wholeNumbers = true;
            _slider.direction = Slider.Direction.LeftToRight;

            var track = UI.Make("Track", sGo.transform);
            UI.Stretch(UI.Rt(track));
            UI.AddImage(track, new Color(0.2f, 0.2f, 0.22f));

            // The handle travels between half-handle insets, so at min/max it stays on the
            // track instead of hanging half off the end, where the list's mask clipped it.
            var handleArea = UI.Make("HandleArea", sGo.transform);
            float half = UI.SliderHandleSize * 0.5f;
            UI.Stretch(UI.Rt(handleArea), half, half, 0, 0);
            var handle = UI.Make("Handle", handleArea.transform);
            UI.Rt(handle).anchorMin = UI.Rt(handle).anchorMax = new Vector2(0f, 0.5f);
            UI.Rt(handle).sizeDelta = new Vector2(UI.SliderHandleSize, UI.SliderHandleSize);
            var hImg = UI.AddImage(handle, new Color(1f, 0.82f, 0.3f));
            _slider.fillRect = null;
            _slider.handleRect = UI.Rt(handle);
            _slider.targetGraphic = hImg;
            _slider.onValueChanged.AddListener((Action<float>)OnSliderChanged);

            _lastIndex = Mathf.Clamp(_get(), 0, _options.Length - 1);
            _slider.SetValueWithoutNotify(_lastIndex);
            SyncText(_lastIndex);
        }

        private void OnSliderChanged(float value)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(value), 0, _options.Length - 1);
            if (idx == _lastIndex) return;
            _lastIndex = idx;
            SyncText(idx);
            _set(idx);
        }

        private void SyncText(int idx)
        {
            string txt = (_options.Length == 0 || idx < 0 || idx >= _options.Length) ? "-" : _options[idx];
            if (_displayText != null) _displayText.text = txt;
        }

        internal override void OnUpdate()
        {
            if (_slider == null || _options.Length == 0) return;
            int ext = Mathf.Clamp(_get(), 0, _options.Length - 1);
            if (ext != _lastIndex)
            {
                _lastIndex = ext;
                _slider.SetValueWithoutNotify(ext);
                SyncText(ext);
            }
        }
    }

    /// <summary>
    /// Keybind entry with up to <see cref="MaxSlots"/> universal slots — any slot accepts
    /// either a keyboard key or a mouse button (KeyCode.Mouse0–Mouse6). Unlike the vanilla
    /// game's Rewired-driven binding screen (2 fixed keyboard slots + 1 fixed mouse slot),
    /// mod slots have no type restriction; whichever key or button the player presses is
    /// recorded in the slot they clicked.
    /// </summary>
    public class KeyEntry : ConfigEntry
    {
        public const int MaxSlots = 3;

        private readonly Func<int, KeyCode> _get;
        private Action<int, KeyCode> _set;
        private readonly string _prefKey;
        private readonly int _slotCount;

        // Slots are universal: any slot accepts a keyboard key OR a mouse button
        // (Mouse0–Mouse6). Defaults to true for both constructors. A mod can set it
        // false to restrict an entry to keyboard-only.
        public bool AllowMouseButtons { get; set; } = true;

        // The entry listening for a key, if any. "Some slot is listening" is derived from
        // this and the Vanilla tab instead of a separate flag that could be left stuck.
        private static KeyEntry s_listening;
        internal static bool AnyWaiting => s_listening != null || ConfigBehaviour.VanillaListening;
        private int _listeningSlot = -1;
        private int _listenStartFrame;

        private readonly TextMeshProUGUI[] _slotTexts = new TextMeshProUGUI[MaxSlots];
        private readonly Image[] _slotBgs = new Image[MaxSlots];
        private readonly RectTransform[] _bindRects = new RectTransform[MaxSlots];
        private readonly GameObject[] _clearGos = new GameObject[MaxSlots];

        // Last KeyCode rendered per slot — guards the per-frame refresh so TMP text is
        // only reassigned on change. Unknown sentinel forces a refresh on the next update.
        private const KeyCode UnknownKey = (KeyCode)(-1);
        private readonly KeyCode[] _lastShown = new KeyCode[MaxSlots];

        private static readonly Color NormalBg    = new Color(0.18f, 0.18f, 0.2f);
        private static readonly Color ListeningBg = new Color(0.30f, 0.24f, 0.08f);

        // ── Legacy single-slot API (binary-compatible with 1.0.x mods) ──
        public KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set)
            : this(label, get, set, null) { }
        public KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set, string prefKey)
            : this(label, i => get(), (i, v) => set(v), prefKey, 1) { }

        // ── Multi-slot API — up to MaxSlots universal keyboard/mouse slots ──
        // get(slot)/set(slot, value) index into whatever storage the mod uses (e.g. a
        // KeyCode[3] field). prefKey persists slot 0 as-is (compatible with the legacy
        // single-slot format) and slots 1/2 as "<prefKey>_2" / "<prefKey>_3".
        public KeyEntry(string label, Func<int, KeyCode> get, Action<int, KeyCode> set,
                        string prefKey, int slotCount) : base(label)
        {
            _get = get;
            _set = set;
            _prefKey = prefKey;
            _slotCount = Mathf.Clamp(slotCount, 1, MaxSlots);
        }

        private readonly KeyCode[] _defaults = new KeyCode[MaxSlots];
        internal override void CaptureDefault()
        { for (int i = 0; i < _slotCount; i++) _defaults[i] = _get(i); }
        internal override void RestoreDefault()
        {
            StopListening();
            for (int i = 0; i < _slotCount; i++) _set(i, _defaults[i]);
        }
        internal override void EndInteraction() => StopListening();

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var prefs = new MelonPreferences_Entry<string>[_slotCount];
            for (int i = 0; i < _slotCount; i++)
            {
                string key = i == 0 ? _prefKey : $"{_prefKey}_{i + 1}";
                var pref = Prefs.Entry(cat, key, _get(i).ToString());
                if (Enum.TryParse<KeyCode>(pref.Value, out var loaded)) _set(i, loaded);
                prefs[i] = pref;
            }
            var orig = _set;
            _set = (slot, v) =>
            {
                orig(slot, v);
                if (slot >= 0 && slot < prefs.Length)
                { prefs[slot].Value = v.ToString(); Prefs.MarkDirty(); }
            };
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            StopListening(); // fresh row: any prior listen belonged to destroyed UI

            var lbl = UI.Make("Label", row.transform);
            UI.Rt(lbl).anchorMin = Vector2.zero;
            UI.Rt(lbl).anchorMax = new Vector2(0.40f, 1f);
            UI.Rt(lbl).offsetMin = UI.Rt(lbl).offsetMax = Vector2.zero;
            var lblTmp = UI.AddText(lbl, Label, font, 14, new Color(0.88f, 0.88f, 0.88f));
            lblTmp.enableWordWrapping = false;
            lblTmp.overflowMode = TextOverflowModes.Ellipsis;

            // Fixed 3-column grid regardless of how many slots this entry uses, filled
            // from the RIGHT edge — a single-slot entry (the default) sits in the
            // rightmost column instead of floating mid-row.
            const float slotsStart = 0.40f;
            const float gap = 0.012f;
            float slotW = (1f - slotsStart - gap * (MaxSlots - 1)) / MaxSlots;

            for (int i = 0; i < _slotCount; i++)
            {
                int slot = i; // capture for closures
                int col = MaxSlots - _slotCount + i; // right-aligned column index
                float x0 = slotsStart + col * (slotW + gap);
                float x1 = x0 + slotW;

                var slotGo = UI.Make("Slot" + i, row.transform);
                var slotRt = UI.Rt(slotGo);
                slotRt.anchorMin = new Vector2(x0, 0.14f);
                slotRt.anchorMax = new Vector2(x1, 0.86f);
                slotRt.offsetMin = slotRt.offsetMax = Vector2.zero;
                _slotBgs[i] = UI.AddImage(slotGo, NormalBg);

                // Bind area covers the slot minus a 14px column reserved for the clear "x" —
                // a separate, non-overlapping rect so Clicks.Map (first-hit-wins over an
                // unordered Dictionary) never has to arbitrate between the two regions.
                var bindGo = UI.Make("Bind", slotGo.transform);
                var bindRt = UI.Rt(bindGo);
                UI.Stretch(bindRt, 0, 14, 0, 0);
                _bindRects[i] = bindRt;
                Clicks.Register(bindRt, () => StartListening(slot));

                var txtGo = UI.Make("Text", slotGo.transform);
                var txtRt = UI.Rt(txtGo);
                UI.Stretch(txtRt, 4, 16, 0, 0);
                var txt = UI.AddText(txtGo, KeyDisplayName(_get(i)), font, 12,
                                     new Color(1f, 0.82f, 0.3f), TextAlignmentOptions.MidlineLeft);
                txt.enableWordWrapping = false;
                txt.overflowMode = TextOverflowModes.Ellipsis;
                _slotTexts[i] = txt;
                _lastShown[i] = _get(i);

                // Clear "x" — small corner box pinned top-right, only shown while the slot
                // holds a real bind.
                var clearGo = UI.Make("Clear", slotGo.transform);
                var clearRt = UI.Rt(clearGo);
                clearRt.anchorMin = clearRt.anchorMax = new Vector2(1f, 1f);
                clearRt.pivot = new Vector2(1f, 1f);
                clearRt.sizeDelta = new Vector2(14, 16);
                clearRt.anchoredPosition = Vector2.zero;
                UI.AddText(clearGo, "x", font, 11, new Color(0.85f, 0.5f, 0.5f), TextAlignmentOptions.Center);
                Clicks.Register(clearRt, () => ClearSlot(slot));
                _clearGos[i] = clearGo;
                clearGo.SetActive(_get(i) != KeyCode.None);
            }
        }

        private static string KeyDisplayName(KeyCode kc) => kc == KeyCode.None ? "—" : kc.ToString();

        private void StartListening(int slot)
        {
            if (AnyWaiting) return;
            _listeningSlot = slot;
            s_listening = this;
            _listenStartFrame = Time.frameCount;
            if (_slotTexts[slot] != null) _slotTexts[slot].text = "…";
            if (_slotBgs[slot] != null) _slotBgs[slot].color = ListeningBg;
        }

        private void StopListening()
        {
            if (_listeningSlot < 0) return;
            if (_slotBgs[_listeningSlot] != null) _slotBgs[_listeningSlot].color = NormalBg;
            // The slot text still shows the "…" placeholder; invalidate the cache so the
            // next OnUpdate refresh pass rewrites it from the actual value.
            _lastShown[_listeningSlot] = UnknownKey;
            _listeningSlot = -1;
            if (s_listening == this) s_listening = null;
        }

        private void ClearSlot(int slot)
        {
            if (_listeningSlot == slot) StopListening();
            _set(slot, KeyCode.None);
            _lastShown[slot] = KeyCode.None;
            if (_slotTexts[slot] != null) _slotTexts[slot].text = KeyDisplayName(KeyCode.None);
            if (_clearGos[slot] != null) _clearGos[slot].SetActive(false);
        }

        internal override void OnUpdate()
        {
            if (_listeningSlot < 0)
            {
                for (int i = 0; i < _slotCount; i++)
                {
                    var kc = _get(i);
                    // Only touch the UI when the value actually changed — a TMP text
                    // assignment triggers a mesh rebuild even for an identical string.
                    if (kc == _lastShown[i]) continue;
                    _lastShown[i] = kc;
                    if (_slotTexts[i] != null) _slotTexts[i].text = KeyDisplayName(kc);
                    if (_clearGos[i] != null) _clearGos[i].SetActive(kc != KeyCode.None);
                }
                return;
            }

            if (SliderEntry.AnyInputActive) return;
            if (Input.GetKeyDown(KeyCode.Escape)) // cancel binding
            { ConfigBehaviour.ConsumeEsc(); StopListening(); return; }

            // Swallow the very click that opened listening — otherwise, with
            // AllowMouseButtons on (the default for multi-slot entries), that same
            // Mouse0-down would be captured as the bind in the same frame.
            if (Time.frameCount == _listenStartFrame) return;

            var pressed = KeyCapture.PollDown(k => AllowMouseButtons || !KeyCapture.IsMouse(k));
            if (pressed == KeyCode.None) return;
            int slot = _listeningSlot;
            _set(slot, pressed);
            StopListening();
            _lastShown[slot] = pressed;
            if (_slotTexts[slot] != null) _slotTexts[slot].text = KeyDisplayName(pressed);
            if (_clearGos[slot] != null) _clearGos[slot].SetActive(true);
        }
    }

    // ── Key capture ───────────────────────────────────────────────────────────

    internal static class KeyCapture
    {
        private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        internal static bool IsMouse(KeyCode kc) => kc >= KeyCode.Mouse0 && kc <= KeyCode.Mouse6;

        // First key or mouse button pressed this frame that `accept` allows, or None.
        // Escape is never returned: it cancels listening. Input.anyKeyDown gates the
        // scan, so the 300-odd GetKeyDown calls only run on frames with a press.
        internal static KeyCode PollDown(Func<KeyCode, bool> accept)
        {
            if (!Input.anyKeyDown) return KeyCode.None;
            foreach (var kc in AllKeys)
            {
                if (kc == KeyCode.None || kc == KeyCode.Escape) continue;
                if (accept != null && !accept(kc)) continue;
                if (Input.GetKeyDown(kc)) return kc;
            }
            return KeyCode.None;
        }
    }

    // ── MODS button label ─────────────────────────────────────────────────────
    // The game's own menu buttons are translated by I2 Localization; ours is not a game term
    // (its Localize components are stripped, see InjectModsClone), so it is translated here.

    internal static class ModsLabel
    {
        // Keys are the language names in this game's only I2 source (measured 2026-07-30: 11
        // languages). A language not listed here, for example one the game adds later, or a
        // failed lookup, shows the English label.
        private static readonly Dictionary<string, string> ByLanguage = new Dictionary<string, string>
        {
            ["English"] = "MODS",
            ["French"] = "MODS",
            ["Italian"] = "MOD",
            ["German"] = "MODS",
            ["Spanish"] = "MODS",
            ["Japanese"] = "MOD",
            ["Simplified Chinese"] = "模组",
            ["Ukrainian"] = "МОДИ",
            ["Russian"] = "МОДЫ",
            ["Swedish"] = "MODDAR",
            ["Goblin"] = "GOBBO HACKS", // in the spirit of "Gobbo Tweak Box" (Settings) and "Scarper" (Quit)
        };
        private const string English = "MODS";
        // Only used if the I2 event cannot be subscribed: then the language is re-read this often.
        private const float PollSeconds = 0.5f;

        private static string _language;
        private static string _text = English;
        private static float _nextPoll;
        private static bool _read;

        // I2 fires OnLocalizeEvent at the end of the pass that re-translates every Localize
        // component, i.e. in the same frame the game's own buttons get their new text. Kept in a
        // field so the delegate handed to IL2CPP is not collected.
        private static Il2CppI2.Loc.LocalizationManager.OnLocalizeCallback _callback;
        private static bool _subscribeFailed;

        /// <summary>Called right after the label text changed, to put it on the buttons at once.</summary>
        public static Action Changed;

        public static string Current
        {
            get
            {
                if (!_read || (_callback == null && Time.unscaledTime >= _nextPoll))
                {
                    _nextPoll = Time.unscaledTime + PollSeconds;
                    Reread();
                }
                return _text;
            }
        }

        public static void EnsureSubscribed()
        {
            if (_callback != null || _subscribeFailed) return;
            try
            {
                _callback = (Il2CppI2.Loc.LocalizationManager.OnLocalizeCallback)new Action(OnLocalized);
                Il2CppI2.Loc.LocalizationManager.add_OnLocalizeEvent(_callback);
                Reread();
            }
            catch (Exception ex)
            {
                _callback = null;
                _subscribeFailed = true;
                MelonLogger.Warning($"[POGConfig] Could not follow language changes through I2 ({ex.Message}); checking every {PollSeconds}s instead.");
            }
        }

        private static void OnLocalized()
        {
            try
            {
                if (Reread()) Changed?.Invoke();
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] MODS label update: {ex.Message}"); }
        }

        /// <summary>Reads the current language. True if the label text changed.</summary>
        private static bool Reread()
        {
            _read = true;
            string lang = null;
            try { lang = Il2CppI2.Loc.LocalizationManager.CurrentLanguage; } catch { }
            if (lang == _language) return false;
            _language = lang;
            string text = lang != null && ByLanguage.TryGetValue(lang, out var t) ? t : English;
            MelonLogger.Msg($"[POGConfig] MODS button label for '{lang ?? "?"}': {text}");
            if (text == _text) return false;
            _text = text;
            return true;
        }
    }

    // ── Preferences ───────────────────────────────────────────────────────────
    // MelonPreferences.Save() rewrites the whole preferences file. Changes are marked
    // dirty here and written once input settles (and on panel close / game quit),
    // instead of on every slider tick while dragging.

    internal static class Prefs
    {
        private const float SaveDelaySeconds = 0.5f;
        private static bool _dirty;
        private static float _lastChange;

        // Existing entry if the key is already there (a mod registering a second time),
        // otherwise a new one. MelonLoader's CreateEntry throws on an existing key.
        internal static MelonPreferences_Entry<T> Entry<T>(MelonPreferences_Category cat, string key, T defaultValue)
            => cat.GetEntry<T>(key) ?? cat.CreateEntry(key, defaultValue);

        internal static void MarkDirty()
        {
            _dirty = true;
            _lastChange = Time.unscaledTime;
        }

        internal static void Tick()
        {
            if (_dirty && Time.unscaledTime - _lastChange >= SaveDelaySeconds) Flush();
        }

        internal static void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            try { MelonPreferences.Save(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] MelonPreferences.Save: {ex.Message}"); }
        }
    }

    // ── Registry ──────────────────────────────────────────────────────────────

    internal class ModRecord
    {
        public string Name;
        public List<ConfigEntry> Entries;
        public ModInfo Info;
    }

    public static class POGConfig
    {
        private static readonly List<ModRecord> _mods = new();
        private static int _registryVersion;

        // True while the MOD SETTINGS panel is open. Check this before processing hotkeys.
        // It is the only open/closed flag; POGConfig's own code reads it too.
        public static bool PanelOpen { get; private set; }

        /// <summary>Raised after the MOD SETTINGS panel opens.</summary>
        public static event Action PanelOpened;

        /// <summary>
        /// Raised after the MOD SETTINGS panel closes. A value still being typed has been
        /// applied by then, so this is the place to apply settings that are expensive to
        /// change one at a time.
        /// </summary>
        public static event Action PanelClosed;

        internal static int RegistryVersion => _registryVersion;

        internal static void SetPanelOpen(bool open)
        {
            if (PanelOpen == open) return;
            PanelOpen = open;
            var handlers = open ? PanelOpened : PanelClosed;
            if (handlers == null) return;
            foreach (Action h in handlers.GetInvocationList())
            {
                try { h(); }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[POGConfig] {(open ? nameof(PanelOpened) : nameof(PanelClosed))} handler: {ex.Message}");
                }
            }
        }

        public static void Register(string modName, List<ConfigEntry> entries)
            => Register(modName, entries, null);

        public static void Register(string modName, List<ConfigEntry> entries, ModInfo info)
        {
            if (string.IsNullOrEmpty(modName) || entries == null)
            {
                MelonLogger.Warning("[POGConfig] Register needs a mod name and an entry list; call ignored.");
                return;
            }

            foreach (var e in entries)
            {
                try { e.CaptureDefault(); } // must run BEFORE BindPrefs overwrites with saved values
                catch (Exception ex)
                { MelonLogger.Warning($"[POGConfig] CaptureDefault '{modName}': {ex.Message}"); }
            }

            try
            {
                var cat = MelonPreferences.CreateCategory(modName.Replace(" ", "_"));
                foreach (var e in entries)
                {
                    try { e.BindPrefs(cat); }
                    catch (Exception ex)
                    { MelonLogger.Warning($"[POGConfig] BindPrefs '{modName}': {ex.Message}"); }
                }
            }
            catch (Exception ex)
            { MelonLogger.Warning($"[POGConfig] Prefs skipped for '{modName}': {ex.Message}"); }

            var rec = new ModRecord { Name = modName, Entries = entries, Info = info };
            int existing = _mods.FindIndex(m => m.Name == modName);
            if (existing >= 0)
            {
                // Registering the same name again replaces the earlier page instead of
                // adding a duplicate one (e.g. a mod that re-registers after a reload).
                foreach (var old in _mods[existing].Entries)
                {
                    try { old.EndInteraction(); }
                    catch (Exception ex)
                    { MelonLogger.Warning($"[POGConfig] EndInteraction '{old.Label}': {ex.Message}"); }
                }
                _mods[existing] = rec;
                MelonLogger.Msg($"[POGConfig] Re-registered: {modName} ({entries.Count} entries, replaces the earlier registration)");
            }
            else
            {
                _mods.Add(rec);
                MelonLogger.Msg($"[POGConfig] Registered: {modName} ({entries.Count} entries)");
            }
            _registryVersion++;
        }

        internal static IReadOnlyList<ModRecord> GetAllRecords() => _mods;
    }

    // ── Vanilla binds (live mirror, editable) ────────────────────────────────
    // POGConfig owns none of this data — Rewired does. Rows are read fresh from the live
    // keyboard/mouse ActionElementMaps of player 0 every time the Vanilla tab is opened,
    // and edits are written straight back through the same Rewired API the game's own
    // Controls screen uses (Replace/Create/DeleteElementMap + userDataStore.Save), then
    // VERIFIED by re-reading the maps. Single source of truth — nothing to synchronize.
    internal static class VanillaBinds
    {
        internal class Slot
        {
            public string Display = "—";
            public int MapId;           // ActionElementMap id; 0 = slot empty
            public ControllerMap Map;   // owning map when bound (null otherwise)
        }

        internal class Row
        {
            public string Action;
            public int ActionId;
            public Pole? PoleFilter;    // null = plain button action; else this axis direction
            public readonly Slot[] Slots = { new Slot(), new Slot(), new Slot() }; // KB1, KB2, Mouse
        }

        private static Player.ControllerHelper.MapHelper _maps;
        private static ControllerMap _kbFallbackMap;    // for creating a bind in an empty slot
        private static ControllerMap _mouseFallbackMap;
        private static Controller _mouse;

        // NOTE ON IL2CPP COLLECTIONS: interop IEnumerable<T>/IEnumerator<T> proxies are
        // NOT foreach-able and do NOT implement the BCL System.Collections interfaces
        // (a cast to System.Collections.IEnumerable silently fails). The reliable shapes
        // are: concrete Il2CppSystem...List<T> (has Count + indexer) — Rewired offers
        // "fill a List, return count" overloads for exactly this — and IList<T>/
        // ICollection<T> interface proxies reached via TryCast<>().

        // Returns null if Rewired isn't ready / the read failed for any reason — the
        // caller shows an "unavailable" message instead of a broken or empty table.
        internal static List<Row> TryRead()
        {
            try
            {
                var player = ReInput.players.GetPlayer(0);
                if (player == null || player.controllers == null) return null;
                _maps = player.controllers.maps;
                if (_maps == null) return null;

                _mouse = player.controllers.hasMouse ? (Controller)player.controllers.Mouse : null;
                _kbFallbackMap = FirstMap(ControllerType.Keyboard);
                _mouseFallbackMap = FirstMap(ControllerType.Mouse);

                var actionsIl = ReInput.mapping.Actions;
                if (actionsIl == null) return null;
                var actionsColl = actionsIl.TryCast<Il2CppSystem.Collections.Generic.ICollection<InputAction>>();
                if (actionsColl == null) return null;

                var actions = new List<InputAction>();
                int total = actionsColl.Count;
                for (int i = 0; i < total; i++)
                {
                    var a = actionsIl[i];
                    if (a != null && a.userAssignable) actions.Add(a);
                }
                // categoryId then id ≈ authoring order — a closer match to the vanilla
                // Controls screen's grouping than an alphabetical action-name sort.
                actions.Sort((a, b) => a.categoryId != b.categoryId
                    ? a.categoryId.CompareTo(b.categoryId) : a.id.CompareTo(b.id));

                var rows = new List<Row>(actions.Count);
                foreach (var action in actions)
                {
                    if (action.type == InputActionType.Button)
                    {
                        rows.Add(new Row
                        {
                            ActionId = action.id,
                            Action = FirstNonEmpty(action.descriptiveName, action.name),
                            PoleFilter = null,
                        });
                    }
                    else
                    {
                        // Axis action (e.g. movement): the vanilla Controls screen shows its
                        // positive and negative directions as two separate rows.
                        rows.Add(new Row
                        {
                            ActionId = action.id,
                            Action = FirstNonEmpty(action.positiveDescriptiveName, action.descriptiveName + " +"),
                            PoleFilter = Pole.Positive,
                        });
                        rows.Add(new Row
                        {
                            ActionId = action.id,
                            Action = FirstNonEmpty(action.negativeDescriptiveName, action.descriptiveName + " -"),
                            PoleFilter = Pole.Negative,
                        });
                    }
                }
                foreach (var row in rows) FillSlots(row);
                return rows;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] VanillaBinds.TryRead: {ex.Message}");
                return null;
            }
        }

        private static string FirstNonEmpty(string a, string b) => !string.IsNullOrEmpty(a) ? a : b;

        private static ControllerMap FirstMap(ControllerType type)
        {
            var buf = new Il2CppSystem.Collections.Generic.List<ControllerMap>();
            _maps.GetAllMaps(type, buf);
            return buf.Count > 0 ? buf[0] : null;
        }

        // Re-reads the row's three slots from the live Rewired maps.
        internal static void FillSlots(Row row)
        {
            foreach (var s in row.Slots) { s.Display = "—"; s.MapId = 0; s.Map = null; }
            if (_maps == null) return;

            var buf = new Il2CppSystem.Collections.Generic.List<ActionElementMap>();
            _maps.GetElementMapsWithAction(ControllerType.Keyboard, row.ActionId, true, buf);
            int slot = 0;
            for (int i = 0; i < buf.Count && slot < 2; i++)
            {
                var em = buf[i];
                if (em == null) continue;
                if (row.PoleFilter.HasValue && em.axisContribution != row.PoleFilter.Value) continue;
                row.Slots[slot].Display = em.elementIdentifierName;
                row.Slots[slot].MapId = em.id;
                row.Slots[slot].Map = em.controllerMap;
                slot++;
            }

            buf = new Il2CppSystem.Collections.Generic.List<ActionElementMap>();
            _maps.GetElementMapsWithAction(ControllerType.Mouse, row.ActionId, true, buf);
            for (int i = 0; i < buf.Count; i++)
            {
                var em = buf[i];
                if (em == null) continue;
                if (row.PoleFilter.HasValue && em.axisContribution != row.PoleFilter.Value) continue;
                row.Slots[2].Display = em.elementIdentifierName;
                row.Slots[2].MapId = em.id;
                row.Slots[2].Map = em.controllerMap;
                break;
            }
        }

        // Writes a bind into the given slot (0/1 = keyboard, 2 = mouse) and verifies it
        // landed by re-reading the maps. Returns true only when the re-read confirms the
        // key is actually bound; the row's slots are refreshed either way, so the UI
        // always ends up showing the real state.
        internal static bool TryWrite(Row row, int slotIdx, KeyCode kc)
        {
            try
            {
                if (_maps == null) return false;
                Pole pole = row.PoleFilter ?? Pole.Positive;
                var slot = row.Slots[slotIdx];
                bool ok;
                int mouseEid = -1;

                if (slotIdx < 2)
                {
                    var map = slot.Map != null ? slot.Map : _kbFallbackMap;
                    if (map == null) return false;
                    ok = slot.MapId != 0
                        ? map.ReplaceElementMap(slot.MapId, row.ActionId, pole, kc, ModifierKeyFlags.None)
                        : map.CreateElementMap(row.ActionId, pole, kc, ModifierKeyFlags.None);
                }
                else
                {
                    if (kc < KeyCode.Mouse0 || kc > KeyCode.Mouse6) return false;
                    mouseEid = MouseButtonElementId(kc - KeyCode.Mouse0);
                    if (mouseEid < 0) return false;
                    var map = slot.Map != null ? slot.Map : _mouseFallbackMap;
                    if (map == null) return false;
                    ok = slot.MapId != 0
                        ? map.ReplaceElementMap(slot.MapId, row.ActionId, pole,
                              mouseEid, ControllerElementType.Button, AxisRange.Full, false)
                        : map.CreateElementMap(row.ActionId, pole,
                              mouseEid, ControllerElementType.Button, AxisRange.Full, false);
                }

                if (ok) SaveUserData();
                FillSlots(row);
                if (!ok)
                {
                    MelonLogger.Warning($"[POGConfig] Vanilla rebind '{row.Action}' → {kc}: Rewired rejected the assignment.");
                    return false;
                }
                bool verified = VerifyBound(row, slotIdx, kc, mouseEid);
                if (!verified)
                    MelonLogger.Warning($"[POGConfig] Vanilla rebind '{row.Action}' → {kc}: write reported OK but re-read does not show it.");
                return verified;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] VanillaBinds.TryWrite: {ex.Message}");
                try { FillSlots(row); } catch { }
                return false;
            }
        }

        internal static bool TryClear(Row row, int slotIdx)
        {
            try
            {
                var slot = row.Slots[slotIdx];
                if (slot.MapId == 0 || slot.Map == null) return true;
                bool ok = slot.Map.DeleteElementMap(slot.MapId);
                if (ok) SaveUserData();
                FillSlots(row);
                if (!ok) MelonLogger.Warning($"[POGConfig] Vanilla unbind '{row.Action}': delete failed.");
                return ok;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] VanillaBinds.TryClear: {ex.Message}");
                return false;
            }
        }

        private static bool VerifyBound(Row row, int slotIdx, KeyCode kc, int mouseEid)
        {
            var buf = new Il2CppSystem.Collections.Generic.List<ActionElementMap>();
            _maps.GetElementMapsWithAction(slotIdx < 2 ? ControllerType.Keyboard : ControllerType.Mouse,
                                           row.ActionId, true, buf);
            for (int i = 0; i < buf.Count; i++)
            {
                var em = buf[i];
                if (em == null) continue;
                if (row.PoleFilter.HasValue && em.axisContribution != row.PoleFilter.Value) continue;
                if (slotIdx < 2) { if (em.keyCode == kc) return true; }
                else { if (em.elementIdentifierId == mouseEid) return true; }
            }
            return false;
        }

        // Maps KeyCode.Mouse<N> button index → Rewired mouse element identifier id via the
        // live Mouse controller (no hardcoded ids — layouts differ between Rewired versions).
        private static int MouseButtonElementId(int buttonIndex)
        {
            if (_mouse == null) return -1;
            var ids = _mouse.ButtonElementIdentifiers;
            if (ids == null) return -1;
            var coll = ids.TryCast<Il2CppSystem.Collections.Generic.ICollection<ControllerElementIdentifier>>();
            if (coll == null || buttonIndex < 0 || buttonIndex >= coll.Count) return -1;
            var cei = ids[buttonIndex];
            return cei != null ? cei.id : -1;
        }

        // Restore the game's default keyboard + mouse maps (same effect as the vanilla
        // Controls screen's "Restore Defaults"), then persist. Requires a prior TryRead
        // to have populated _maps (the button only exists on the read Vanilla tab).
        internal static bool RestoreDefaults()
        {
            try
            {
                if (_maps == null) return false;
                _maps.LoadDefaultMaps(ControllerType.Keyboard);
                _maps.LoadDefaultMaps(ControllerType.Mouse);
                SaveUserData();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] VanillaBinds.RestoreDefaults: {ex.Message}");
                return false;
            }
        }

        private static void SaveUserData()
        {
            try
            {
                var uds = ReInput.userDataStore;
                if (uds != null) uds.Save();
            }
            catch (Exception ex)
            { MelonLogger.Warning($"[POGConfig] userDataStore.Save: {ex.Message}"); }
        }
    }

    // ── Harmony patch ─────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(NetworkMenu), "TogglePauseMenu")]
    class PatchTogglePauseMenu
    {
        static bool Prefix()
        {
            if (POGConfig.PanelOpen)
            {
                ConfigBehaviour.RequestClose();
                return false;
            }
            // Same-frame race: ConfigBehaviour.Update may have already closed the panel
            // with THIS Esc press before Rewired fires TogglePauseMenu — without this
            // guard the same press would then also close the pause menu itself.
            if (ConfigBehaviour.LastCloseFrame == Time.frameCount) return false;
            return true;
        }
    }

    // ── Native menu state ───────────────────────────────────────────────────
    // A real NetworkMenuState the game switches to via NetworkMenu.SetState when
    // the MOD SETTINGS panel opens. Being the CurrentState means the game hides
    // the underlying menu through its own OnExitState and treats us as a first-class
    // screen. We override ShowElements and OnCancelButton so this panel behaves like
    // a native menu without depending on removed internal fields from older versions.

    public class PogConfigState : NetworkMenuState
    {
        public PogConfigState(IntPtr ptr) : base(ptr) { }

        public override void ShowElements(bool show)
        {
            // Do NOT call base: the base ShowElements walks m_hideOnState, a field that
            // belongs to the generic NetworkMenuState<T> subclass and isn't on our type.
            try { ConfigBehaviour.OnStateShowElements(show); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] state.ShowElements: {ex.Message}"); }
        }

        public override void OnCancelButton()
        {
            try { ConfigBehaviour.OnGameCancel(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] state.OnCancelButton: {ex.Message}"); }
        }
    }

    // ── Mod entry ─────────────────────────────────────────────────────────────

    public class PogConfigMelon : MelonMod
    {
        public const string VERSION = "1.0.13";

        public override void OnInitializeMelon()
        {
            try { HarmonyInstance.PatchAll(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] Harmony patch failed: {ex.Message}"); }

            ClassInjector.RegisterTypeInIl2Cpp<ConfigBehaviour>();
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<PogConfigState>();
                ConfigBehaviour.StateTypeRegistered = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] Native state type registration failed, " +
                                    $"using overlay fallback: {ex.Message}");
            }

            Theme.Load();

            var runner = new GameObject("POGConfigRunner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.hideFlags = HideFlags.HideAndDontSave;
            runner.AddComponent<ConfigBehaviour>();
            MelonLogger.Msg($"POG Config {VERSION} loaded.");
        }

        // Write a setting changed in the last moments before quitting.
        public override void OnApplicationQuit() => Prefs.Flush();
    }

    // ── ConfigBehaviour ───────────────────────────────────────────────────────

    public class ConfigBehaviour : MonoBehaviour
    {
        public ConfigBehaviour(IntPtr ptr) : base(ptr) { }

        internal static void RequestClose() => _instance?.DoClose();
        private static ConfigBehaviour _instance;

        // Frame on which the panel was last closed — lets the TogglePauseMenu patch
        // swallow the same Esc press that closed the panel (see PatchTogglePauseMenu).
        internal static int LastCloseFrame = -1;

        // Native menu-state integration (Phase A). Set true once the injected
        // PogConfigState type registers; if registration failed we fall back to the
        // plain overlay show/hide with no state transition.
        internal static bool StateTypeRegistered;
        private PogConfigState  _cfgState;
        private NetworkMenuState _prevState;   // state to return to on close
        private bool _usingNativeState;        // true while open via SetState
        private bool _nativeInitTried, _nativeOk;

        internal static void OnStateShowElements(bool show) => _instance?.SetOverlayVisible(show);

        // Read by SliderEntry.AnyInputActive / KeyEntry.AnyWaiting, which are derived from
        // the actual edit and listen state rather than stored separately.
        internal static bool SearchEditing => _instance != null && _instance._editingSearch;

        // The list a ChoiceRow opens. While it is open it takes all input (clicks, wheel, keys).
        private ChoicePopup _popup;
        internal static ChoicePopup Popup => _instance != null ? _instance._popup : null;
        private static bool PopupOpen => Popup != null && Popup.IsOpen;
        internal static bool VanillaListening => _instance != null && _instance._vListenSlot >= 0;

        // Any handler that reacts to Escape (cancel search edit, value edit, key binding)
        // calls ConsumeEsc() so the same press doesn't ALSO close the panel. Stamped with
        // the frame number rather than reset each Update, so the answer is right no matter
        // whether the game's cancel handling runs before or after our Update.
        private static int _escConsumedFrame = -1;
        internal static bool EscConsumed => _escConsumedFrame == Time.frameCount;
        internal static void ConsumeEsc() => _escConsumedFrame = Time.frameCount;

        // The game's own cancel input (Esc, gamepad B) reaches the panel through
        // PogConfigState.OnCancelButton. That press must not close the panel while it is
        // cancelling something inside it: a key listen, a value edit or the search box.
        internal static void OnGameCancel()
        {
            if (EscConsumed || KeyEntry.AnyWaiting || SliderEntry.AnyInputActive || PopupOpen) return;
            RequestClose();
        }

        // Canvas units; Theme.Scale scales the whole canvas on top of these.
        private const float PANEL_W    = 1040f;
        private const float PANEL_H    = 650f;
        private const float TOP_H      = 68f;
        private const float BOT_H      = 24f;
        private const float SB_W       = 8f;
        private const float SIDEBAR_W  = 220f;
        private const float SIDEBAR_PAD= 8f;
        private const float SEARCH_H   = 32f;
        private const float MOD_HDR_H  = 56f; // mod info header in content area: its smallest height
        private const float SIDE_TOP   = TOP_H + 4f; // sidebar and separator start below the title bar
        private const float SIDEBAR_LIST_H = PANEL_H - SIDE_TOP - BOT_H - SEARCH_H - SIDEBAR_PAD;
        // The mod header grows with a wrapped description, and the rows' viewport shrinks with it.
        private float _modHdrH = MOD_HDR_H;
        private float ViewportH => PANEL_H - TOP_H - BOT_H - _modHdrH;
        private const float SCROLL_SPD = 40f;

        private bool _isPause, _isMainMenu;
        private bool _updateErrLogged;

        // ── Floating MODS button (pause-menu FALLBACK if native injection fails) ─
        private RectTransform _modsBtnRt;
        private TMP_Text _modsBtnText;
        private RectTransform _doneBtnRt;
        private GameObject    _overlay;
        private Image         _modsBtnImg;

        // ── Injected NetworkMenuButton clones (main menu + pause menu) ────────
        private GameObject _mainMenuModsBtn;
        private GameObject _pauseModsBtn;

        // ── Content area (right) ─────────────────────────────────────────────
        private Transform     _scrollContent;
        private RectTransform _scrollContentRt;
        private RectTransform _scrollTrackRt;
        private RectTransform _scrollThumbRt;
        private GameObject    _scrollbarGo;
        private RectTransform _contentVpRt;   // RectMask2D viewport around _scrollContent
        private RectTransform _sidebarVpRt;   // RectMask2D viewport around _sidebarList

        private float _totalContentH;
        private float _scrollOffset;
        private bool  _uiReady, _contentBuilt;
        private int _builtRegistryVersion = -1;
        private TMP_FontAsset _font;

        // ── Sidebar / mod selection / search ─────────────────────────────────
        private Transform     _sidebarList;
        private RectTransform _sidebarListRt;
        private RectTransform _sidebarTrackRt;
        private RectTransform _sidebarThumbRt;
        private GameObject    _sidebarScrollbarGo;
        private float _sidebarTotalH;
        private float _sidebarOffset;

        private RectTransform _searchBgRt;
        private Image         _searchBgImg;
        private TextMeshProUGUI _searchText;
        private string _searchQuery = "";
        private bool _editingSearch;

        private RectTransform _contentHeaderRt;
        private TextMeshProUGUI _modTitleTmp;
        private TextMeshProUGUI _modSubTmp;
        private TextMeshProUGUI _modDescTmp;

        private int _selectedMod = 0;
        private readonly Dictionary<int, RectTransform> _sidebarItemRects = new();
        private readonly Dictionary<int, Image> _sidebarItemBgs = new();

        // ── Vanilla tab interactive state ─────────────────────────────────────
        private class VanillaRowUI
        {
            public VanillaBinds.Row Data;
            public readonly TextMeshProUGUI[] Texts = new TextMeshProUGUI[3];
            public readonly Image[] Bgs = new Image[3];
            public readonly GameObject[] Clears = new GameObject[3];
        }
        private readonly List<VanillaRowUI> _vanillaRows = new();
        private VanillaRowUI _vListenRow;
        private int _vListenSlot = -1;
        private int _vListenFrame;
        private static readonly Color VNormalBg = new Color(0.14f, 0.15f, 0.19f);
        private static readonly Color VListenBg = new Color(0.10f, 0.20f, 0.32f);

        // ── Thumb drag state ─────────────────────────────────────────────────
        private bool  _scrollDragging;
        private float _scrollDragGrab;   // mouseY - thumbTopY at drag start
        private bool  _sidebarDragging;
        private float _sidebarDragGrab;

        // EventSystem selection saved when panel opens, restored on close —
        // we deselect everything while the panel is open so Enter/gamepad-Submit
        // can't trigger START/SETTINGS/etc. through Button.OnSubmit.
        private GameObject _savedSelected;

        void Awake() { _instance = this; }
        void Start()  { BuildStaticUI(); }

        // ── HitTest ───────────────────────────────────────────────────────────

        internal static bool HitTest(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null) return false;
            try
            {
                return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);
            }
            catch
            {
                try
                {
                    var c = GetCorners(rt);
                    float cx = screenPoint.x - Screen.width  * 0.5f;
                    float cy = screenPoint.y - Screen.height * 0.5f;
                    return cx >= c[0].x && cx <= c[2].x && cy >= c[0].y && cy <= c[2].y;
                }
                catch { return false; }
            }
        }

        // ── Update ────────────────────────────────────────────────────────────

        void Update()
        {
            if (!_uiReady) BuildStaticUI();
            try
            {
                Prefs.Tick();
                var nm = NetworkMenu.Instance;
                var curState = (nm != null) ? nm.CurrentState : null;
                string state = (curState != null) ? curState.GetIl2CppType().Name : "";
                _isPause    = state == "NetworkMenuPauseState";
                _isMainMenu = state == "NetworkMenuMainState";
                bool isOurState = _usingNativeState && _cfgState != null && curState != null
                                  && curState.Pointer == _cfgState.Pointer;

                // Main menu — inject a real NetworkMenuButton into the game's Buttons Panel.
                // Hover/selection visuals come natively from NetworkMenuButton
                // (OnPointerEnter → SelectButton → m_menu.SetSelected → OnSelect).
                if (_isMainMenu && !POGConfig.PanelOpen)
                {
                    EnsureMainMenuModsBtn();
                    EnforceModsLabel(_mainMenuModsBtn);
                }

                // Pause menu — same native injection as the main menu.
                if (_isPause && !POGConfig.PanelOpen)
                {
                    EnsurePauseModsBtn();
                    EnforceModsLabel(_pauseModsBtn);
                }

                // Floating MODS button — pause-menu FALLBACK, only if native injection
                // didn't take (template button not found this frame).
                bool showFloating = _isPause && !POGConfig.PanelOpen && _pauseModsBtn == null;
                if (_modsBtnRt != null)
                {
                    _modsBtnRt.gameObject.SetActive(showFloating);
                    if (showFloating)
                    {
                        PositionModsButton();
                        UpdateModsButtonHover();
                        if (_modsBtnText != null && _modsBtnText.text != ModsLabel.Current) _modsBtnText.text = ModsLabel.Current;
                    }
                    else if (_modsBtnImg != null) _modsBtnImg.color = new Color(0, 0, 0, 0);
                }

                // DIAG: Shift+M — log who's under the cursor so we can debug missed clicks.
                if (_isMainMenu && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.M))
                    DiagnoseAtCursor();

                // DIAG: Shift+P — dump the pause-menu button container (layout, spacing, children).
                if (_isPause && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.P))
                    DiagnosePauseMenu();

                // Our own state counts as "in menu"; without this the guard below would
                // close the panel every frame while the native state is active.
                bool inMenu = _isPause || _isMainMenu || isOurState;
                if (!inMenu && POGConfig.PanelOpen) DoClose();

                bool popupOpen = PopupOpen;
                if (!popupOpen) HandleClicks();

                if (POGConfig.PanelOpen)
                {
                    // Keep the underlying menu deselected — game's Update may try to re-select
                    // a button; if so, Enter would submit it. Force null every frame.
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null && es.currentSelectedGameObject != null)
                        es.SetSelectedGameObject(null);

                    if (popupOpen) Popup.Tick();
                    else
                    {
                        UpdateThumbDrag();
                        HandleScroll();
                        HandleSearchInput();
                        UpdateVanillaListen();
                    }
                    // Only the page on screen has rows to sync. Pages switched away from
                    // had their interactions ended when the content was rebuilt.
                    var mods = POGConfig.GetAllRecords();
                    if (_selectedMod >= 0 && _selectedMod < mods.Count)
                        foreach (var e in mods[_selectedMod].Entries)
                            e.OnUpdate();

                    // Esc closes the panel — but only if no handler above already used
                    // this press to cancel an edit/binding (EscConsumed).
                    if (!popupOpen && !_editingSearch && !KeyEntry.AnyWaiting && !EscConsumed
                        && Input.GetKeyDown(KeyCode.Escape))
                        DoClose();
                }
                else if (_scrollDragging || _sidebarDragging)
                {
                    _scrollDragging = false; _sidebarDragging = false;
                }
            }
            catch (Exception ex)
            {
                if (!_updateErrLogged)
                {
                    _updateErrLogged = true;
                    MelonLogger.Warning($"[POGConfig] Update error: {ex}");
                }
            }
        }

        // ── Scroll ────────────────────────────────────────────────────────────

        private void HandleScroll()
        {
            float wheel = Input.mouseScrollDelta.y;
            Vector2 mp = Input.mousePosition;

            float contentMax = Mathf.Max(0, _totalContentH - ViewportH);
            float sidebarMax = Mathf.Max(0, _sidebarTotalH - SIDEBAR_LIST_H);

            // Strict cursor-over-section gating so wheel scrolls only the panel under the cursor.
            bool overSidebar = HitTest(_sidebarVpRt, mp);
            bool overContent = HitTest(_contentVpRt, mp);

            if (Mathf.Abs(wheel) > 0.001f)
            {
                if (overSidebar && sidebarMax > 0)
                {
                    _sidebarOffset = Mathf.Clamp(_sidebarOffset - wheel * SCROLL_SPD, 0, sidebarMax);
                    ApplySidebarScroll(sidebarMax);
                }
                else if (overContent && contentMax > 0)
                {
                    _scrollOffset = Mathf.Clamp(_scrollOffset - wheel * SCROLL_SPD, 0, contentMax);
                    ApplyScroll(contentMax);
                }
            }
            else
            {
                if (contentMax > 0) UpdateScrollThumb(contentMax);
                if (sidebarMax > 0) UpdateSidebarThumb(sidebarMax);
            }
        }

        private void ApplySidebarScroll(float maxScroll)
        {
            _sidebarOffset = Mathf.Clamp(_sidebarOffset, 0, maxScroll);
            if (_sidebarListRt != null)
                _sidebarListRt.anchoredPosition = new Vector2(0, _sidebarOffset);
            UpdateSidebarThumb(maxScroll);
        }

        private void UpdateSidebarThumb(float maxScroll)
        {
            if (_sidebarThumbRt == null || _sidebarTotalH <= 0) return;
            float ratio = maxScroll > 0 ? _sidebarOffset / maxScroll : 0f;
            float thumbH = Mathf.Max(20f, SIDEBAR_LIST_H * SIDEBAR_LIST_H / _sidebarTotalH);
            float thumbY = -ratio * (SIDEBAR_LIST_H - thumbH);
            _sidebarThumbRt.sizeDelta = new Vector2(0, thumbH);
            _sidebarThumbRt.anchoredPosition = new Vector2(0, thumbY);
        }

        private void ApplyScroll(float maxScroll)
        {
            _scrollOffset = Mathf.Clamp(_scrollOffset, 0, maxScroll);
            if (_scrollContentRt != null)
                _scrollContentRt.anchoredPosition = new Vector2(0, _scrollOffset);
            UpdateScrollThumb(maxScroll);
        }

        private void UpdateScrollThumb(float maxScroll)
        {
            if (_scrollThumbRt == null || _totalContentH <= 0) return;
            float ratio   = maxScroll > 0 ? _scrollOffset / maxScroll : 0f;
            float thumbH  = Mathf.Max(30f, ViewportH * ViewportH / _totalContentH);
            float thumbY  = -ratio * (ViewportH - thumbH);
            _scrollThumbRt.sizeDelta        = new Vector2(0, thumbH);
            _scrollThumbRt.anchoredPosition = new Vector2(0, thumbY);
        }

        // ── Clicks ────────────────────────────────────────────────────────────

        private void HandleClicks()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            Vector2 mp = Input.mousePosition;

            // Floating MODS button (pause only) — only when panel is closed
            if (!POGConfig.PanelOpen && _modsBtnRt != null && _modsBtnRt.gameObject.activeSelf && HitTest(_modsBtnRt, mp))
            {
                OpenPanel();
                return;
            }

            if (!POGConfig.PanelOpen) return;

            // Thumb hit — start a drag (handled per-frame by UpdateThumbDrag).
            // Check thumbs BEFORE the track-jump branch since thumb is a child of track.
            if (_scrollThumbRt != null && _scrollbarGo != null && _scrollbarGo.activeInHierarchy
                && HitTest(_scrollThumbRt, mp))
            {
                StartScrollThumbDrag(mp);
                return;
            }
            if (_sidebarThumbRt != null && _sidebarScrollbarGo != null && _sidebarScrollbarGo.activeInHierarchy
                && HitTest(_sidebarThumbRt, mp))
            {
                StartSidebarThumbDrag(mp);
                return;
            }

            bool insideSearch = _searchBgRt != null && HitTest(_searchBgRt, mp);

            // If we were editing search and click is OUTSIDE — commit first, then process click normally.
            if (_editingSearch && !insideSearch) CommitSearchEdit();

            // Click on search box → enter edit mode
            if (insideSearch)
            {
                if (!_editingSearch)
                {
                    _editingSearch = true;
                    if (_searchBgImg != null) _searchBgImg.color = new Color(0.28f, 0.22f, 0.08f);
                    UpdateSearchTextDisplay();
                }
                return;
            }

            // Scrollbar track click → centre thumb on click position, then start a drag
            // so the user can keep dragging from where they clicked (typical scrollbar UX).
            if (_scrollTrackRt != null && _scrollbarGo != null && _scrollbarGo.activeInHierarchy
                && HitTest(_scrollTrackRt, mp))
            {
                JumpScrollToCursor(mp);
                StartScrollThumbDrag(mp);
                return;
            }
            if (_sidebarTrackRt != null && _sidebarScrollbarGo != null && _sidebarScrollbarGo.activeInHierarchy
                && HitTest(_sidebarTrackRt, mp))
            {
                JumpSidebarToCursor(mp);
                StartSidebarThumbDrag(mp);
                return;
            }

            // Registered buttons (DONE, sidebar items, KeyEntry slots, SliderEntry value boxes, etc.)
            // Rows live inside RectMask2D viewports. A row scrolled out of view is only hidden;
            // its rect is still there (e.g. under the mod title), so a row counts as hit only
            // when the click is also inside that row's viewport.
            bool inContentVp = HitTest(_contentVpRt, mp);
            bool inSidebarVp = HitTest(_sidebarVpRt, mp);
            foreach (var (rt, cb) in Clicks.Map)
            {
                if (rt == null || !HitTest(rt, mp)) continue;
                if (!inContentVp && _scrollContent != null && rt.IsChildOf(_scrollContent)) continue;
                if (!inSidebarVp && _sidebarList != null && rt.IsChildOf(_sidebarList)) continue;
                cb?.Invoke();
                return;
            }
        }

        // ── Thumb drag ────────────────────────────────────────────────────────
        // Scrollbar thumbs are children of their tracks, so HandleClicks tests them
        // before the track-jump branch. While the mouse button is held the thumb
        // follows the cursor; on release the drag stops.

        private void StartScrollThumbDrag(Vector2 mp)
        {
            if (_scrollThumbRt == null) return;
            var c = GetCorners(_scrollThumbRt);
            float thumbTopY = c[1].y;
            _scrollDragGrab = mp.y - thumbTopY;
            _scrollDragging = true;
        }

        private void StartSidebarThumbDrag(Vector2 mp)
        {
            if (_sidebarThumbRt == null) return;
            var c = GetCorners(_sidebarThumbRt);
            float thumbTopY = c[1].y;
            _sidebarDragGrab = mp.y - thumbTopY;
            _sidebarDragging = true;
        }

        // ── World corners helper (Il2Cpp) ─────────────────────────────────────
        // RectTransform.GetWorldCorners(Vector3[]) silently fails in Il2Cpp interop:
        // the implicit conversion creates a *new* Il2CppStructArray<Vector3>, the native
        // call writes into THAT, and our managed Vector3[] stays all-zero. Pass an
        // Il2CppStructArray directly so we can read the values back.
        private static Il2CppStructArray<Vector3> GetCorners(RectTransform rt)
        {
            var c = new Il2CppStructArray<Vector3>(4);
            rt.GetWorldCorners(c);
            return c;
        }

        // ScreenPointHeight: returns the height of an RT in screen-pixel space (not canvas units).
        private static float ScreenPointHeight(RectTransform rt)
        {
            var c = GetCorners(rt);
            return c[1].y - c[0].y;
        }

        // Click on empty track area: snap thumb so its centre lands at the click,
        // then we'll start a drag from that point.
        private void JumpScrollToCursor(Vector2 mp)
        {
            if (_scrollTrackRt == null || _scrollThumbRt == null) return;
            float maxScroll = Mathf.Max(0, _totalContentH - ViewportH);
            if (maxScroll <= 0) return;
            var tc = GetCorners(_scrollTrackRt);
            float trackTop = tc[1].y;
            float trackBot = tc[0].y;
            float trackH   = trackTop - trackBot;
            float thumbH   = ScreenPointHeight(_scrollThumbRt);
            float usable   = trackH - thumbH;
            if (usable <= 0) return;
            float desiredThumbTop = mp.y + thumbH * 0.5f; // centre on click
            float dropDown = trackTop - desiredThumbTop;
            float ratio    = Mathf.Clamp01(dropDown / usable);
            _scrollOffset  = ratio * maxScroll;
            ApplyScroll(maxScroll);
        }

        private void JumpSidebarToCursor(Vector2 mp)
        {
            if (_sidebarTrackRt == null || _sidebarThumbRt == null) return;
            float maxScroll = Mathf.Max(0, _sidebarTotalH - SIDEBAR_LIST_H);
            if (maxScroll <= 0) return;
            var tc = GetCorners(_sidebarTrackRt);
            float trackTop = tc[1].y;
            float trackBot = tc[0].y;
            float trackH   = trackTop - trackBot;
            float thumbH   = ScreenPointHeight(_sidebarThumbRt);
            float usable   = trackH - thumbH;
            if (usable <= 0) return;
            float desiredThumbTop = mp.y + thumbH * 0.5f;
            float dropDown = trackTop - desiredThumbTop;
            float ratio    = Mathf.Clamp01(dropDown / usable);
            _sidebarOffset = ratio * maxScroll;
            ApplySidebarScroll(maxScroll);
        }

        private void UpdateThumbDrag()
        {
            // Drag ends on mouse release (regardless of where the cursor is).
            if (Input.GetMouseButtonUp(0))
            {
                _scrollDragging = false;
                _sidebarDragging = false;
                return;
            }

            if (_scrollDragging) DragScrollThumb();
            if (_sidebarDragging) DragSidebarThumb();
        }

        private void DragScrollThumb()
        {
            if (_scrollTrackRt == null || _scrollThumbRt == null) { _scrollDragging = false; return; }
            float maxScroll = Mathf.Max(0, _totalContentH - ViewportH);
            if (maxScroll <= 0) return;
            var tc = GetCorners(_scrollTrackRt);
            float trackTop = tc[1].y;
            float trackBot = tc[0].y;
            float trackH   = trackTop - trackBot;
            float thumbH   = ScreenPointHeight(_scrollThumbRt);
            float usable   = trackH - thumbH;
            if (usable <= 0) return;
            float desiredThumbTop = Input.mousePosition.y - _scrollDragGrab;
            float dropDown = trackTop - desiredThumbTop;
            float ratio    = Mathf.Clamp01(dropDown / usable);
            _scrollOffset  = ratio * maxScroll;
            ApplyScroll(maxScroll);
        }

        private void DragSidebarThumb()
        {
            if (_sidebarTrackRt == null || _sidebarThumbRt == null) { _sidebarDragging = false; return; }
            float maxScroll = Mathf.Max(0, _sidebarTotalH - SIDEBAR_LIST_H);
            if (maxScroll <= 0) return;
            var tc = GetCorners(_sidebarTrackRt);
            float trackTop = tc[1].y;
            float trackBot = tc[0].y;
            float trackH   = trackTop - trackBot;
            float thumbH   = ScreenPointHeight(_sidebarThumbRt);
            float usable   = trackH - thumbH;
            if (usable <= 0) return;
            float desiredThumbTop = Input.mousePosition.y - _sidebarDragGrab;
            float dropDown = trackTop - desiredThumbTop;
            float ratio    = Mathf.Clamp01(dropDown / usable);
            _sidebarOffset = ratio * maxScroll;
            ApplySidebarScroll(maxScroll);
        }

        private void CommitSearchEdit()
        {
            if (!_editingSearch) return;
            _editingSearch = false;
            if (_searchBgImg != null) _searchBgImg.color = new Color(0.16f, 0.16f, 0.18f, 0.95f);
            UpdateSearchTextDisplay();
        }

        private void HandleSearchInput()
        {
            if (!_editingSearch) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ConsumeEsc();
                _editingSearch = false;
                _searchQuery = "";
                if (_searchBgImg != null) _searchBgImg.color = new Color(0.16f, 0.16f, 0.18f, 0.95f);
                UpdateSearchTextDisplay();
                RebuildSidebar();
                return;
            }
            bool changed = false;
            string s = Input.inputString;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\b')
                {
                    if (_searchQuery.Length > 0) { _searchQuery = _searchQuery.Substring(0, _searchQuery.Length - 1); changed = true; }
                }
                else if (c == '\n' || c == '\r') { /* keep editing */ }
                else if (c >= ' ') { _searchQuery += c; changed = true; }
            }
            if (changed) { UpdateSearchTextDisplay(); RebuildSidebar(); }
        }

        private void UpdateSearchTextDisplay()
        {
            if (_searchText == null) return;
            if (_editingSearch)
                _searchText.text = _searchQuery + "|";
            else if (string.IsNullOrEmpty(_searchQuery))
                _searchText.text = "Search…";
            else
                _searchText.text = _searchQuery;
        }

        // ── Hover / Position (floating button — pause menu) ──────────────────

        private void UpdateModsButtonHover()
        {
            if (_modsBtnImg == null || _modsBtnRt == null) return;
            bool over = HitTest(_modsBtnRt, Input.mousePosition);
            _modsBtnImg.color = over ? new Color(1f, 1f, 1f, 0.14f) : new Color(0f, 0f, 0f, 0f);
        }

        private void PositionModsButton()
        {
            // Fallback only: shown when the native pause-menu injection could not find a
            // template button. Both menus normally use an injected NetworkMenuButton.
            _modsBtnRt.anchorMin = _modsBtnRt.anchorMax = new Vector2(0.5f, 0.685f);
            _modsBtnRt.sizeDelta = new Vector2(260, 34);
            _modsBtnRt.anchoredPosition = Vector2.zero;
        }

        // ── NetworkMenuButton injection ───────────────────────────────────────
        // Clones one of the game's own buttons so we get matching style, click
        // sound, hover highlight, EventSystem selection and keyboard/gamepad
        // navigation for free. Used for both the main menu (clone START) and the
        // pause menu (clone SETTINGS/INVITE/RESUME) — the button sits INSIDE the
        // game's real button column, so the layout spaces it correctly and there
        // are no hard-coded overlay coordinates to collide with.

        // Belt and braces on top of stripping the Localize components: if ANY relocalization path
        // we did not anticipate rewrites the label, put it back. Cheap (one string compare per frame,
        // only while the respective menu is on screen).
        // Puts the label on our buttons in the same frame the game relabels its own (I2's
        // OnLocalizeEvent), instead of waiting for the next per-frame check below.
        private void FollowLanguage()
        {
            ModsLabel.Changed = ApplyModsLabel;
            ModsLabel.EnsureSubscribed();
        }

        private void ApplyModsLabel()
        {
            EnforceModsLabel(_mainMenuModsBtn);
            EnforceModsLabel(_pauseModsBtn);
            if (_modsBtnText != null) _modsBtnText.text = ModsLabel.Current;
        }

        private static void EnforceModsLabel(GameObject buttonGo)
        {
            if (buttonGo == null) return;
            var nmb = buttonGo.GetComponent<NetworkMenuButton>();
            var text = nmb != null ? nmb.m_buttonText : null;
            if (text == null) return;
            string want = ModsLabel.Current;
            if (text.text != want) text.text = want;
        }

        private void EnsureMainMenuModsBtn()
        {
            // Unity's overloaded == treats destroyed objects as null — covers scene reloads.
            if (_mainMenuModsBtn != null) return;

            // Handler names first (language-independent); label text as a fallback.
            var template = FindButtonByHandler("OnStartButton") ?? FindButtonByLabel("START");
            if (template == null) return; // menu not fully built yet, retry next frame

            _mainMenuModsBtn = InjectModsClone(template, "POGModsButton", 1); // after START
            if (_mainMenuModsBtn != null)
            {
                MelonLogger.Msg("[POGConfig] MODS button injected into main menu.");
                FollowLanguage();
            }
        }

        private void EnsurePauseModsBtn()
        {
            if (_pauseModsBtn != null) return;

            // Prefer a button that's clearly in the main pause column (not DISCONNECT,
            // which may live in a separate footer container). Main-menu buttons are
            // inactive during pause, so "SETTINGS" resolves to the pause one here.
            var template = FindButtonByHandler("OnSettingsButton", "OnInviteButton", "OnResumeButton")
                           ?? FindButtonByLabel("SETTINGS", "INVITE", "RESUME", "SHOW LOBBY CODE");
            if (template == null) return; // pause column not ready → floating fallback shows

            // Sibling index 0 = top of the column, above SHOW LOBBY CODE/INVITE.
            _pauseModsBtn = InjectModsClone(template, "POGModsButtonPause", 0);
            if (_pauseModsBtn != null)
            {
                FollowLanguage();
                string parent = template.transform.parent != null ? template.transform.parent.name : "?";
                MelonLogger.Msg($"[POGConfig] MODS button injected into pause menu (parent='{parent}').");
            }
        }

        // Clone `template`, relabel to MODS, rewire its click to toggle our panel,
        // place at `siblingIndex` in the template's own parent. Returns the clone
        // (or null on failure so the caller can fall back).
        private GameObject InjectModsClone(NetworkMenuButton template, string cloneName, int siblingIndex)
        {
            try
            {
                // Parent must be set IN the Instantiate call: NetworkMenuButton.Awake runs
                // during Instantiate and resolves m_menu = GetComponentInParent<NetworkMenu>()
                // (m_menu is a private non-serialized field, so it is NOT copied from the
                // template). A parentless clone gets m_menu = null, and then the native
                // hover path (OnPointerEnter → SelectButton → m_menu.SetSelected) throws
                // before enabling the pointer/selection images — no highlight on hover.
                var clone = UnityEngine.Object.Instantiate(
                    template.gameObject, template.transform.parent, false);
                clone.name = cloneName;

                // The donor carries I2 Localize component(s) that re-apply the DONOR's term on every
                // language change — overwriting our "MODS" label with a second localized START/
                // SETTINGS. Our label is not a game term, so strip them from the clone entirely.
                foreach (var loc in clone.GetComponentsInChildren<Il2CppI2.Loc.Localize>(true))
                    if (loc != null) UnityEngine.Object.Destroy(loc);

                var nmb = clone.GetComponent<NetworkMenuButton>();
                if (nmb == null) { UnityEngine.Object.Destroy(clone); return null; }

                // Belt and braces in case Awake ran before the parent chain was active.
                if (nmb.m_menu == null)
                    nmb.m_menu = template.m_menu != null ? template.m_menu : NetworkMenu.Instance;

                if (nmb.m_buttonText != null) nmb.m_buttonText.text = ModsLabel.Current;

                var btn = nmb.m_button;
                if (btn != null)
                {
                    // RemoveAllListeners only clears RUNTIME listeners. The prefab persistent
                    // listener (e.g. OnStartButton / OnSettingsButton) is serialized data and
                    // gets copied into our clone — clicking MODS would still fire it.
                    // Disable each persistent entry explicitly.
                    btn.onClick.RemoveAllListeners();
                    int pcount = btn.onClick.GetPersistentEventCount();
                    for (int i = 0; i < pcount; i++)
                        btn.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

                    Action onClick = () =>
                    {
                        try { if (POGConfig.PanelOpen) DoClose(); else OpenPanel(); }
                        catch (Exception ex)
                        { MelonLogger.Warning($"[POGConfig] MODS click: {ex.Message}"); }
                    };
                    btn.onClick.AddListener((Action)onClick);
                    btn.enabled = true;
                    btn.interactable = true;

                    var nav = btn.navigation;
                    nav.mode = UnityEngine.UI.Navigation.Mode.Automatic;
                    btn.navigation = nav;
                }

                clone.transform.SetSiblingIndex(siblingIndex);

                // Defensive — Instantiate should already preserve raycasts.
                var images = clone.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in images) img.raycastTarget = true;

                clone.SetActive(true);

                // Force layout / canvas rebuild so a LayoutGroup re-spaces the column.
                var parentRt = template.transform.parent as RectTransform;
                if (parentRt != null)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                Canvas.ForceUpdateCanvases();

                return clone;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] InjectModsClone('{cloneName}'): {ex.Message}");
                return null;
            }
        }

        private static NetworkMenuButton FindButtonByLabel(params string[] labels)
        {
            var all = Resources.FindObjectsOfTypeAll<NetworkMenuButton>();
            foreach (var b in all)
            {
                if (b == null) continue;
                if (!b.gameObject.activeInHierarchy) continue;
                if (b.gameObject.name.StartsWith("POGModsButton")) continue; // never match our own clone
                var tmp = b.m_buttonText;
                if (tmp == null) continue;
                string t = tmp.text;
                if (string.IsNullOrEmpty(t)) continue;
                foreach (var lbl in labels)
                    if (t.Equals(lbl, StringComparison.OrdinalIgnoreCase)) return b;
            }
            return null;
        }

        // Language-independent template lookup. Labels change with the game language ("START"
        // becomes its translation), which silently broke re-injection after a language switch —
        // the MODS button vanished and never came back. The persistent onClick handler names
        // (OnStartButton, OnSettingsButton, …) are serialized prefab data and never localized.
        private static NetworkMenuButton FindButtonByHandler(params string[] methodNames)
        {
            var all = Resources.FindObjectsOfTypeAll<NetworkMenuButton>();
            foreach (var b in all)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                // Our clones carry the donor's (disabled) persistent entries with the same method
                // names — skip them or the clone becomes its own template.
                if (b.gameObject.name.StartsWith("POGModsButton")) continue;
                var btn = b.m_button;
                if (btn == null) continue;

                int count;
                try { count = btn.onClick.GetPersistentEventCount(); } catch { continue; }
                for (int i = 0; i < count; i++)
                {
                    string m;
                    try { m = btn.onClick.GetPersistentMethodName(i); } catch { continue; }
                    if (string.IsNullOrEmpty(m)) continue;
                    foreach (var want in methodNames)
                        if (m.Equals(want, StringComparison.OrdinalIgnoreCase)) return b;
                }
            }
            return null;
        }

        // Shift+P in pause: dump the button column so we can see how the game
        // spaces its buttons (VerticalLayoutGroup + spacing/padding vs absolute)
        // and where our injected MODS button landed.
        private static void DiagnosePauseMenu()
        {
            try
            {
                MelonLogger.Msg("[POGConfig] === PAUSE DIAG ===");
                var all = Resources.FindObjectsOfTypeAll<NetworkMenuButton>();
                Transform container = null;
                foreach (var b in all)
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    var rt = b.transform as RectTransform;
                    var tmp = b.m_buttonText;
                    string t = tmp != null ? tmp.text : "?";
                    string parent = b.transform.parent != null ? b.transform.parent.name : "<none>";
                    MelonLogger.Msg($"[POGConfig]   BTN '{t}' go='{b.gameObject.name}' parent='{parent}' " +
                        $"idx={b.transform.GetSiblingIndex()} pos={(rt != null ? rt.anchoredPosition.ToString() : "?")} " +
                        $"size={(rt != null ? rt.sizeDelta.ToString() : "?")}");
                    if (container == null && (t == "SETTINGS" || t == "RESUME" || t == "INVITE"))
                        container = b.transform.parent;
                }

                if (container == null) { MelonLogger.Msg("[POGConfig]   no pause container found"); return; }

                MelonLogger.Msg($"[POGConfig]   CONTAINER '{container.name}' children={container.childCount}");
                foreach (var c in container.gameObject.GetComponents<Component>())
                    if (c != null) MelonLogger.Msg($"[POGConfig]     comp: {c.GetIl2CppType().Name}");

                var vlg = container.gameObject.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                if (vlg != null)
                    MelonLogger.Msg($"[POGConfig]     VLG spacing={vlg.spacing} pad(t/b={vlg.padding.top}/{vlg.padding.bottom}) " +
                        $"align={vlg.childAlignment} ctrlH={vlg.childControlHeight} forceH={vlg.childForceExpandHeight}");

                for (int i = 0; i < container.childCount; i++)
                {
                    var ch = container.GetChild(i);
                    var crt = ch as RectTransform;
                    var nb = ch.GetComponent<NetworkMenuButton>();
                    string lbl = (nb != null && nb.m_buttonText != null) ? nb.m_buttonText.text : "";
                    MelonLogger.Msg($"[POGConfig]     child[{i}] '{ch.name}' lbl='{lbl}' active={ch.gameObject.activeSelf} " +
                        $"pos={(crt != null ? crt.anchoredPosition.ToString() : "?")} size={(crt != null ? crt.sizeDelta.ToString() : "?")}");
                }
            }
            catch (Exception ex)
            { MelonLogger.Warning($"[POGConfig] DiagnosePauseMenu: {ex.Message}"); }
        }

        private static void DiagnoseAtCursor()
        {
            try
            {
                Vector2 mp = Input.mousePosition;
                MelonLogger.Msg($"[POGConfig] === DIAG @ mouse={mp} ===");

                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                {
                    var sel = es.currentSelectedGameObject;
                    MelonLogger.Msg($"[POGConfig]   EventSystem.selected = {(sel != null ? sel.name : "<null>")}");
                }

                var all = Resources.FindObjectsOfTypeAll<NetworkMenuButton>();
                foreach (var b in all)
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    var rt = b.transform as RectTransform;
                    if (rt == null) continue;
                    bool hit = RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null);
                    string label = b.m_buttonText != null ? b.m_buttonText.text : "?";
                    if (hit)
                        MelonLogger.Msg($"[POGConfig]   HIT '{b.gameObject.name}' label='{label}' pos={rt.anchoredPosition} parent='{rt.parent?.name}'");
                }
            }
            catch (Exception ex)
            { MelonLogger.Warning($"[POGConfig] DiagnoseAtCursor: {ex.Message}"); }
        }

        // ── Open / Close ──────────────────────────────────────────────────────

        // Lazily create the injected state on a hidden, DontDestroyOnLoad host.
        // Returns null (and disables native mode) if anything fails.
        private PogConfigState EnsureState()
        {
            if (_cfgState != null) return _cfgState;
            if (_nativeInitTried) return _nativeOk ? _cfgState : null;
            _nativeInitTried = true;
            if (!StateTypeRegistered) { _nativeOk = false; return null; }
            try
            {
                var host = new GameObject("POGConfigState");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                _cfgState = host.AddComponent<PogConfigState>();
                _nativeOk = _cfgState != null;
                if (_nativeOk) MelonLogger.Msg("[POGConfig] Native config state created.");
            }
            catch (Exception ex)
            {
                _nativeOk = false;
                MelonLogger.Warning($"[POGConfig] Native state unavailable, overlay fallback: {ex.Message}");
            }
            return _nativeOk ? _cfgState : null;
        }

        private void OpenPanel()
        {
            if (_builtRegistryVersion != POGConfig.RegistryVersion) _contentBuilt = false;

            var nm = NetworkMenu.Instance;
            var st = EnsureState();
            if (st != null && nm != null && nm.CurrentState != null
                && nm.CurrentState.Pointer != st.Pointer)
            {
                try
                {
                    // Become a real menu state: the game hides the underlying menu via its
                    // OnExitState, then calls our OnEnterState → ShowElements(true).
                    _prevState = nm.CurrentState;
                    st.m_previousState = _prevState;
                    st.m_menu = nm;
                    _usingNativeState = true;
                    nm.SetState(st);
                    return;
                }
                catch (Exception ex)
                {
                    _usingNativeState = false;
                    _nativeOk = false; // stop trying native this session
                    MelonLogger.Warning($"[POGConfig] SetState open failed, overlay fallback: {ex.Message}");
                }
            }

            _usingNativeState = false;
            SetOverlayVisible(true);
        }

        internal void DoClose()
        {
            if (_usingNativeState)
            {
                var nm = NetworkMenu.Instance;
                bool stillActive = nm != null && _cfgState != null && nm.CurrentState != null
                                   && nm.CurrentState.Pointer == _cfgState.Pointer;
                if (stillActive && _prevState != null)
                {
                    // SetState(prev) → our OnExitState → ShowElements(false) hides the overlay
                    // and reshows the previous menu.
                    try { nm.SetState(_prevState); }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[POGConfig] SetState close failed: {ex.Message}");
                        SetOverlayVisible(false);
                    }
                }
                else
                {
                    // Already transitioned away (e.g. game cancelled) — just ensure hidden.
                    SetOverlayVisible(false);
                }
                _usingNativeState = false;
                return;
            }

            SetOverlayVisible(false);
        }

        // Single owner of overlay visibility + open/close bookkeeping. Called directly
        // in fallback mode, or via the state's ShowElements in native mode.
        internal void SetOverlayVisible(bool show)
        {
            if (show)
            {
                if (_builtRegistryVersion != POGConfig.RegistryVersion) _contentBuilt = false;
                bool alreadyBuilt = _contentBuilt;
                EnsureContent();
                // The Vanilla tab is a live mirror of Rewired: if it was left selected,
                // re-read on every panel open, otherwise binds changed in the game's own
                // Controls screen while the panel was closed would show stale values.
                // (EnsureContent already rebuilt everything when alreadyBuilt is false.)
                if (alreadyBuilt && _selectedMod == -1) RebuildContent();
                if (_overlay != null) _overlay.SetActive(true);

                // Block keyboard / gamepad submit on any underlying menu by clearing selection.
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                {
                    _savedSelected = es.currentSelectedGameObject;
                    es.SetSelectedGameObject(null);
                }
                POGConfig.SetPanelOpen(true);
            }
            else
            {
                // Finish anything half-done before hiding: a value being typed is applied,
                // a key listen is cancelled, the search box leaves edit mode. Otherwise the
                // panel reopens still mid-edit (DONE and game-driven closes skip the
                // per-row update that would normally end it).
                EndAllInteractions();
                if (_editingSearch) CommitSearchEdit();
                CancelVanillaListen();

                if (_overlay != null) _overlay.SetActive(false);
                LastCloseFrame = Time.frameCount;
                _scrollDragging = false;
                _sidebarDragging = false;

                // In native mode the previous state's OnEnterState reselects for us.
                if (!_usingNativeState)
                {
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null && _savedSelected != null)
                        es.SetSelectedGameObject(_savedSelected);
                }
                _savedSelected = null;

                Prefs.Flush();
                POGConfig.SetPanelOpen(false);
            }
        }

        private static void EndAllInteractions()
        {
            Popup?.Close();
            foreach (var rec in POGConfig.GetAllRecords())
                foreach (var e in rec.Entries)
                {
                    try { e.EndInteraction(); }
                    catch (Exception ex)
                    { MelonLogger.Warning($"[POGConfig] EndInteraction '{e.Label}': {ex.Message}"); }
                }
        }

        // ── Build static UI ───────────────────────────────────────────────────

        private void BuildStaticUI()
        {
            if (_uiReady) return;
            _uiReady = true;
            try
            {
                try
                {
                    var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                    if (fonts != null && fonts.Count > 0) _font = fonts[0];
                }
                catch { }
                try { if (_font == null) _font = TMP_Settings.defaultFontAsset; }
                catch { }

                // Standalone canvas root (NOT under HideAndDontSave runner)
                var canvasGo = new GameObject("POGCanvas");
                UnityEngine.Object.DontDestroyOnLoad(canvasGo);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = Theme.ReferenceResolution;
                scaler.matchWidthOrHeight  = 0.5f;
                // One number grows the whole panel, text included: see Theme.
                Theme.ScaleChanged += () => { if (scaler != null) scaler.referenceResolution = Theme.ReferenceResolution; };
                canvasGo.AddComponent<GraphicRaycaster>();

                if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
                {
                    var es = new GameObject("POG_EventSystem");
                    UnityEngine.Object.DontDestroyOnLoad(es);
                    es.AddComponent<EventSystem>();
                    es.AddComponent<StandaloneInputModule>();
                }

                var modsBtn = BuildModsButton(canvasGo.transform);
                _modsBtnRt = UI.Rt(modsBtn);
                modsBtn.SetActive(false);

                _overlay = BuildOverlay(canvasGo.transform);
                _overlay.SetActive(false);

                MelonLogger.Msg("[POGConfig] Canvas created.");
            }
            catch (Exception ex)
            {
                _uiReady = false; // allow retry next frame
                MelonLogger.Warning($"[POGConfig] BuildStaticUI failed: {ex.Message}");
            }
        }

        private GameObject BuildModsButton(Transform parent)
        {
            var go = UI.Make("ModsButton", parent);
            _modsBtnImg = UI.AddImage(go, new Color(0, 0, 0, 0));
            var textGo = UI.Make("Text", go.transform);
            UI.Stretch(UI.Rt(textGo));
            var txt = UI.AddText(textGo, ModsLabel.Current, _font, 20, Color.white, TextAlignmentOptions.Center);
            txt.fontStyle = FontStyles.Bold;
            _modsBtnText = txt;
            return go;
        }

        private GameObject BuildOverlay(Transform parent)
        {
            var overlayGo = UI.Make("Overlay", parent);
            UI.Stretch(UI.Rt(overlayGo));
            UI.AddImage(overlayGo, new Color(0, 0, 0, 0.82f));

            var panelGo = UI.Make("Panel", overlayGo.transform);
            var pRt = UI.Rt(panelGo);
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(PANEL_W, PANEL_H);
            var panelColor = new Color(0.055f, 0.055f, 0.07f, 0.99f);
            UI.AddImage(panelGo, panelColor);

            // ── Title (top bar) ──
            var titleGo = UI.Make("Title", panelGo.transform);
            UI.PlaceFromTop(UI.Rt(titleGo), 14f, 40f, 24f, 24f);
            var titleTxt = UI.AddText(titleGo, "MOD SETTINGS", _font, 22, Color.white,
                                      TextAlignmentOptions.MidlineLeft);
            titleTxt.fontStyle = FontStyles.Bold;

            var sepGo = UI.Make("Sep", panelGo.transform);
            UI.PlaceFromTop(UI.Rt(sepGo), 62f, 1f, 24f, 24f);
            UI.AddImage(sepGo, new Color(1, 1, 1, 0.12f), false);

            // ── Sidebar (left) ──
            BuildSidebar(panelGo.transform);

            // Vertical separator between sidebar and content
            var vSepGo = UI.Make("VSep", panelGo.transform);
            var vRt = UI.Rt(vSepGo);
            vRt.anchorMin = new Vector2(0, 0); vRt.anchorMax = new Vector2(0, 1);
            vRt.pivot = new Vector2(0, 0.5f);
            vRt.offsetMin = new Vector2(16f + SIDEBAR_W + 8f, BOT_H);
            vRt.offsetMax = new Vector2(16f + SIDEBAR_W + 9f, -SIDE_TOP);
            UI.AddImage(vSepGo, new Color(1, 1, 1, 0.10f), false);

            // ── Content area (right) ──
            BuildContentHeader(panelGo.transform);
            BuildViewport(panelGo.transform);
            BuildScrollbar(panelGo.transform);

            // ── DONE button (top-right) ──
            var doneGo = UI.Make("DoneBtn", panelGo.transform);
            _doneBtnRt = UI.Rt(doneGo);
            _doneBtnRt.anchorMin = new Vector2(1, 1); _doneBtnRt.anchorMax = new Vector2(1, 1);
            _doneBtnRt.pivot     = new Vector2(1, 1);
            _doneBtnRt.sizeDelta = new Vector2(120, 36);
            _doneBtnRt.anchoredPosition = new Vector2(-20, -16);
            UI.AddImage(doneGo, new Color(0.85f, 0.65f, 0.1f));
            var doneTxtGo = UI.Make("Text", doneGo.transform);
            UI.Stretch(UI.Rt(doneTxtGo));
            var doneTxt = UI.AddText(doneTxtGo, "DONE", _font, 15,
                                     new Color(0.08f, 0.04f, 0f), TextAlignmentOptions.Center);
            doneTxt.fontStyle = FontStyles.Bold;

            // Last child: drawn over everything else in the panel.
            _popup = new ChoicePopup(panelGo.transform, _font, PANEL_H);

            return overlayGo;
        }

        private void BuildSidebar(Transform panelTr)
        {
            // Sidebar root (left strip below title)
            var sb = UI.Make("Sidebar", panelTr);
            var sbRt = UI.Rt(sb);
            // Explicit insets: a box centred with sizeDelta -(TOP_H + BOT_H) started 46 units from
            // the top instead of TOP_H, over the title bar's separator.
            sbRt.anchorMin = new Vector2(0, 0); sbRt.anchorMax = new Vector2(0, 1);
            sbRt.pivot = new Vector2(0, 1);
            sbRt.offsetMin = new Vector2(16f, BOT_H);
            sbRt.offsetMax = new Vector2(16f + SIDEBAR_W, -SIDE_TOP);

            // Search box background
            var searchGo = UI.Make("Search", sb.transform);
            _searchBgRt = UI.Rt(searchGo);
            _searchBgRt.anchorMin = new Vector2(0, 1); _searchBgRt.anchorMax = new Vector2(1, 1);
            _searchBgRt.pivot = new Vector2(0.5f, 1f);
            _searchBgRt.sizeDelta = new Vector2(0, SEARCH_H);
            _searchBgRt.anchoredPosition = new Vector2(0, 0);
            _searchBgImg = UI.AddImage(searchGo, new Color(0.16f, 0.16f, 0.18f, 0.95f));

            var searchTxtGo = UI.Make("Text", searchGo.transform);
            UI.Stretch(UI.Rt(searchTxtGo), 8, 8, 0, 0);
            _searchText = UI.AddText(searchTxtGo, "Search…", _font, 13,
                                     new Color(0.55f, 0.55f, 0.6f), TextAlignmentOptions.MidlineLeft);
            _searchText.enableWordWrapping = false;
            _searchText.overflowMode = TextOverflowModes.Ellipsis;

            // Mod list scrollable area
            var listVp = UI.Make("ListVP", sb.transform);
            var lvpRt = UI.Rt(listVp);
            lvpRt.anchorMin = new Vector2(0, 0); lvpRt.anchorMax = new Vector2(1, 1);
            lvpRt.offsetMin = new Vector2(0, 0);
            lvpRt.offsetMax = new Vector2(0, -(SEARCH_H + SIDEBAR_PAD));
            listVp.AddComponent<RectMask2D>();
            _sidebarVpRt = lvpRt;

            var listGo = UI.Make("List", listVp.transform);
            _sidebarListRt = UI.Rt(listGo);
            _sidebarListRt.anchorMin = new Vector2(0, 1);
            _sidebarListRt.anchorMax = new Vector2(1, 1);
            _sidebarListRt.pivot = new Vector2(0.5f, 1f);
            _sidebarListRt.sizeDelta = new Vector2(0, 0);
            _sidebarListRt.anchoredPosition = Vector2.zero;
            _sidebarList = listGo.transform;

            // Sidebar scrollbar
            _sidebarScrollbarGo = UI.Make("SBScroll", sb.transform);
            _sidebarTrackRt = UI.Rt(_sidebarScrollbarGo);
            _sidebarTrackRt.anchorMin = new Vector2(1, 0); _sidebarTrackRt.anchorMax = new Vector2(1, 1);
            _sidebarTrackRt.pivot = new Vector2(1, 0.5f);
            _sidebarTrackRt.sizeDelta = new Vector2(SB_W * 0.6f, -(SEARCH_H + SIDEBAR_PAD));
            _sidebarTrackRt.anchoredPosition = new Vector2(0, -(SEARCH_H + SIDEBAR_PAD) * 0.5f);
            UI.AddImage(_sidebarScrollbarGo, new Color(0.10f, 0.10f, 0.12f));

            var sbThumbGo = UI.Make("Thumb", _sidebarScrollbarGo.transform);
            _sidebarThumbRt = UI.Rt(sbThumbGo);
            _sidebarThumbRt.anchorMin = new Vector2(0, 1); _sidebarThumbRt.anchorMax = new Vector2(1, 1);
            _sidebarThumbRt.pivot = new Vector2(0.5f, 1f);
            _sidebarThumbRt.sizeDelta = new Vector2(0, 30);
            UI.AddImage(sbThumbGo, new Color(0.4f, 0.4f, 0.45f));

            _sidebarScrollbarGo.SetActive(false);
        }

        private void BuildContentHeader(Transform panelTr)
        {
            var hdr = UI.Make("ContentHeader", panelTr);
            _contentHeaderRt = UI.Rt(hdr);
            float leftEdge = 16f + SIDEBAR_W + 16f + 1f; // sidebar + sep
            _contentHeaderRt.anchorMin = new Vector2(0, 1); _contentHeaderRt.anchorMax = new Vector2(1, 1);
            _contentHeaderRt.pivot = new Vector2(0, 1);
            _contentHeaderRt.offsetMin = new Vector2(leftEdge, -(TOP_H + MOD_HDR_H));
            _contentHeaderRt.offsetMax = new Vector2(-16, -TOP_H);

            var titleGo = UI.Make("ModTitle", hdr.transform);
            var tRt = UI.Rt(titleGo);
            tRt.anchorMin = new Vector2(0, 1); tRt.anchorMax = new Vector2(1, 1);
            tRt.pivot = new Vector2(0, 1);
            tRt.sizeDelta = new Vector2(0, 28);
            tRt.anchoredPosition = new Vector2(0, -2);
            _modTitleTmp = UI.AddText(titleGo, "", _font, 18, new Color(1f, 0.82f, 0.3f),
                                       TextAlignmentOptions.TopLeft);
            _modTitleTmp.fontStyle = FontStyles.Bold;

            // Version and author on one line, the description under it, wrapped.
            var subGo = UI.Make("ModSub", hdr.transform);
            var sRt = UI.Rt(subGo);
            sRt.anchorMin = new Vector2(0, 1); sRt.anchorMax = new Vector2(1, 1);
            sRt.pivot = new Vector2(0, 1);
            sRt.sizeDelta = new Vector2(0, 18);
            sRt.anchoredPosition = new Vector2(0, -30);
            _modSubTmp = UI.AddText(subGo, "", _font, 12,
                                    new Color(0.55f, 0.55f, 0.6f), TextAlignmentOptions.TopLeft);
            _modSubTmp.enableWordWrapping = false;
            _modSubTmp.overflowMode = TextOverflowModes.Ellipsis;

            var descGo = UI.Make("ModDesc", hdr.transform);
            var dRt = UI.Rt(descGo);
            dRt.anchorMin = Vector2.zero; dRt.anchorMax = Vector2.one;
            dRt.offsetMin = new Vector2(0, 4); dRt.offsetMax = new Vector2(0, -50);
            _modDescTmp = UI.AddText(descGo, "", _font, 13,
                                     new Color(0.78f, 0.78f, 0.8f), TextAlignmentOptions.TopLeft);
            _modDescTmp.enableWordWrapping = true;
            _modDescTmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        // Most lines of description the header shows before cutting it off.
        private const int HDR_DESC_MAX_LINES = 4;

        /// <summary>Fills the mod header and resizes it (and the rows' viewport) to fit the description.</summary>
        private void SetModHeader(string title, string meta, string description)
        {
            if (_modTitleTmp != null) _modTitleTmp.text = title ?? "";
            if (_modSubTmp != null) _modSubTmp.text = meta ?? "";
            float descH = 0f;
            if (_modDescTmp != null)
            {
                _modDescTmp.text = description ?? "";
                if (!string.IsNullOrEmpty(description) && _contentHeaderRt != null)
                {
                    float width = Mathf.Max(100f, _contentHeaderRt.rect.width);
                    float line = _modDescTmp.GetPreferredValues("Ag", width, 0f).y;
                    descH = Mathf.Min(_modDescTmp.GetPreferredValues(description, width, 0f).y, line * HDR_DESC_MAX_LINES + 2f);
                }
            }
            _modHdrH = Mathf.Max(MOD_HDR_H, 50f + descH + 10f);
            if (_contentHeaderRt != null) _contentHeaderRt.offsetMin = new Vector2(_contentHeaderRt.offsetMin.x, -(TOP_H + _modHdrH));
            if (_contentVpRt != null) _contentVpRt.offsetMax = new Vector2(_contentVpRt.offsetMax.x, -(TOP_H + _modHdrH));
            if (_scrollTrackRt != null) _scrollTrackRt.offsetMax = new Vector2(_scrollTrackRt.offsetMax.x, -(TOP_H + _modHdrH));
        }

        private void BuildViewport(Transform panelTr)
        {
            var vpGo = UI.Make("Viewport", panelTr);
            var vpRt = UI.Rt(vpGo);
            float leftEdge = 16f + SIDEBAR_W + 16f + 1f;
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(leftEdge, BOT_H);
            vpRt.offsetMax = new Vector2(-(16 + SB_W + 6), -(TOP_H + MOD_HDR_H));
            vpGo.AddComponent<RectMask2D>();
            _contentVpRt = vpRt;

            var contentGo = UI.Make("ScrollContent", vpGo.transform);
            _scrollContentRt = UI.Rt(contentGo);
            _scrollContentRt.anchorMin = new Vector2(0, 1);
            _scrollContentRt.anchorMax = new Vector2(1, 1);
            _scrollContentRt.pivot = new Vector2(0.5f, 1f);
            _scrollContentRt.sizeDelta = new Vector2(0, 0);
            _scrollContentRt.anchoredPosition = Vector2.zero;
            _scrollContent = contentGo.transform;
        }

        private void BuildScrollbar(Transform panelTr)
        {
            _scrollbarGo = UI.Make("ScrollTrack", panelTr);
            _scrollTrackRt = UI.Rt(_scrollbarGo);
            _scrollTrackRt.anchorMin = new Vector2(1, 0);
            _scrollTrackRt.anchorMax = new Vector2(1, 1);
            _scrollTrackRt.pivot     = new Vector2(1, 0.5f);
            _scrollTrackRt.offsetMin = new Vector2(-(SB_W + 8), BOT_H);
            _scrollTrackRt.offsetMax = new Vector2(-8, -(TOP_H + MOD_HDR_H));
            UI.AddImage(_scrollbarGo, new Color(0.12f, 0.12f, 0.14f));

            var thumbGo = UI.Make("Thumb", _scrollbarGo.transform);
            _scrollThumbRt = UI.Rt(thumbGo);
            _scrollThumbRt.anchorMin = new Vector2(0, 1);
            _scrollThumbRt.anchorMax = new Vector2(1, 1);
            _scrollThumbRt.pivot     = new Vector2(0.5f, 1f);
            _scrollThumbRt.offsetMin = new Vector2(2, 0);
            _scrollThumbRt.offsetMax = new Vector2(-2, 0);
            _scrollThumbRt.sizeDelta = new Vector2(-4, 40);
            _scrollThumbRt.anchoredPosition = Vector2.zero;
            UI.AddImage(thumbGo, new Color(0.34f, 0.34f, 0.38f));

            _scrollbarGo.SetActive(false);
        }

        // ── EnsureContent ─────────────────────────────────────────────────────

        private void EnsureContent()
        {
            if (_contentBuilt || _scrollContent == null) return;
            _contentBuilt = true;
            _builtRegistryVersion = POGConfig.RegistryVersion;
            RebuildAll();
        }

        // Rebuild both sidebar and content panel from scratch.
        private void RebuildAll()
        {
            Clicks.Clear();
            if (_doneBtnRt != null) Clicks.Register(_doneBtnRt, DoClose);

            // Clamp selection. -1 is the Vanilla Binds pseudo-entry, which always exists,
            // so an empty mods list falls back to it instead of the old "no mods" message.
            var mods = POGConfig.GetAllRecords();
            if (_selectedMod < -1) _selectedMod = -1;
            else if (_selectedMod >= 0)
            {
                if (mods.Count == 0) _selectedMod = -1;
                else if (_selectedMod >= mods.Count) _selectedMod = mods.Count - 1;
            }

            RebuildSidebar();
            RebuildContent();
        }

        private void RebuildSidebar()
        {
            if (_sidebarList == null) return;

            for (int i = _sidebarList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_sidebarList.GetChild(i).gameObject);
            // Drop the destroyed items' clicks too; otherwise every search keystroke leaves
            // another set of dead rects in Clicks.Map.
            foreach (var oldRt in _sidebarItemRects.Values) Clicks.Unregister(oldRt);
            _sidebarItemRects.Clear();
            _sidebarItemBgs.Clear();

            var mods = POGConfig.GetAllRecords();
            const float ITEM_H = 36f;
            const float GAP    = 2f;
            float y = 4f;

            // Vanilla is a pseudo-entry (index -1), always shown first and unaffected by
            // search, so the read-only bind mirror is never hidden by a mod-name query.
            AddSidebarItem(-1, "VANILLA BINDS", vanilla: true, ref y, ITEM_H, GAP);

            string q = (_searchQuery ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < mods.Count; i++)
            {
                var rec = mods[i];
                if (q.Length > 0 && !rec.Name.ToLowerInvariant().Contains(q)) continue;
                AddSidebarItem(i, rec.Name, vanilla: false, ref y, ITEM_H, GAP);
            }

            _sidebarTotalH = y + 4f;
            _sidebarOffset = 0f;
            if (_sidebarListRt != null)
            {
                _sidebarListRt.sizeDelta = new Vector2(0, _sidebarTotalH);
                // Snap the list back to top — offset is reset above, so without this the
                // previous scroll position persists and items hide above the viewport.
                _sidebarListRt.anchoredPosition = Vector2.zero;
            }

            float listVpH = SIDEBAR_LIST_H;
            bool needsScroll = _sidebarTotalH > listVpH;
            if (_sidebarScrollbarGo != null) _sidebarScrollbarGo.SetActive(needsScroll);
        }

        private void AddSidebarItem(int idx, string label, bool vanilla, ref float y, float itemH, float gap)
        {
            var item = UI.Make("MI_" + (vanilla ? "Vanilla" : label), _sidebarList);
            var iRt = UI.Rt(item);
            iRt.anchorMin = new Vector2(0, 1); iRt.anchorMax = new Vector2(1, 1);
            iRt.pivot = new Vector2(0.5f, 1f);
            iRt.sizeDelta = new Vector2(-4, itemH);
            iRt.anchoredPosition = new Vector2(0, -y);

            bool isSel = idx == _selectedMod;
            Color selBg   = vanilla ? new Color(0.45f, 0.68f, 0.95f, 0.18f) : new Color(1f, 0.82f, 0.3f, 0.18f);
            Color selText = vanilla ? new Color(0.65f, 0.82f, 1f) : new Color(1f, 0.85f, 0.4f);
            var bg = UI.AddImage(item, isSel ? selBg : new Color(0.12f, 0.12f, 0.14f, 0.7f));
            _sidebarItemBgs[idx] = bg;
            _sidebarItemRects[idx] = iRt;

            var lblGo = UI.Make("Label", item.transform);
            UI.Stretch(UI.Rt(lblGo), 10, 10, 0, 0);
            var lTmp = UI.AddText(lblGo, label, _font, 13,
                isSel ? selText : new Color(0.85f, 0.85f, 0.88f), TextAlignmentOptions.MidlineLeft);
            if (isSel) lTmp.fontStyle = FontStyles.Bold;
            if (vanilla) lTmp.fontStyle |= FontStyles.Italic;
            lTmp.enableWordWrapping = false;
            lTmp.overflowMode = TextOverflowModes.Ellipsis;

            Clicks.Register(iRt, () => SelectMod(idx));

            y += itemH + gap;
        }

        private void SelectMod(int index)
        {
            if (index == _selectedMod) return;
            _selectedMod = index;
            RebuildSidebar();   // refresh selection highlight
            RebuildContent();
        }

        private static void RestoreModDefaults(ModRecord rec)
        {
            foreach (var e in rec.Entries)
            {
                try { e.RestoreDefault(); }
                catch (Exception ex)
                { MelonLogger.Warning($"[POGConfig] RestoreDefault '{e.Label}': {ex.Message}"); }
            }
            MelonLogger.Msg($"[POGConfig] '{rec.Name}': settings restored to defaults.");
        }

        private void RebuildContent()
        {
            if (_scrollContent == null) return;

            // End any in-progress edit or bind listening BEFORE destroying the rows it lived
            // on: a value being typed is applied, a key listen is cancelled.
            EndAllInteractions();
            _vListenRow = null;
            _vListenSlot = -1;

            for (int i = _scrollContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_scrollContent.GetChild(i).gameObject);

            // Drop any per-row clicks (KeyEntry Change/Clear, SliderEntry value boxes, sidebar items)
            // and re-register the durable ones.
            Clicks.Clear();
            if (_doneBtnRt != null) Clicks.Register(_doneBtnRt, DoClose);
            // Re-register sidebar items (they live across content rebuilds)
            foreach (var kv in _sidebarItemRects)
            {
                int idx = kv.Key;
                Clicks.Register(kv.Value, () => SelectMod(idx));
            }

            if (_selectedMod == -1) { RebuildVanillaContent(); return; }

            var mods = POGConfig.GetAllRecords();
            if (mods.Count == 0)
            {
                SetModHeader("No mods registered", "", "Install a POGConfig-aware mod to see settings here.");
                _totalContentH = 0;
                if (_scrollContentRt != null) _scrollContentRt.sizeDelta = new Vector2(0, 0);
                if (_scrollbarGo != null) _scrollbarGo.SetActive(false);
                return;
            }

            var rec = mods[_selectedMod];
            {
                string meta = "";
                if (rec.Info != null)
                {
                    if (!string.IsNullOrEmpty(rec.Info.Version)) meta += "v" + rec.Info.Version;
                    if (!string.IsNullOrEmpty(rec.Info.Author))  meta += (meta.Length > 0 ? "  ·  " : "") + "by " + rec.Info.Author;
                }
                SetModHeader(rec.Name, meta, rec.Info?.Description);
            }

            const float ROW_H = 40f;
            float y = 4f;
            for (int j = 0; j < rec.Entries.Count; j++)
            {
                var entry = rec.Entries[j];
                // Rows are at least ROW_H; a row may ask for more (ChoiceRow with a description).
                float h = entry is HeaderEntry ? entry.Height : Math.Max(ROW_H, entry.Height);
                try
                {
                    var row = UI.Make("R_" + (entry.Label ?? "?"), _scrollContent);
                    UI.PlaceFromTop(UI.Rt(row), y, h);
                    entry.BuildRowInto(row, _font);
                    float width = _scrollContentRt != null && _scrollContentRt.rect.width > 1f
                        ? _scrollContentRt.rect.width : PANEL_W - SIDEBAR_W - 80f;
                    float measured = entry.MeasureHeight(width, h);
                    if (Math.Abs(measured - h) > 0.5f) { h = measured; UI.PlaceFromTop(UI.Rt(row), y, h); }
                }
                catch (Exception ex)
                { MelonLogger.Warning($"[POGConfig] Row '{entry.Label}': {ex.Message}"); }
                y += h + 2f;
            }

            // RESTORE DEFAULTS — bottom-right, resets every entry of this mod to the
            // values it was registered with (captured before saved prefs were applied).
            {
                y += 10f;
                var rdRow = UI.Make("R_RestoreDefaults", _scrollContent);
                UI.PlaceFromTop(UI.Rt(rdRow), y, 30f);

                var rdRt = UI.AddButton(rdRow.transform, "RESTORE DEFAULTS", _font,
                                        new Color(0.24f, 0.16f, 0.14f), new Color(0.9f, 0.62f, 0.5f));
                var recCaptured = rec;
                Clicks.Register(rdRt, () => RestoreModDefaults(recCaptured));
                y += 32f;
            }

            _totalContentH = y + 4f;
            _scrollOffset  = 0f;
            if (_scrollContentRt != null)
            {
                _scrollContentRt.sizeDelta = new Vector2(0, _totalContentH);
                // Snap scroll back to top when switching mods — without this the previous
                // mod's scroll offset persists and items hide above the viewport.
                _scrollContentRt.anchoredPosition = Vector2.zero;
            }
            bool needsScroll = _totalContentH > ViewportH;
            if (_scrollbarGo != null) _scrollbarGo.SetActive(needsScroll);
            // Reset scrollbar thumb visual to top.
            if (needsScroll) UpdateScrollThumb(Mathf.Max(0, _totalContentH - ViewportH));
            // Cancel any in-progress drag from the previous mod's content.
            _scrollDragging = false;
        }

        // Live, editable mirror of the game's own Controls screen. Rebuilt fresh every
        // time the tab is selected; edits are written straight into Rewired (the same
        // storage the vanilla screen uses) and verified by re-reading.
        private void RebuildVanillaContent()
        {
            _vanillaRows.Clear();
            _vListenRow = null;
            _vListenSlot = -1;

            SetModHeader("Vanilla Binds", "",
                "The game's own controls. Click a slot to rebind — changes apply to the vanilla settings directly.");

            var rows = VanillaBinds.TryRead();
            if (rows == null || rows.Count == 0)
            {
                var msgGo = UI.Make("R_Msg", _scrollContent);
                UI.PlaceFromTop(UI.Rt(msgGo), 4f, 40f);
                UI.AddText(msgGo,
                    rows == null ? "Vanilla binds unavailable right now." : "No user-assignable binds found.",
                    _font, 13, new Color(0.6f, 0.6f, 0.64f));
                _totalContentH = 48f;
            }
            else
            {
                float y = 4f;

                // Column headers over the three slots.
                var hdrGo = UI.Make("R_Header", _scrollContent);
                UI.PlaceFromTop(UI.Rt(hdrGo), y, 18f);
                string[] heads = { "KEYBOARD", "KEYBOARD 2", "MOUSE" };
                const float slotsStart = 0.40f, gap = 0.012f;
                float slotW = (1f - slotsStart - gap * 2) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    float x0 = slotsStart + i * (slotW + gap);
                    var hGo = UI.Make("H" + i, hdrGo.transform);
                    var hRt = UI.Rt(hGo);
                    hRt.anchorMin = new Vector2(x0, 0f);
                    hRt.anchorMax = new Vector2(x0 + slotW, 1f);
                    hRt.offsetMin = hRt.offsetMax = Vector2.zero;
                    var hTmp = UI.AddText(hGo, heads[i], _font, 10,
                                          new Color(0.5f, 0.55f, 0.65f), TextAlignmentOptions.Center);
                    hTmp.fontStyle = FontStyles.Bold;
                }
                y += 20f;

                const float ROW_H = 40f;
                foreach (var row in rows)
                {
                    var rowGo = UI.Make("R_" + row.Action, _scrollContent);
                    UI.PlaceFromTop(UI.Rt(rowGo), y, ROW_H);
                    BuildVanillaRow(rowGo, row);
                    y += ROW_H + 2f;
                }

                // RESTORE DEFAULTS — resets the game's keyboard+mouse maps to their
                // defaults (same as the vanilla Controls screen's own button).
                y += 10f;
                var rdRow = UI.Make("R_RestoreDefaults", _scrollContent);
                UI.PlaceFromTop(UI.Rt(rdRow), y, 30f);
                var rdRt = UI.AddButton(rdRow.transform, "RESTORE DEFAULTS", _font,
                                        new Color(0.14f, 0.18f, 0.24f), new Color(0.6f, 0.78f, 1f));
                Clicks.Register(rdRt, RestoreVanillaDefaults);
                y += 32f;

                _totalContentH = y + 4f;
            }

            _scrollOffset = 0f;
            if (_scrollContentRt != null)
            {
                _scrollContentRt.sizeDelta = new Vector2(0, _totalContentH);
                _scrollContentRt.anchoredPosition = Vector2.zero;
            }
            bool needsScroll = _totalContentH > ViewportH;
            if (_scrollbarGo != null) _scrollbarGo.SetActive(needsScroll);
            if (needsScroll) UpdateScrollThumb(Mathf.Max(0, _totalContentH - ViewportH));
            _scrollDragging = false;
        }

        private void BuildVanillaRow(GameObject row, VanillaBinds.Row data)
        {
            var ui = new VanillaRowUI { Data = data };

            var lbl = UI.Make("Label", row.transform);
            UI.Rt(lbl).anchorMin = Vector2.zero;
            UI.Rt(lbl).anchorMax = new Vector2(0.40f, 1f);
            UI.Rt(lbl).offsetMin = UI.Rt(lbl).offsetMax = Vector2.zero;
            var lTmp = UI.AddText(lbl, data.Action, _font, 14, new Color(0.82f, 0.86f, 0.92f));
            lTmp.enableWordWrapping = false;
            lTmp.overflowMode = TextOverflowModes.Ellipsis;

            // Same 3-column grid as KeyEntry; blue tint marks these as vanilla rows.
            const float slotsStart = 0.40f, gap = 0.012f;
            float slotW = (1f - slotsStart - gap * 2) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int slotIdx = i; // capture
                float x0 = slotsStart + i * (slotW + gap);
                float x1 = x0 + slotW;
                var slotGo = UI.Make("Slot" + i, row.transform);
                var slotRt = UI.Rt(slotGo);
                slotRt.anchorMin = new Vector2(x0, 0.14f);
                slotRt.anchorMax = new Vector2(x1, 0.86f);
                slotRt.offsetMin = slotRt.offsetMax = Vector2.zero;
                ui.Bgs[i] = UI.AddImage(slotGo, VNormalBg);

                // Bind area = slot minus the 14px clear-"x" column (non-overlapping rects,
                // same pattern as KeyEntry).
                var bindGo = UI.Make("Bind", slotGo.transform);
                var bindRt = UI.Rt(bindGo);
                UI.Stretch(bindRt, 0, 14, 0, 0);
                Clicks.Register(bindRt, () => StartVanillaListen(ui, slotIdx));

                var txtGo = UI.Make("Text", slotGo.transform);
                UI.Stretch(UI.Rt(txtGo), 4, 16, 0, 0);
                var txt = UI.AddText(txtGo, data.Slots[i].Display, _font, 12,
                                     new Color(0.6f, 0.78f, 1f), TextAlignmentOptions.MidlineLeft);
                txt.enableWordWrapping = false;
                txt.overflowMode = TextOverflowModes.Ellipsis;
                ui.Texts[i] = txt;

                var clearGo = UI.Make("Clear", slotGo.transform);
                var clearRt = UI.Rt(clearGo);
                clearRt.anchorMin = clearRt.anchorMax = new Vector2(1f, 1f);
                clearRt.pivot = new Vector2(1f, 1f);
                clearRt.sizeDelta = new Vector2(14, 16);
                clearRt.anchoredPosition = Vector2.zero;
                UI.AddText(clearGo, "x", _font, 11, new Color(0.85f, 0.5f, 0.5f), TextAlignmentOptions.Center);
                Clicks.Register(clearRt, () => VanillaClear(ui, slotIdx));
                ui.Clears[i] = clearGo;
                clearGo.SetActive(data.Slots[i].MapId != 0);
            }

            _vanillaRows.Add(ui);
        }

        private void StartVanillaListen(VanillaRowUI ui, int slotIdx)
        {
            if (_vListenSlot >= 0 || KeyEntry.AnyWaiting) return;
            _vListenRow = ui;
            _vListenSlot = slotIdx;
            _vListenFrame = Time.frameCount;
            // _vListenSlot >= 0 now makes KeyEntry.AnyWaiting true, which blocks Esc-close
            // and mod KeyEntry listening.
            if (ui.Texts[slotIdx] != null) ui.Texts[slotIdx].text = "…";
            if (ui.Bgs[slotIdx] != null) ui.Bgs[slotIdx].color = VListenBg;
        }

        private void CancelVanillaListen()
        {
            if (_vListenSlot < 0) return;
            var row = _vListenRow;
            _vListenRow = null;
            _vListenSlot = -1;
            if (row != null) RefreshVanillaRowUI(row);
        }

        // Per-frame while the panel is open: drives vanilla slot listening. Keyboard
        // columns only accept keys, the mouse column only accepts mouse buttons — the
        // typed 2+1 structure IS how Rewired stores these, so the UI can't request an
        // assignment the vanilla screen couldn't represent.
        private void UpdateVanillaListen()
        {
            if (_vListenSlot < 0 || _vListenRow == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            { ConsumeEsc(); CancelVanillaListen(); return; }

            // Swallow the click that started listening (it would bind Mouse0 instantly).
            if (Time.frameCount == _vListenFrame) return;

            var row = _vListenRow;
            int slotIdx = _vListenSlot;
            var kc = KeyCapture.PollDown(k => slotIdx < 2 ? !KeyCapture.IsMouse(k) : KeyCapture.IsMouse(k));
            if (kc == KeyCode.None) return;

            bool verified = VanillaBinds.TryWrite(row.Data, slotIdx, kc);
            _vListenRow = null;
            _vListenSlot = -1;
            RefreshVanillaRowUI(row);
            if (!verified)
                MelonLogger.Warning($"[POGConfig] Vanilla rebind '{row.Data.Action}' → {kc} not confirmed; showing actual state.");
        }

        private void VanillaClear(VanillaRowUI ui, int slotIdx)
        {
            if (_vListenSlot >= 0) CancelVanillaListen();
            VanillaBinds.TryClear(ui.Data, slotIdx);
            RefreshVanillaRowUI(ui);
        }

        private void RestoreVanillaDefaults()
        {
            if (_vListenSlot >= 0) CancelVanillaListen();
            if (VanillaBinds.RestoreDefaults())
                RebuildContent(); // re-read all rows so the UI reflects the reset binds
        }

        private static void RefreshVanillaRowUI(VanillaRowUI ui)
        {
            for (int i = 0; i < 3; i++)
            {
                var s = ui.Data.Slots[i];
                if (ui.Texts[i] != null) ui.Texts[i].text = s.Display;
                if (ui.Bgs[i] != null) ui.Bgs[i].color = VNormalBg;
                if (ui.Clears[i] != null) ui.Clears[i].SetActive(s.MapId != 0);
            }
        }
    }
}
