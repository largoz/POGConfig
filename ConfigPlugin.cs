using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2CppGame.Menu;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
        internal static void Clear() => Map.Clear();
    }

    internal static class UI
    {
        private static Sprite _white;
        internal static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var t = new Texture2D(1, 1);
                t.SetPixel(0, 0, Color.white);
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
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public abstract class ConfigEntry
    {
        public string Label { get; protected set; }
        protected ConfigEntry(string label) { Label = label; }

        public virtual void Draw(float x, float y, float width, GUIStyle labelStyle) { }
        public virtual float Height => 36f;

        internal abstract void BuildRowInto(GameObject row, TMP_FontAsset font);
        internal virtual void BuildRow(Transform parent, TMP_FontAsset font) { }
        internal virtual void OnUpdate() { }
        internal virtual void BindPrefs(MelonPreferences_Category cat) { }
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

        public ToggleEntry(string label, Func<bool> get, Action<bool> set)
            : this(label, get, set, null) { }
        public ToggleEntry(string label, Func<bool> get, Action<bool> set, string prefKey) : base(label)
        { _get = get; _set = set; _prefKey = prefKey; }

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var pref = cat.CreateEntry(_prefKey, _get());
            _set(pref.Value);
            var orig = _set;
            _set = v => { orig(v); pref.Value = v; MelonPreferences.Save(); };
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
            tRt.sizeDelta = new Vector2(40, 24);
            tRt.anchoredPosition = Vector2.zero;

            _toggle = tGo.AddComponent<Toggle>();
            var bg = UI.Make("BG", tGo.transform);
            UI.Stretch(UI.Rt(bg));
            var bgImg = UI.AddImage(bg, new Color(0.18f, 0.18f, 0.2f));
            var ck = UI.Make("Check", bg.transform);
            UI.Stretch(UI.Rt(ck), 3, 3, 3, 3);
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

        internal static bool AnyInputActive;

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

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var pref = cat.CreateEntry(_prefKey, _get());
            _set(pref.Value);
            var orig = _set;
            _set = v => { orig(v); pref.Value = v; MelonPreferences.Save(); };
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

            var handleArea = UI.Make("HandleArea", sGo.transform);
            UI.Stretch(UI.Rt(handleArea));
            var handle = UI.Make("Handle", handleArea.transform);
            UI.Rt(handle).anchorMin = UI.Rt(handle).anchorMax = new Vector2(0f, 0.5f);
            UI.Rt(handle).sizeDelta = new Vector2(14, 14);
            var hImg = UI.AddImage(handle, new Color(1f, 0.82f, 0.3f));

            _slider.fillRect = null;
            _slider.handleRect = UI.Rt(handle);
            _slider.targetGraphic = hImg;
            _lastValue = _get();
            _slider.SetValueWithoutNotify(_lastValue);
            _slider.onValueChanged.AddListener((Action<float>)OnSliderChanged);
            UpdateFillFromOrigin(_lastValue);
        }

        private void BuildStepPoints(Transform sliderTransform)
        {
            if (_stepPointsCount < 2) return;
            var pointsGo = UI.Make("StepPoints", sliderTransform);
            var pointsRt = UI.Rt(pointsGo);
            pointsRt.anchorMin = new Vector2(0f, 0.5f);
            pointsRt.anchorMax = new Vector2(1f, 0.5f);
            pointsRt.offsetMin = new Vector2(0f, -1f);
            pointsRt.offsetMax = new Vector2(0f, 1f);

            for (int i = 0; i < _stepPointsCount; i++)
            {
                float t = _stepPointsCount <= 1 ? 0f : i / (float)(_stepPointsCount - 1);
                var dot = UI.Make("P" + i, pointsGo.transform);
                var dotRt = UI.Rt(dot);
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(t, 0.5f);
                dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.sizeDelta = new Vector2(4f, 4f);
                dotRt.anchoredPosition = Vector2.zero;
                UI.AddImage(dot, new Color(0.48f, 0.48f, 0.52f, 0.75f), false);
            }
        }

        private void StartEditing()
        {
            if (AnyInputActive) return;
            _editingInput = true;
            AnyInputActive = true;
            _marqueeTimer = 0f;
            _inputBuffer = ToEditableNumericString(_lastValue);
            if (_valueBgImg != null) _valueBgImg.color = new Color(0.28f, 0.22f, 0.08f);
            if (_inputText != null) _inputText.text = _inputBuffer + "|";
        }

        private void StopEditing(bool commit)
        {
            _editingInput = false;
            AnyInputActive = false;
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
                { ConfigBehaviour.EscConsumed = true; StopEditing(false); return; }
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

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null || _options.Length == 0) return;
            var pref = cat.CreateEntry(_prefKey, Mathf.Clamp(_get(), 0, _options.Length - 1));
            _set(Mathf.Clamp(pref.Value, 0, _options.Length - 1));
            var orig = _set;
            _set = i => { orig(i); pref.Value = i; MelonPreferences.Save(); };
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

            var handleArea = UI.Make("HandleArea", sGo.transform);
            UI.Stretch(UI.Rt(handleArea));
            var handle = UI.Make("Handle", handleArea.transform);
            UI.Rt(handle).anchorMin = UI.Rt(handle).anchorMax = new Vector2(0f, 0.5f);
            UI.Rt(handle).sizeDelta = new Vector2(14, 14);
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

    public class KeyEntry : ConfigEntry
    {
        private readonly Func<KeyCode> _get;
        private Action<KeyCode> _set;
        private readonly string _prefKey;

        // Set to true to allow binding mouse buttons (Mouse0–Mouse6).
        public bool AllowMouseButtons { get; set; } = false;

        internal static bool AnyWaiting;
        private bool _listening;
        private TextMeshProUGUI _keyText;
        private TextMeshProUGUI _btnText;
        private RectTransform _clearBtnRt;

        public KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set)
            : this(label, get, set, null) { }
        public KeyEntry(string label, Func<KeyCode> get, Action<KeyCode> set, string prefKey) : base(label)
        { _get = get; _set = set; _prefKey = prefKey; }

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var pref = cat.CreateEntry(_prefKey, _get().ToString());
            if (Enum.TryParse<KeyCode>(pref.Value, out var loaded)) _set(loaded);
            var orig = _set;
            _set = v => { orig(v); pref.Value = v.ToString(); MelonPreferences.Save(); };
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            var lbl = UI.Make("Label", row.transform);
            UI.Rt(lbl).anchorMin = Vector2.zero;
            UI.Rt(lbl).anchorMax = new Vector2(0.42f, 1f);
            UI.Rt(lbl).offsetMin = UI.Rt(lbl).offsetMax = Vector2.zero;
            UI.AddText(lbl, Label, font, 14, new Color(0.88f, 0.88f, 0.88f));

            var keyGo = UI.Make("KeyName", row.transform);
            UI.Rt(keyGo).anchorMin = new Vector2(0.42f, 0f);
            UI.Rt(keyGo).anchorMax = new Vector2(0.70f, 1f);
            UI.Rt(keyGo).offsetMin = UI.Rt(keyGo).offsetMax = Vector2.zero;
            _keyText = UI.AddText(keyGo, KeyDisplayName(_get()), font, 13, new Color(1f, 0.82f, 0.3f),
                                  TextAlignmentOptions.MidlineRight);

            // Clear button
            var clearGo = UI.Make("ClearBtn", row.transform);
            _clearBtnRt = UI.Rt(clearGo);
            _clearBtnRt.anchorMin = _clearBtnRt.anchorMax = new Vector2(1f, 0.5f);
            _clearBtnRt.pivot = new Vector2(1f, 0.5f);
            _clearBtnRt.sizeDelta = new Vector2(56, 28);
            _clearBtnRt.anchoredPosition = new Vector2(-96f, 0f);
            UI.AddImage(clearGo, new Color(0.30f, 0.18f, 0.18f));
            var clearTxtGo = UI.Make("Text", clearGo.transform);
            UI.Stretch(UI.Rt(clearTxtGo));
            UI.AddText(clearTxtGo, "Clear", font, 12, new Color(0.85f, 0.55f, 0.55f), TextAlignmentOptions.Center);
            Clicks.Register(_clearBtnRt, ClearBind);

            // Change button
            var btnGo = UI.Make("ChangeBtn", row.transform);
            var btnRt = UI.Rt(btnGo);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.sizeDelta = new Vector2(90, 28);
            btnRt.anchoredPosition = Vector2.zero;
            UI.AddImage(btnGo, new Color(0.25f, 0.25f, 0.3f));

            var btnTxtGo = UI.Make("Text", btnGo.transform);
            UI.Stretch(UI.Rt(btnTxtGo));
            _btnText = UI.AddText(btnTxtGo, "Change", font, 12,
                                  new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Center);
            Clicks.Register(btnRt, ToggleListen);
        }

        private static string KeyDisplayName(KeyCode kc) => kc == KeyCode.None ? "— none —" : kc.ToString();

        private void ClearBind()
        {
            if (_listening) { _listening = false; AnyWaiting = false; }
            _set(KeyCode.None);
            if (_keyText != null) _keyText.text = KeyDisplayName(KeyCode.None);
            if (_btnText != null) _btnText.text = "Change";
        }

        private void ToggleListen()
        {
            if (_listening)
            {
                _listening = false; AnyWaiting = false;
                if (_keyText != null) _keyText.text = _get().ToString();
                if (_btnText != null) _btnText.text = "Change";
            }
            else if (!AnyWaiting)
            {
                _listening = true; AnyWaiting = true;
                if (_keyText != null) _keyText.text = "[ press key ]";
                if (_btnText != null) _btnText.text = "Cancel";
            }
            else
            {
                if (_keyText != null) _keyText.text = KeyDisplayName(_get());
            }
        }

        internal override void OnUpdate()
        {
            if (!_listening) return;
            if (SliderEntry.AnyInputActive) return;
            if (Input.GetKeyDown(KeyCode.Escape)) // cancel binding
            { ConfigBehaviour.EscConsumed = true; ToggleListen(); return; }
            foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
            {
                if (kc == KeyCode.None || kc == KeyCode.Escape) continue;
                if (!AllowMouseButtons && kc >= KeyCode.Mouse0 && kc <= KeyCode.Mouse6) continue;
                if (!Input.GetKeyDown(kc)) continue;
                _set(kc);
                _listening = false; AnyWaiting = false;
                if (_keyText != null) _keyText.text = KeyDisplayName(kc);
                if (_btnText != null) _btnText.text = "Change";
                break;
            }
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

        // True while MOD SETTINGS panel is open. Check this before processing hotkeys.
        public static bool PanelOpen { get; internal set; }
        internal static int RegistryVersion => _registryVersion;

        public static void Register(string modName, List<ConfigEntry> entries)
            => Register(modName, entries, null);

        public static void Register(string modName, List<ConfigEntry> entries, ModInfo info)
        {
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

            _mods.Add(new ModRecord { Name = modName, Entries = entries, Info = info });
            _registryVersion++;
            MelonLogger.Msg($"[POGConfig] Registered: {modName} ({entries.Count} entries)");
        }

        // Backwards-compat tuple view used by Update() to iterate OnUpdate() across all mods.
        internal static IEnumerable<(string Name, List<ConfigEntry> Entries)> GetAll()
        {
            foreach (var m in _mods) yield return (m.Name, m.Entries);
        }

        internal static IReadOnlyList<ModRecord> GetAllRecords() => _mods;
    }

    // ── Harmony patch ─────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(NetworkMenu), "TogglePauseMenu")]
    class PatchTogglePauseMenu
    {
        static bool Prefix()
        {
            if (!ConfigBehaviour.PanelOpen) return true;
            ConfigBehaviour.RequestClose();
            return false;
        }
    }

    // ── Mod entry ─────────────────────────────────────────────────────────────

    public class PogConfigMelon : MelonMod
    {
        public const string VERSION = "1.0.8";

        public override void OnInitializeMelon()
        {
            try { HarmonyInstance.PatchAll(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] Harmony patch failed: {ex.Message}"); }

            ClassInjector.RegisterTypeInIl2Cpp<ConfigBehaviour>();
            var runner = new GameObject("POGConfigRunner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.hideFlags = HideFlags.HideAndDontSave;
            runner.AddComponent<ConfigBehaviour>();
            MelonLogger.Msg($"POG Config {VERSION} loaded.");
        }
    }

    // ── ConfigBehaviour ───────────────────────────────────────────────────────

    public class ConfigBehaviour : MonoBehaviour
    {
        public ConfigBehaviour(IntPtr ptr) : base(ptr) { }

        internal static bool PanelOpen;
        internal static void RequestClose() => _instance?.DoClose();
        private static ConfigBehaviour _instance;

        // Set by any handler that reacted to Escape this frame (cancel search edit,
        // cancel value edit, cancel key binding) so the same press doesn't ALSO
        // close the panel. Reset at the start of every Update.
        internal static bool EscConsumed;

        private const float PANEL_W    = 980f;
        private const float PANEL_H    = 600f;
        private const float TOP_H      = 68f;
        private const float BOT_H      = 24f;
        private const float SB_W       = 14f;
        private const float SIDEBAR_W  = 220f;
        private const float SIDEBAR_PAD= 8f;
        private const float SEARCH_H   = 32f;
        private const float MOD_HDR_H  = 56f; // mod info header in content area
        private const float VIEWPORT_H = PANEL_H - TOP_H - BOT_H - MOD_HDR_H;
        private const float SIDEBAR_LIST_H = PANEL_H - TOP_H - BOT_H - SEARCH_H - SIDEBAR_PAD * 2;
        private const float SCROLL_SPD = 40f;

        private bool _isPause, _isMainMenu, _panelOpen;
        private bool _updateErrLogged;

        // ── Floating MODS button (pause menu only since 1.6.0) ───────────────
        private RectTransform _modsBtnRt;
        private RectTransform _doneBtnRt;
        private GameObject    _overlay;
        private Image         _modsBtnImg;

        // ── Main-menu injected NetworkMenuButton clone ────────────────────────
        private GameObject _mainMenuModsBtn;

        // ── Content area (right) ─────────────────────────────────────────────
        private Transform     _scrollContent;
        private RectTransform _scrollContentRt;
        private RectTransform _scrollTrackRt;
        private RectTransform _scrollThumbRt;
        private GameObject    _scrollbarGo;

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

        private int _selectedMod = 0;
        private readonly Dictionary<int, RectTransform> _sidebarItemRects = new();
        private readonly Dictionary<int, Image> _sidebarItemBgs = new();

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
                var nm = NetworkMenu.Instance;
                string state = (nm != null && nm.CurrentState != null)
                    ? nm.CurrentState.GetIl2CppType().Name : "";
                _isPause    = state == "NetworkMenuPauseState";
                _isMainMenu = state == "NetworkMenuMainState";

                // Floating MODS button — pause menu only.
                bool showFloating = _isPause && !_panelOpen;
                if (_modsBtnRt != null)
                {
                    _modsBtnRt.gameObject.SetActive(showFloating);
                    if (showFloating) { PositionModsButton(); UpdateModsButtonHover(); }
                    else if (_modsBtnImg != null) _modsBtnImg.color = new Color(0, 0, 0, 0);
                }

                // Main menu — inject a real NetworkMenuButton into the game's Buttons Panel.
                // Hover/selection visuals come natively from NetworkMenuButton
                // (OnPointerEnter → SelectButton → m_menu.SetSelected → OnSelect).
                if (_isMainMenu && !_panelOpen)
                    EnsureMainMenuModsBtn();

                // DIAG: Shift+M — log who's under the cursor so we can debug missed clicks.
                if (_isMainMenu && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.M))
                    DiagnoseAtCursor();

                bool inMenu = _isPause || _isMainMenu;
                if (!inMenu && _panelOpen) DoClose();

                HandleClicks();

                if (_panelOpen)
                {
                    // Keep the underlying menu deselected — game's Update may try to re-select
                    // a button; if so, Enter would submit it. Force null every frame.
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null && es.currentSelectedGameObject != null)
                        es.SetSelectedGameObject(null);

                    EscConsumed = false;
                    UpdateThumbDrag();
                    HandleScroll();
                    HandleSearchInput();
                    foreach (var (_, entries) in POGConfig.GetAll())
                        foreach (var e in entries)
                            e.OnUpdate();

                    // Esc closes the panel — but only if no handler above already used
                    // this press to cancel an edit/binding (EscConsumed).
                    if (!_editingSearch && !KeyEntry.AnyWaiting && !EscConsumed
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

            float contentMax = Mathf.Max(0, _totalContentH - VIEWPORT_H);
            float sidebarMax = Mathf.Max(0, _sidebarTotalH - SIDEBAR_LIST_H);

            // Strict cursor-over-section gating so wheel scrolls only the panel under the cursor.
            // IL2Cpp won't allow (RectTransform)Transform — the C# wrapper type is Transform even
            // when the underlying Unity object IS a RectTransform. Use GetComponent<RectTransform>().
            var sidebarVpRt = (_sidebarListRt != null && _sidebarListRt.parent != null)
                ? _sidebarListRt.parent.GetComponent<RectTransform>() : null;
            var contentVpRt = (_scrollContentRt != null && _scrollContentRt.parent != null)
                ? _scrollContentRt.parent.GetComponent<RectTransform>() : null;
            bool overSidebar = sidebarVpRt != null && HitTest(sidebarVpRt, mp);
            bool overContent = contentVpRt != null && HitTest(contentVpRt, mp);

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
            float thumbH  = Mathf.Max(30f, VIEWPORT_H * VIEWPORT_H / _totalContentH);
            float thumbY  = -ratio * (VIEWPORT_H - thumbH);
            _scrollThumbRt.sizeDelta        = new Vector2(0, thumbH);
            _scrollThumbRt.anchoredPosition = new Vector2(0, thumbY);
        }

        // ── Clicks ────────────────────────────────────────────────────────────

        private void HandleClicks()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            Vector2 mp = Input.mousePosition;

            // Floating MODS button (pause only) — only when panel is closed
            if (!_panelOpen && _modsBtnRt != null && _modsBtnRt.gameObject.activeSelf && HitTest(_modsBtnRt, mp))
            {
                OpenPanel();
                return;
            }

            if (!_panelOpen) return;

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
                    SliderEntry.AnyInputActive = true;
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

            // Registered buttons (DONE, sidebar items, KeyEntry "Change", SliderEntry value boxes, etc.)
            foreach (var (rt, cb) in Clicks.Map)
            {
                if (rt == null) continue;
                if (HitTest(rt, mp)) { cb?.Invoke(); return; }
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
            float maxScroll = Mathf.Max(0, _totalContentH - VIEWPORT_H);
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
            float maxScroll = Mathf.Max(0, _totalContentH - VIEWPORT_H);
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
            SliderEntry.AnyInputActive = false;
            if (_searchBgImg != null) _searchBgImg.color = new Color(0.16f, 0.16f, 0.18f, 0.95f);
            UpdateSearchTextDisplay();
        }

        private void HandleSearchInput()
        {
            if (!_editingSearch) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EscConsumed = true;
                _editingSearch = false;
                SliderEntry.AnyInputActive = false;
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
            // Pause-menu position only. Main menu uses an injected NetworkMenuButton
            // (see EnsureMainMenuModsBtn) so the floating button never shows there.
            _modsBtnRt.anchorMin = _modsBtnRt.anchorMax = new Vector2(0.5f, 0.685f);
            _modsBtnRt.sizeDelta = new Vector2(260, 34);
            _modsBtnRt.anchoredPosition = Vector2.zero;
        }

        // ── Main-menu button injection ────────────────────────────────────────
        // Clones the game's START button so we get matching style, click sound,
        // hover highlight, and keyboard/gamepad navigation for free.
        // Hierarchy: PitOfGoblin_Menu3D ▸ Main Panel ▸ Buttons Panel (GridLayoutGroup).
        // We land at sibling index 1 (right after START).

        private void EnsureMainMenuModsBtn()
        {
            // Unity's overloaded == treats destroyed objects as null — covers scene reloads.
            if (_mainMenuModsBtn != null) return;

            var template = FindStartButton();
            if (template == null) return; // menu not fully built yet, retry next frame

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
                clone.name = "POGModsButton";

                var nmb = clone.GetComponent<NetworkMenuButton>();
                if (nmb == null) { UnityEngine.Object.Destroy(clone); return; }

                // Belt and braces in case Awake ran before the parent chain was active.
                if (nmb.m_menu == null)
                    nmb.m_menu = template.m_menu != null ? template.m_menu : NetworkMenu.Instance;

                if (nmb.m_buttonText != null) nmb.m_buttonText.text = "MODS";

                var btn = nmb.m_button;
                if (btn != null)
                {
                    // RemoveAllListeners only clears RUNTIME listeners. The prefab persistent
                    // listener (NetworkMenuMainState.OnStartButton) is serialized data and
                    // gets copied into our clone — clicking MODS would still fire OnStartButton.
                    // Disable each persistent entry explicitly.
                    btn.onClick.RemoveAllListeners();
                    int pcount = btn.onClick.GetPersistentEventCount();
                    for (int i = 0; i < pcount; i++)
                        btn.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

                    Action onClick = () =>
                    {
                        try { if (_panelOpen) DoClose(); else OpenPanel(); }
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

                clone.transform.SetSiblingIndex(1); // after START

                // Defensive — Instantiate should already preserve raycasts.
                var images = clone.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in images) img.raycastTarget = true;

                clone.SetActive(true);
                _mainMenuModsBtn = clone;

                // Force layout / canvas rebuild.
                var parent = template.transform.parent;
                var parentRt = parent as RectTransform;
                if (parentRt != null)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                Canvas.ForceUpdateCanvases();

                MelonLogger.Msg("[POGConfig] MODS button injected into main menu.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGConfig] EnsureMainMenuModsBtn: {ex.Message}");
            }
        }

        private static NetworkMenuButton FindStartButton()
        {
            var all = Resources.FindObjectsOfTypeAll<NetworkMenuButton>();
            foreach (var b in all)
            {
                if (b == null) continue;
                if (!b.gameObject.activeInHierarchy) continue;
                var tmp = b.m_buttonText;
                if (tmp == null) continue;
                string t = tmp.text;
                if (!string.IsNullOrEmpty(t) && t.Equals("START", StringComparison.OrdinalIgnoreCase))
                    return b;
            }
            return null;
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

        private void OpenPanel()
        {
            if (_builtRegistryVersion != POGConfig.RegistryVersion) _contentBuilt = false;
            EnsureContent();
            _overlay.SetActive(true);
            _panelOpen = true;
            PanelOpen  = true;
            POGConfig.PanelOpen = true;

            // Block keyboard / gamepad submit on the underlying menu by clearing selection.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
            {
                _savedSelected = es.currentSelectedGameObject;
                es.SetSelectedGameObject(null);
            }
        }

        internal void DoClose()
        {
            if (_overlay != null) _overlay.SetActive(false);
            _panelOpen = false;
            PanelOpen  = false;
            POGConfig.PanelOpen = false;
            KeyEntry.AnyWaiting = false;
            _scrollDragging = false;
            _sidebarDragging = false;

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && _savedSelected != null)
                es.SetSelectedGameObject(_savedSelected);
            _savedSelected = null;
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
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight  = 0.5f;
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
            var txt = UI.AddText(textGo, "MODS", _font, 20, Color.white, TextAlignmentOptions.Center);
            txt.fontStyle = FontStyles.Bold;
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
            vRt.sizeDelta = new Vector2(1, -(TOP_H + BOT_H));
            vRt.anchoredPosition = new Vector2(SIDEBAR_W + 16f, 0);
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

            return overlayGo;
        }

        private void BuildSidebar(Transform panelTr)
        {
            // Sidebar root (left strip below title)
            var sb = UI.Make("Sidebar", panelTr);
            var sbRt = UI.Rt(sb);
            sbRt.anchorMin = new Vector2(0, 0); sbRt.anchorMax = new Vector2(0, 1);
            sbRt.pivot = new Vector2(0, 0.5f);
            sbRt.sizeDelta = new Vector2(SIDEBAR_W, -(TOP_H + BOT_H));
            sbRt.anchoredPosition = new Vector2(16f, 0);

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

            var subGo = UI.Make("ModSub", hdr.transform);
            var sRt = UI.Rt(subGo);
            sRt.anchorMin = new Vector2(0, 1); sRt.anchorMax = new Vector2(1, 1);
            sRt.pivot = new Vector2(0, 1);
            sRt.sizeDelta = new Vector2(0, 22);
            sRt.anchoredPosition = new Vector2(0, -28);
            _modSubTmp = UI.AddText(subGo, "", _font, 12,
                                    new Color(0.7f, 0.7f, 0.72f), TextAlignmentOptions.TopLeft);
            _modSubTmp.enableWordWrapping = false;
            _modSubTmp.overflowMode = TextOverflowModes.Ellipsis;
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
            UI.AddImage(thumbGo, new Color(0.45f, 0.45f, 0.5f));

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

            // Clamp selection
            var mods = POGConfig.GetAllRecords();
            if (mods.Count == 0) _selectedMod = 0;
            else if (_selectedMod >= mods.Count) _selectedMod = mods.Count - 1;
            else if (_selectedMod < 0) _selectedMod = 0;

            RebuildSidebar();
            RebuildContent();
        }

        private void RebuildSidebar()
        {
            if (_sidebarList == null) return;

            for (int i = _sidebarList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_sidebarList.GetChild(i).gameObject);
            _sidebarItemRects.Clear();
            _sidebarItemBgs.Clear();

            var mods = POGConfig.GetAllRecords();
            const float ITEM_H = 36f;
            const float GAP    = 2f;
            float y = 4f;

            string q = (_searchQuery ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < mods.Count; i++)
            {
                var rec = mods[i];
                if (q.Length > 0 && !rec.Name.ToLowerInvariant().Contains(q)) continue;

                int idx = i; // capture
                var item = UI.Make("MI_" + rec.Name, _sidebarList);
                var iRt = UI.Rt(item);
                iRt.anchorMin = new Vector2(0, 1); iRt.anchorMax = new Vector2(1, 1);
                iRt.pivot = new Vector2(0.5f, 1f);
                iRt.sizeDelta = new Vector2(-4, ITEM_H);
                iRt.anchoredPosition = new Vector2(0, -y);

                bool isSel = idx == _selectedMod;
                var bg = UI.AddImage(item,
                    isSel ? new Color(1f, 0.82f, 0.3f, 0.18f) : new Color(0.12f, 0.12f, 0.14f, 0.7f));
                _sidebarItemBgs[idx] = bg;
                _sidebarItemRects[idx] = iRt;

                var lblGo = UI.Make("Label", item.transform);
                UI.Stretch(UI.Rt(lblGo), 10, 10, 0, 0);
                var lTmp = UI.AddText(lblGo, rec.Name, _font, 13,
                    isSel ? new Color(1f, 0.85f, 0.4f) : new Color(0.85f, 0.85f, 0.88f),
                    TextAlignmentOptions.MidlineLeft);
                if (isSel) lTmp.fontStyle = FontStyles.Bold;
                lTmp.enableWordWrapping = false;
                lTmp.overflowMode = TextOverflowModes.Ellipsis;

                Clicks.Register(iRt, () => SelectMod(idx));

                y += ITEM_H + GAP;
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

        private void SelectMod(int index)
        {
            if (index == _selectedMod) return;
            _selectedMod = index;
            RebuildSidebar();   // refresh selection highlight
            RebuildContent();
        }

        private void RebuildContent()
        {
            if (_scrollContent == null) return;

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

            var mods = POGConfig.GetAllRecords();
            if (mods.Count == 0)
            {
                if (_modTitleTmp != null) _modTitleTmp.text = "No mods registered";
                if (_modSubTmp != null) _modSubTmp.text = "Install a POGConfig-aware mod to see settings here.";
                _totalContentH = 0;
                if (_scrollContentRt != null) _scrollContentRt.sizeDelta = new Vector2(0, 0);
                if (_scrollbarGo != null) _scrollbarGo.SetActive(false);
                return;
            }

            var rec = mods[_selectedMod];
            if (_modTitleTmp != null) _modTitleTmp.text = rec.Name;
            if (_modSubTmp != null)
            {
                string sub = "";
                if (rec.Info != null)
                {
                    if (!string.IsNullOrEmpty(rec.Info.Version)) sub += "v" + rec.Info.Version;
                    if (!string.IsNullOrEmpty(rec.Info.Author))  sub += (sub.Length > 0 ? "  ·  " : "") + "by " + rec.Info.Author;
                    if (!string.IsNullOrEmpty(rec.Info.Description))
                        sub += (sub.Length > 0 ? "  ·  " : "") + rec.Info.Description;
                }
                _modSubTmp.text = sub;
            }

            const float ROW_H = 40f;
            float y = 4f;
            for (int j = 0; j < rec.Entries.Count; j++)
            {
                var entry = rec.Entries[j];
                float h = entry is HeaderEntry ? entry.Height : ROW_H;
                try
                {
                    var row = UI.Make("R_" + (entry.Label ?? "?"), _scrollContent);
                    UI.PlaceFromTop(UI.Rt(row), y, h);
                    entry.BuildRowInto(row, _font);
                }
                catch (Exception ex)
                { MelonLogger.Warning($"[POGConfig] Row '{entry.Label}': {ex.Message}"); }
                y += h + 2f;
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
            bool needsScroll = _totalContentH > VIEWPORT_H;
            if (_scrollbarGo != null) _scrollbarGo.SetActive(needsScroll);
            // Reset scrollbar thumb visual to top.
            if (needsScroll) UpdateScrollThumb(Mathf.Max(0, _totalContentH - VIEWPORT_H));
            // Cancel any in-progress drag from the previous mod's content.
            _scrollDragging = false;
        }
    }
}
