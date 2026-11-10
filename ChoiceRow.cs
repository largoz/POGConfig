using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace POGMods.Config
{
    /// <summary>One item a <see cref="ChoiceRow"/> can be set to.</summary>
    public sealed class Option
    {
        /// <summary>What the setting stores. Stays the same when the label is reworded.</summary>
        public string Key { get; }
        /// <summary>What the player reads. Defaults to the key.</summary>
        public string Label { get; }
        /// <summary>One line under the label in the list. Optional.</summary>
        public string Description { get; set; }
        /// <summary>Words the list's filter buttons and search match on. Optional.</summary>
        public List<string> Tags { get; } = new List<string>();

        public Option(string key, string label = null, string description = null, params string[] tags)
        {
            Key = key ?? "";
            Label = string.IsNullOrEmpty(label) ? Key : label;
            Description = description;
            if (tags != null) Tags.AddRange(tags.Where(t => !string.IsNullOrEmpty(t)));
        }
    }

    /// <summary>
    /// A setting picked from a list. The row shows the current item; a click opens the list over the
    /// page, with search and filter buttons, that stays fast with thousands of items (only the visible
    /// ones are drawn). The value is the item's <see cref="Option.Key"/>, so adding, removing or
    /// reordering items never turns a saved choice into a different one.
    ///
    /// <code>
    /// new ChoiceRow("Difficulty", () => _diff, v => _diff = v, "Easy", "Normal", "Hard")
    /// new ChoiceRow("Font", () => _font, v => _font = v, () => FontOptions())   // list that changes in game
    ///     .Saved("Font").Searchable().Filters("Font mods", "Game fonts").Description("Font for the menu")
    /// </code>
    /// </summary>
    public class ChoiceRow : ConfigEntry
    {
        // Search appears by itself from this many items on; .Searchable() forces it.
        private const int AutoSearchFrom = 10;

        private readonly Func<string> _get;
        private Action<string> _set;
        private readonly Func<IReadOnlyList<Option>> _options;
        private string _prefKey;
        private bool? _searchable;
        private readonly List<string> _filters = new List<string>();
        private string _description;

        private TextMeshProUGUI _valueTmp;
        private RectTransform _buttonRt;
        private string _shownKey;
        private string _defaultKey;

        public ChoiceRow(string label, Func<string> get, Action<string> set, params string[] options)
            : this(label, get, set, (options ?? Array.Empty<string>()).Select(o => new Option(o)).ToList()) { }

        public ChoiceRow(string label, Func<string> get, Action<string> set, IEnumerable<Option> options)
            : this(label, get, set, Fixed(options)) { }

        /// <summary>A list that can change while the game runs; it is read each time it is shown.</summary>
        public ChoiceRow(string label, Func<string> get, Action<string> set, Func<IReadOnlyList<Option>> options) : base(label)
        {
            _get = get ?? (() => "");
            _set = set ?? (_ => { });
            _options = options ?? (() => Array.Empty<Option>());
        }

        private static Func<IReadOnlyList<Option>> Fixed(IEnumerable<Option> options)
        {
            var list = (options ?? Enumerable.Empty<Option>()).ToList();
            return () => list;
        }

        // ── Modules ─────────────────────────────────────────────────────────

        /// <summary>Keep the choice between game sessions (MelonPreferences, under this mod's category).</summary>
        public ChoiceRow Saved(string prefKey) { _prefKey = prefKey; return this; }
        /// <summary>Show the search field (it appears by itself from 10 items on).</summary>
        public ChoiceRow Searchable(bool on = true) { _searchable = on; return this; }
        /// <summary>Filter buttons over the list, one per tag; items carry tags in <see cref="Option.Tags"/>.</summary>
        public ChoiceRow Filters(params string[] tags) { _filters.Clear(); if (tags != null) _filters.AddRange(tags); return this; }
        /// <summary>A line of explanation under the row's label.</summary>
        public ChoiceRow Description(string text) { _description = text; return this; }

        // ── Row ─────────────────────────────────────────────────────────────

        // Label and button share the top line; a description spans the whole row under them and
        // wraps onto as many lines as it needs (MeasureHeight grows the row to fit).
        private const float TopLine = 36f;
        private TextMeshProUGUI _descTmp;

        public override float Height => string.IsNullOrEmpty(_description) ? 40f : 56f;

        internal override float MeasureHeight(float width, float placedHeight)
        {
            if (_descTmp == null) return placedHeight;
            float h = _descTmp.GetPreferredValues(_description, Mathf.Max(50f, width - 4f), 0f).y;
            return Mathf.Max(placedHeight, TopLine + h + 6f);
        }

        internal IReadOnlyList<Option> CurrentOptions()
        {
            try { return _options() ?? Array.Empty<Option>(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] '{Label}' options: {ex.Message}"); return Array.Empty<Option>(); }
        }

        private string LabelFor(string key)
        {
            var o = CurrentOptions().FirstOrDefault(x => x.Key == key);
            return o != null ? o.Label : (string.IsNullOrEmpty(key) ? "—" : key);
        }

        internal override void CaptureDefault() => _defaultKey = _get();
        internal override void RestoreDefault() { if (_defaultKey != null) Choose(_defaultKey); }
        internal override void EndInteraction() => ConfigBehaviour.Popup?.CloseIfOwner(this);

        internal override void BindPrefs(MelonPreferences_Category cat)
        {
            if (_prefKey == null) return;
            var pref = Prefs.Entry(cat, _prefKey, _get() ?? "");
            if (CurrentOptions().Any(o => o.Key == pref.Value)) _set(pref.Value);
            var orig = _set;
            _set = v => { orig(v); pref.Value = v; Prefs.MarkDirty(); };
        }

        internal override void BuildRowInto(GameObject row, TMP_FontAsset font)
        {
            bool hasDesc = !string.IsNullOrEmpty(_description);
            var lbl = UI.Make("Label", row.transform);
            var lRt = UI.Rt(lbl);
            if (hasDesc)
            {
                lRt.anchorMin = new Vector2(0f, 1f); lRt.anchorMax = new Vector2(0.46f, 1f);
                lRt.pivot = new Vector2(0f, 1f);
                lRt.offsetMin = new Vector2(0f, -TopLine); lRt.offsetMax = Vector2.zero;
            }
            else
            {
                lRt.anchorMin = Vector2.zero; lRt.anchorMax = new Vector2(0.46f, 1f);
                lRt.offsetMin = lRt.offsetMax = Vector2.zero;
            }
            var lTmp = UI.AddText(lbl, Label, font, (int)Theme.Defaults["font-size-label"], new Color(0.88f, 0.88f, 0.88f));
            lTmp.enableWordWrapping = false;
            lTmp.overflowMode = TextOverflowModes.Ellipsis;

            if (hasDesc)
            {
                var d = UI.Make("Description", row.transform);
                var dRt = UI.Rt(d);
                dRt.anchorMin = Vector2.zero; dRt.anchorMax = Vector2.one;
                dRt.offsetMin = new Vector2(0f, 2f); dRt.offsetMax = new Vector2(-4f, -TopLine);
                _descTmp = UI.AddText(d, _description, font, (int)Theme.Defaults["font-size-small"], new Color(0.6f, 0.6f, 0.64f),
                                      TextAlignmentOptions.TopLeft);
                _descTmp.enableWordWrapping = true;
                _descTmp.overflowMode = TextOverflowModes.Overflow;
            }

            var btn = UI.Make("Choice", row.transform);
            _buttonRt = UI.Rt(btn);
            if (hasDesc)
            {
                // Centred on the top line, with the label.
                _buttonRt.anchorMin = new Vector2(0.48f, 1f); _buttonRt.anchorMax = new Vector2(1f, 1f);
                _buttonRt.pivot = new Vector2(0.5f, 0.5f);
                _buttonRt.sizeDelta = new Vector2(0f, 28f);
                _buttonRt.anchoredPosition = new Vector2(0f, -TopLine * 0.5f);
            }
            else
            {
                _buttonRt.anchorMin = new Vector2(0.48f, 0.5f); _buttonRt.anchorMax = new Vector2(1f, 0.5f);
                _buttonRt.pivot = new Vector2(0.5f, 0.5f);
                _buttonRt.sizeDelta = new Vector2(0f, 28f);
                _buttonRt.anchoredPosition = Vector2.zero;
            }
            UI.AddImage(btn, new Color(0.16f, 0.16f, 0.18f, 0.95f));

            var vGo = UI.Make("Value", btn.transform);
            UI.Stretch(UI.Rt(vGo), 10f, 28f, 0f, 0f);
            _shownKey = _get();
            _valueTmp = UI.AddText(vGo, LabelFor(_shownKey), font, (int)Theme.Defaults["font-size-value"], new Color(1f, 0.82f, 0.3f));
            _valueTmp.enableWordWrapping = false;
            _valueTmp.overflowMode = TextOverflowModes.Ellipsis;

            var arrow = UI.Make("Arrow", btn.transform);
            var aRt = UI.Rt(arrow);
            aRt.anchorMin = new Vector2(1f, 0f); aRt.anchorMax = new Vector2(1f, 1f);
            aRt.pivot = new Vector2(1f, 0.5f);
            aRt.sizeDelta = new Vector2(24f, 0f);
            aRt.anchoredPosition = new Vector2(-4f, 0f);
            UI.AddText(arrow, "▼", font, 10, new Color(1f, 0.82f, 0.3f), TextAlignmentOptions.Center);

            Clicks.Register(_buttonRt, OpenList);
        }

        internal void OpenList()
        {
            var popup = ConfigBehaviour.Popup;
            if (popup == null) return;
            var options = CurrentOptions();
            bool search = _searchable ?? options.Count >= AutoSearchFrom;
            popup.Open(this, Label, options, _get(), search, _filters, Choose);
        }

        private void Choose(string key)
        {
            if (key == null) return;
            if (key != _get()) _set(key);
            Refresh();
        }

        private void Refresh()
        {
            _shownKey = _get();
            if (_valueTmp != null) _valueTmp.text = LabelFor(_shownKey);
        }

        internal override void OnUpdate()
        {
            if (_valueTmp == null) return;
            string key = _get();
            if (key != _shownKey) Refresh();
        }
    }

    /// <summary>
    /// The list a <see cref="ChoiceRow"/> opens, drawn over the panel. While it is open it takes all
    /// input: clicks, the wheel and the keyboard (typing searches; arrows, Page Up/Down, Home/End move;
    /// Enter picks; Esc or a click outside closes without a change). Only the rows in view exist, so a
    /// list of thousands costs the same as a list of ten.
    /// </summary>
    internal sealed class ChoicePopup
    {
        private const float BoxW = 640f;
        private const float Pad = 14f;
        private const float WheelStep = 40f;

        private readonly GameObject _root;
        private readonly RectTransform _boxRt;
        private readonly RectTransform _viewportRt;
        private readonly RectTransform _contentRt;
        private readonly RectTransform _chipsRt;
        private readonly TextMeshProUGUI _title, _search, _footer;
        private readonly TMP_FontAsset _font;
        private readonly float _boxH;

        private readonly List<RowView> _pool = new List<RowView>();
        private readonly List<(RectTransform rt, Image img, TextMeshProUGUI tmp, string tag)> _chips =
            new List<(RectTransform, Image, TextMeshProUGUI, string)>();
        private readonly HashSet<string> _activeTags = new HashSet<string>();

        private ChoiceRow _owner;
        private IReadOnlyList<Option> _all = Array.Empty<Option>();
        private readonly List<int> _shown = new List<int>();
        private string _selectedKey;
        private Action<string> _onChosen;
        private bool _searchable;
        private string _query = "";
        private float _rowH = 32f;
        private float _offset;
        private int _highlight = -1;
        private Vector2 _lastMouse;

        private sealed class RowView
        {
            public RectTransform Rt;
            public Image Bg;
            public TextMeshProUGUI Label, Desc, Mark;
            public int Shown = -1; // index into _shown
        }

        public bool IsOpen => _root != null && _root.activeSelf;

        public ChoicePopup(Transform panel, TMP_FontAsset font, float panelHeight)
        {
            _font = font;
            _boxH = Mathf.Max(240f, panelHeight - 70f);

            _root = UI.Make("ChoicePopup", panel);
            UI.Stretch(UI.Rt(_root));
            UI.AddImage(_root, new Color(0f, 0f, 0f, 0.55f));

            var box = UI.Make("Box", _root.transform);
            _boxRt = UI.Rt(box);
            _boxRt.anchorMin = _boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            _boxRt.sizeDelta = new Vector2(BoxW, _boxH);
            UI.AddImage(box, new Color(0.085f, 0.085f, 0.1f, 1f));
            var edge = UI.Make("Edge", box.transform);
            UI.PlaceFromTop(UI.Rt(edge), 0f, 2f);
            UI.AddImage(edge, new Color(1f, 0.82f, 0.3f, 0.8f), false);

            var t = UI.Make("Title", box.transform);
            UI.PlaceFromTop(UI.Rt(t), 10f, 26f, Pad, Pad);
            _title = UI.AddText(t, "", font, (int)Theme.Defaults["font-size-label"] + 2, new Color(1f, 0.82f, 0.3f));
            _title.fontStyle = FontStyles.Bold;
            _title.enableWordWrapping = false;
            _title.overflowMode = TextOverflowModes.Ellipsis;

            var s = UI.Make("Search", box.transform);
            UI.PlaceFromTop(UI.Rt(s), 42f, 30f, Pad, Pad);
            UI.AddImage(s, new Color(0.16f, 0.16f, 0.18f, 0.95f));
            var sText = UI.Make("Text", s.transform);
            UI.Stretch(UI.Rt(sText), 10f, 10f, 0f, 0f);
            _search = UI.AddText(sText, "", font, (int)Theme.Defaults["font-size-value"], Color.white);
            _search.enableWordWrapping = false;
            _search.overflowMode = TextOverflowModes.Ellipsis;

            var c = UI.Make("Filters", box.transform);
            _chipsRt = UI.Rt(c);
            UI.PlaceFromTop(_chipsRt, 80f, 26f, Pad, Pad);

            var vp = UI.Make("List", box.transform);
            _viewportRt = UI.Rt(vp);
            vp.AddComponent<RectMask2D>();
            var content = UI.Make("Content", vp.transform);
            _contentRt = UI.Rt(content);
            _contentRt.anchorMin = new Vector2(0f, 1f); _contentRt.anchorMax = new Vector2(1f, 1f);
            _contentRt.pivot = new Vector2(0.5f, 1f);
            _contentRt.offsetMin = _contentRt.offsetMax = Vector2.zero;

            var f = UI.Make("Footer", box.transform);
            var fRt = UI.Rt(f);
            fRt.anchorMin = new Vector2(0f, 0f); fRt.anchorMax = new Vector2(1f, 0f);
            fRt.pivot = new Vector2(0.5f, 0f);
            fRt.offsetMin = new Vector2(Pad, 6f); fRt.offsetMax = new Vector2(-Pad, 26f);
            _footer = UI.AddText(f, "", font, (int)Theme.Defaults["font-size-small"], new Color(0.55f, 0.55f, 0.6f));

            _root.SetActive(false);
        }

        public void Open(ChoiceRow owner, string title, IReadOnlyList<Option> options, string selectedKey,
                         bool searchable, IReadOnlyList<string> filterTags, Action<string> onChosen)
        {
            _owner = owner;
            _all = options ?? Array.Empty<Option>();
            _selectedKey = selectedKey;
            _onChosen = onChosen;
            _searchable = searchable;
            _query = "";
            _activeTags.Clear();
            _title.text = title;

            BuildChips(filterTags);
            bool hasChips = _chips.Count > 0;
            float listTop = (_searchable ? 80f : 42f) + (hasChips ? 34f : 0f);
            UI.Rt(_search.transform.parent.gameObject).gameObject.SetActive(_searchable);
            _chipsRt.gameObject.SetActive(hasChips);
            UI.PlaceFromTop(_chipsRt, _searchable ? 80f : 42f, 26f, Pad, Pad);
            _viewportRt.anchorMin = Vector2.zero; _viewportRt.anchorMax = Vector2.one;
            _viewportRt.offsetMin = new Vector2(Pad, 32f);
            _viewportRt.offsetMax = new Vector2(-Pad, -listTop);

            _rowH = _all.Any(o => !string.IsNullOrEmpty(o.Description)) ? 46f : 32f;
            BuildPool();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Refilter(keepSelected: true);
            UpdateSearchText();
            _lastMouse = Input.mousePosition;
        }

        public void CloseIfOwner(ChoiceRow row) { if (IsOpen && _owner == row) Close(); }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            _owner = null;
            _onChosen = null;
        }

        // ── Building ────────────────────────────────────────────────────────

        private void BuildChips(IReadOnlyList<string> tags)
        {
            foreach (var chip in _chips) UnityEngine.Object.Destroy(chip.rt.gameObject);
            _chips.Clear();
            if (tags == null) return;
            float x = 0f;
            foreach (var tag in tags)
            {
                var go = UI.Make("Chip_" + tag, _chipsRt.transform);
                var rt = UI.Rt(go);
                var img = UI.AddImage(go, new Color(0.16f, 0.16f, 0.18f, 0.95f));
                var tGo = UI.Make("Text", go.transform);
                UI.Stretch(UI.Rt(tGo), 10f, 10f, 0f, 0f);
                var tmp = UI.AddText(tGo, tag, _font, (int)Theme.Defaults["font-size-small"], new Color(0.8f, 0.8f, 0.82f), TextAlignmentOptions.Center);
                tmp.enableWordWrapping = false;
                float w = Mathf.Clamp(tmp.GetPreferredValues(tag).x + 24f, 60f, 260f);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(w, 24f);
                rt.anchoredPosition = new Vector2(x, 0f);
                x += w + 8f;
                _chips.Add((rt, img, tmp, tag));
            }
        }

        private void BuildPool()
        {
            float vpH = Mathf.Max(_rowH, _boxH - 160f);
            int need = Mathf.CeilToInt(vpH / _rowH) + 2;
            foreach (var v in _pool) UnityEngine.Object.Destroy(v.Rt.gameObject);
            _pool.Clear();
            for (int i = 0; i < need; i++)
            {
                var go = UI.Make("Item" + i, _contentRt.transform);
                var v = new RowView { Rt = UI.Rt(go) };
                v.Bg = UI.AddImage(go, new Color(0, 0, 0, 0));

                var mark = UI.Make("Mark", go.transform);
                var mRt = UI.Rt(mark);
                mRt.anchorMin = new Vector2(0f, 0f); mRt.anchorMax = new Vector2(0f, 1f);
                mRt.pivot = new Vector2(0f, 0.5f);
                mRt.sizeDelta = new Vector2(20f, 0f);
                mRt.anchoredPosition = new Vector2(4f, 0f);
                v.Mark = UI.AddText(mark, "", _font, (int)Theme.Defaults["font-size-label"], new Color(1f, 0.82f, 0.3f), TextAlignmentOptions.Center);

                bool desc = _rowH > 40f;
                var l = UI.Make("Label", go.transform);
                var lRt = UI.Rt(l);
                lRt.anchorMin = new Vector2(0f, desc ? 0.45f : 0f); lRt.anchorMax = Vector2.one;
                lRt.offsetMin = new Vector2(26f, 0f); lRt.offsetMax = new Vector2(-8f, 0f);
                v.Label = UI.AddText(l, "", _font, (int)Theme.Defaults["font-size-label"], new Color(0.9f, 0.9f, 0.9f),
                                     desc ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.MidlineLeft);
                v.Label.enableWordWrapping = false;
                v.Label.overflowMode = TextOverflowModes.Ellipsis;

                var d = UI.Make("Desc", go.transform);
                var dRt = UI.Rt(d);
                dRt.anchorMin = Vector2.zero; dRt.anchorMax = new Vector2(1f, 0.45f);
                dRt.offsetMin = new Vector2(26f, 2f); dRt.offsetMax = new Vector2(-8f, 0f);
                v.Desc = UI.AddText(d, "", _font, (int)Theme.Defaults["font-size-small"], new Color(0.58f, 0.58f, 0.62f),
                                    TextAlignmentOptions.TopLeft);
                v.Desc.enableWordWrapping = false;
                v.Desc.overflowMode = TextOverflowModes.Ellipsis;
                d.SetActive(desc);
                _pool.Add(v);
            }
        }

        // ── Filtering and drawing ───────────────────────────────────────────

        private bool Matches(Option o)
        {
            if (_activeTags.Count > 0 && !o.Tags.Any(_activeTags.Contains)) return false;
            if (string.IsNullOrEmpty(_query)) return true;
            string q = _query.ToLowerInvariant();
            return (o.Label ?? "").ToLowerInvariant().Contains(q)
                || (o.Key ?? "").ToLowerInvariant().Contains(q)
                || (o.Description ?? "").ToLowerInvariant().Contains(q)
                || o.Tags.Any(t => t.ToLowerInvariant().Contains(q));
        }

        private void Refilter(bool keepSelected)
        {
            _shown.Clear();
            for (int i = 0; i < _all.Count; i++)
                if (Matches(_all[i])) _shown.Add(i);
            int sel = keepSelected ? _shown.FindIndex(i => _all[i].Key == _selectedKey) : -1;
            _highlight = sel >= 0 ? sel : (_shown.Count > 0 ? 0 : -1);
            _offset = 0f;
            if (_highlight >= 0) ScrollTo(_highlight, center: true);
            foreach (var c in _chips)
            {
                bool on = _activeTags.Contains(c.tag);
                c.img.color = on ? new Color(0.36f, 0.28f, 0.08f) : new Color(0.16f, 0.16f, 0.18f, 0.95f);
                c.tmp.color = on ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.8f, 0.82f);
            }
            Draw();
        }

        private float ViewportH => Mathf.Max(1f, _viewportRt.rect.height);
        private float MaxOffset => Mathf.Max(0f, _shown.Count * _rowH - ViewportH);

        private void ScrollTo(int shownIndex, bool center = false)
        {
            float top = shownIndex * _rowH, bottom = top + _rowH, h = ViewportH;
            if (center) _offset = top - (h - _rowH) * 0.5f;
            else if (top < _offset) _offset = top;
            else if (bottom > _offset + h) _offset = bottom - h;
            _offset = Mathf.Clamp(_offset, 0f, MaxOffset);
        }

        private void Draw()
        {
            int first = Mathf.Max(0, Mathf.FloorToInt(_offset / _rowH));
            for (int p = 0; p < _pool.Count; p++)
            {
                var v = _pool[p];
                int si = first + p;
                if (si >= _shown.Count) { v.Rt.gameObject.SetActive(false); v.Shown = -1; continue; }
                v.Rt.gameObject.SetActive(true);
                v.Shown = si;
                UI.PlaceFromTop(v.Rt, si * _rowH - _offset, _rowH - 2f);
                var o = _all[_shown[si]];
                bool selected = o.Key == _selectedKey;
                v.Label.text = o.Label;
                v.Label.color = selected ? new Color(1f, 0.82f, 0.3f) : new Color(0.9f, 0.9f, 0.9f);
                v.Desc.text = o.Description ?? "";
                v.Mark.text = selected ? "•" : "";
                v.Bg.color = si == _highlight ? new Color(0.24f, 0.2f, 0.1f, 1f) : new Color(1f, 1f, 1f, 0.03f);
            }
            _footer.text = _shown.Count == _all.Count
                ? $"{_all.Count} items   ·   Up/Down, Enter to pick, Esc to close"
                : $"{_shown.Count} of {_all.Count} items   ·   Up/Down, Enter to pick, Esc to close";
        }

        private void UpdateSearchText()
        {
            if (!_searchable) return;
            _search.text = _query.Length > 0 ? _query + "|" : "<color=#8c8c94>Type to search…</color>";
        }

        // ── Input (ConfigBehaviour calls this every frame while the list is open) ──

        public void Tick()
        {
            if (!IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { ConfigBehaviour.ConsumeEsc(); Close(); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { PickHighlighted(); return; }

            int move = 0;
            if (Input.GetKeyDown(KeyCode.DownArrow)) move = 1;
            else if (Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
            else if (Input.GetKeyDown(KeyCode.PageDown)) move = Mathf.Max(1, Mathf.FloorToInt(ViewportH / _rowH) - 1);
            else if (Input.GetKeyDown(KeyCode.PageUp)) move = -Mathf.Max(1, Mathf.FloorToInt(ViewportH / _rowH) - 1);
            else if (Input.GetKeyDown(KeyCode.Home)) move = int.MinValue;
            else if (Input.GetKeyDown(KeyCode.End)) move = int.MaxValue;
            if (move != 0 && _shown.Count > 0)
            {
                long target = move == int.MinValue ? 0 : move == int.MaxValue ? _shown.Count - 1 : (long)Math.Max(0, _highlight) + move;
                MoveHighlight((int)Math.Max(0, Math.Min(_shown.Count - 1, target)));
            }

            if (_searchable)
            {
                bool changed = false;
                foreach (char ch in Input.inputString)
                {
                    if (ch == '\b') { if (_query.Length > 0) { _query = _query.Substring(0, _query.Length - 1); changed = true; } }
                    else if (ch == '\n' || ch == '\r') { }
                    else if (ch >= ' ') { _query += ch; changed = true; }
                }
                if (changed) SetQuery(_query);
            }

            Vector2 mp = Input.mousePosition;
            bool overList = ConfigBehaviour.HitTest(_viewportRt, mp);
            float wheel = Input.mouseScrollDelta.y;
            if (overList && Mathf.Abs(wheel) > 0.001f)
            {
                _offset = Mathf.Clamp(_offset - wheel * WheelStep, 0f, MaxOffset);
                Draw();
            }
            if (overList && (mp - _lastMouse).sqrMagnitude > 1f)
            {
                var v = RowAt(mp);
                if (v != null && v.Shown != _highlight) { _highlight = v.Shown; Draw(); }
            }
            _lastMouse = mp;

            if (!Input.GetMouseButtonDown(0)) return;
            if (!ConfigBehaviour.HitTest(_boxRt, mp)) { Close(); return; }
            foreach (var c in _chips)
            {
                if (!ConfigBehaviour.HitTest(c.rt, mp)) continue;
                if (!_activeTags.Remove(c.tag)) _activeTags.Add(c.tag);
                Refilter(keepSelected: true);
                return;
            }
            if (overList)
            {
                var v = RowAt(mp);
                if (v != null) { _highlight = v.Shown; PickHighlighted(); }
            }
        }

        private RowView RowAt(Vector2 mp) =>
            _pool.FirstOrDefault(v => v.Shown >= 0 && v.Rt.gameObject.activeSelf && ConfigBehaviour.HitTest(v.Rt, mp));

        // Also used by test drivers, which cannot type or click.
        internal void SetQuery(string query)
        {
            _query = query ?? "";
            UpdateSearchText();
            Refilter(keepSelected: false);
        }

        internal void MoveHighlight(int shownIndex)
        {
            if (_shown.Count == 0) return;
            _highlight = Mathf.Clamp(shownIndex, 0, _shown.Count - 1);
            ScrollTo(_highlight);
            Draw();
        }

        internal void ToggleFilter(string tag)
        {
            if (!_activeTags.Remove(tag)) _activeTags.Add(tag);
            Refilter(keepSelected: true);
        }

        internal void PickHighlighted()
        {
            if (_highlight < 0 || _highlight >= _shown.Count) return;
            string key = _all[_shown[_highlight]].Key;
            var cb = _onChosen;
            Close();
            try { cb?.Invoke(key); }
            catch (Exception ex) { MelonLogger.Warning($"[POGConfig] choice '{key}': {ex.Message}"); }
        }
    }
}
