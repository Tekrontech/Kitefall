using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// Builds uGUI elements from code (milestone 9/12 UI). Every screen uses these helpers,
    /// so there are no prefabs or scene wiring to break, and the look stays consistent.
    /// </summary>
    public static class UiKit
    {
        public static Action ClickSound;
        private static Font _font, _bold;

        /// <summary>
        /// One font family everywhere: Inter (SIL Open Font License, Assets/Resources/Fonts) – SemiBold for text, ExtraBold for
        /// titles and buttons. Falls back to Unity's built-in font if the files are missing.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/Inter-SemiBold");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        /// <summary>The heavy cut of the same family (used for FontStyle.Bold instead of a synthetic bold).</summary>
        public static Font BoldFont
        {
            get
            {
                if (_bold == null) _bold = Resources.Load<Font>("Fonts/Inter-ExtraBold");
                return _bold != null ? _bold : Font;
            }
        }

        private static Font _display, _cond;

        /// <summary>
        /// Sky Ops display face: Barlow Condensed ExtraBold Italic (SIL OFL, Assets/Resources/Fonts) – screen titles, PLAY,
        /// big numbers. Falls back to the bold body face.
        /// </summary>
        public static Font DisplayFont
        {
            get
            {
                if (_display == null) _display = Resources.Load<Font>("Fonts/BarlowCondensed-ExtraBoldItalic");
                return _display != null ? _display : BoldFont;
            }
        }

        /// <summary>Barlow Condensed Bold (upright) – button labels, tabs, small caps labels.</summary>
        public static Font CondFont
        {
            get
            {
                if (_cond == null) _cond = Resources.Load<Font>("Fonts/BarlowCondensed-Bold");
                return _cond != null ? _cond : BoldFont;
            }
        }

        /// <summary>Condensed faces look smaller than Inter at the same point size; this keeps the visual size.</summary>
        public const float CondScale = 1.14f;

        /// <summary>Upper-cases text but leaves rich-text tags (&lt;color=#…&gt;, &lt;size=…&gt;) untouched.</summary>
        public static string Caps(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf('<') < 0) return s.ToUpperInvariant();
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<') tag = true;
                sb.Append(tag ? ch : char.ToUpperInvariant(ch));
                if (ch == '>') tag = false;
            }
            return sb.ToString();
        }

        /// <summary>Button label face: condensed bold, or the italic display face for calls to action.</summary>
        public static Text UseButtonFont(Text t, bool callToAction)
        {
            if (t == null) return t;
            Font f = callToAction ? DisplayFont : CondFont;
            if (f == t.font) return t;
            // Label() already enlarged big bold text for the display face – only scale when coming from the Inter faces.
            bool alreadyCondensed = (_display != null && t.font == _display) || (_cond != null && t.font == _cond);
            bool scaled = f != BoldFont && f != Font && !alreadyCondensed;
            t.font = f;
            t.fontStyle = FontStyle.Normal;
            if (scaled) t.fontSize = Mathf.RoundToInt(t.fontSize * CondScale);
            return t;
        }

        /// <summary>Screen / panel title: display face, upper case.</summary>
        public static Text Display(Text t)
        {
            if (t == null) return t;
            if (t.font != DisplayFont && DisplayFont != BoldFont) t.fontSize = Mathf.RoundToInt(t.fontSize * CondScale);
            t.font = DisplayFont;
            t.fontStyle = FontStyle.Normal;
            t.text = Caps(t.text);
            return t;
        }

        /// <summary>Light 1 px lift for dark text on bright buttons.</summary>
        public static Text SoftShadow(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(1f, 1f, 1f, 0.35f);
            s.effectDistance = new Vector2(0f, -1.5f);
            return t;
        }

        // ---- Rect helpers ----------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Band(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static LayoutElement Size(this Component c, float width = -1, float height = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        // ---- Visuals ---------------------------------------------------------

        public static Image Image(Transform parent, Color color, Sprite sprite = null, string name = "Image", bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, Color? color = null, string name = "Panel", bool raycast = true)
        {
            Color c = color ?? Theme.Panel;
            // Standard panels and cards use the glossy navy glass of the home design; custom tints keep the plain look.
            bool glossy = Same(c, Theme.Panel) || Same(c, Theme.Card) || Same(c, Theme.CardSelected);
            Image img = glossy
                ? Image(parent, Same(c, Theme.CardSelected) ? new Color(0.75f, 0.9f, 1.25f) : Color.white, Glossy.Sprite(Glossy.Style.Panel), name, raycast)
                : Image(parent, c, Sprites.Rounded, name, raycast);
            return img;
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.03f;

        /// <summary>Theme colour → Sky Ops button skin (amber call-to-action, slate menu, cyan selected, green confirm, red danger).</summary>
        public static Glossy.Style? SkinStyle(Color c)
        {
            if (Same(c, Theme.Accent)) return Glossy.Style.Orange;
            if (Same(c, Theme.Good)) return Glossy.Style.Green;
            if (Same(c, Theme.Bad)) return Glossy.Style.Red;
            if (Same(c, Theme.Accent2) || Same(c, Theme.CardSelected)) return Glossy.Style.Selected;
            if (Same(c, Theme.PanelLight) || Same(c, Theme.Card) || Same(c, Theme.Panel) || Same(c, new Color(0.12f, 0.17f, 0.28f))) return Glossy.Style.Menu;
            if (Same(c, Theme.Disabled)) return Glossy.Style.Locked;
            return null;
        }

        private static Sprite ButtonSkin(Color c, out Color tint)
        {
            tint = Color.white;
            Glossy.Style? s = SkinStyle(c);
            if (s.HasValue) return Glossy.Sprite(s.Value);
            tint = c;
            return Sprites.Rounded;
        }

        /// <summary>
        /// Re-skins a button made by <see cref="Button"/> for a new theme colour (selected tab, ON/OFF…): swaps the sprite,
        /// the tint and the label colour. (Before: only the tint changed, so an orange skin was darkened instead of swapped.)
        /// </summary>
        public static void SetSkin(Button b, Color themeColor)
        {
            if (b == null) return;
            Sprite skin = ButtonSkin(themeColor, out Color tint);
            b.image.sprite = skin;
            b.image.type = UnityEngine.UI.Image.Type.Sliced;
            b.image.color = tint;
            Text label = b.ButtonLabel();
            if (label == null) return;
            Glossy.Style? s = SkinStyle(themeColor);
            label.color = s.HasValue ? Glossy.TextOn(s.Value) : Theme.Text;
            Shadow sh = label.GetComponent<Shadow>();
            if (sh != null) sh.effectColor = label.color == Theme.Text ? new Color(0f, 0f, 0f, 0.7f) : new Color(1f, 1f, 1f, 0.35f);
        }

        public static Text Label(Transform parent, string text, int size = Theme.BodySize, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, string name = "Label")
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            bool realBold = style == FontStyle.Bold && BoldFont != Font;
            // Big bold text (titles, scores, prices ≥ 40) uses the condensed italic display face of the Sky Ops look.
            bool display = style == FontStyle.Bold && size >= 40 && DisplayFont != BoldFont;
            t.font = display ? DisplayFont : realBold ? BoldFont : Font;
            if (display) { size = Mathf.RoundToInt(size * CondScale); realBold = true; }
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.fontStyle = realBold ? FontStyle.Normal : style;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Shadowed(this Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.7f);
            s.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        // ---- Buttons ---------------------------------------------------------

        public static Button Button(Transform parent, string text, Action onClick, Color? color = null, int fontSize = Theme.BodySize, string name = null)
        {
            Sprite skin = ButtonSkin(color ?? Theme.Accent, out Color tint);
            Image img = Image(parent, tint, skin, name ?? ("Button " + text), true);
            var b = img.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.disabledColor = Color.white; // DisabledSkin swaps to the grey "locked" skin instead of darkening (orange went brown)
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.targetGraphic = img;
            img.gameObject.AddComponent<DisabledSkin>();
            if (!string.IsNullOrEmpty(text))
            {
                Glossy.Style? style = SkinStyle(color ?? Theme.Accent);
                Color ink = style.HasValue ? Glossy.TextOn(style.Value) : Color.white;
                Text label = Label(img.transform, text.Length <= 3 ? text : Caps(text), fontSize, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                UseButtonFont(label, style.HasValue && Glossy.IsCallToAction(style.Value) && fontSize >= 30);
                label.rectTransform.Stretch(8, 4, 8, 4);
                if (ink == Theme.Text || ink == Color.white) label.Shadowed();
                else SoftShadow(label);
            }
            if (onClick != null)
                b.onClick.AddListener(() =>
                {
                    ClickSound?.Invoke();
                    try { onClick(); }
                    catch (Exception e) { Debug.LogException(e); }
                });
            return b;
        }

        public static Text ButtonLabel(this Button b) => b.GetComponentInChildren<Text>();

        /// <summary>Item picture (Image Mapper art) at the left of a plain button; a null icon keeps the text-only button.</summary>
        public static Button WithIcon(this Button b, Sprite icon, float size = 46f)
        {
            if (icon == null) return b;
            Image ic = Image(b.transform, Color.white, icon, "Icon");
            ic.type = UnityEngine.UI.Image.Type.Simple;
            ic.preserveAspect = true;
            ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(size, size));
            Text label = b.ButtonLabel();
            if (label != null) label.rectTransform.Stretch(size + 12, 4, 8, 4);
            return b;
        }

        public static Button Interactable(this Button b, bool on)
        {
            b.interactable = on;
            return b;
        }

        // ---- Layout ----------------------------------------------------------

        public static RectTransform Row(Transform parent, float spacing = 12f, int padding = 0, TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = false, string name = "Row")
        {
            RectTransform rt = Rect(name, parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            return rt;
        }

        public static RectTransform Column(Transform parent, float spacing = 12f, int padding = 0, TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true, string name = "Column")
        {
            RectTransform rt = Rect(name, parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return rt;
        }

        public static RectTransform Grid(Transform parent, Vector2 cell, Vector2 spacing, int columns, string name = "Grid")
        {
            RectTransform rt = Rect(name, parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            g.childAlignment = TextAnchor.UpperLeft;
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        public static RectTransform Spacer(Transform parent, float width = -1, float height = -1, float flex = 1)
        {
            RectTransform rt = Rect("Spacer", parent);
            rt.Size(width, height, flex, flex);
            return rt;
        }

        /// <summary>Scrolling list. Returns the scroll rect; put items into <paramref name="content"/>.</summary>
        public static ScrollRect Scroll(Transform parent, out RectTransform content, bool vertical = true, float spacing = 12f, int padding = 8)
        {
            Image bg = Image(parent, new Color(0, 0, 0, 0.001f), null, "Scroll", true);
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("Viewport", bg.transform).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            content = vertical ? Column(viewport, spacing, padding) : Row(viewport, spacing, padding);
            content.name = "Content";
            if (vertical)
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
                content.offsetMin = content.offsetMax = Vector2.zero;
            }
            else
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
                content.offsetMin = content.offsetMax = Vector2.zero;
                content.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            }
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            if (vertical) fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            else fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = !vertical;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        // ---- Inputs ----------------------------------------------------------

        public static InputField Input(Transform parent, string placeholder, string value, int charLimit, Action<string> onChanged)
        {
            Image bg = Image(parent, Theme.PanelLight, Sprites.Rounded, "Input", true);
            var field = bg.gameObject.AddComponent<InputField>();
            Text text = Label(bg.transform, "", Theme.BodySize, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, "Text");
            text.rectTransform.Stretch(18, 6, 18, 6);
            text.supportRichText = false;
            Text ph = Label(bg.transform, placeholder, Theme.BodySize, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic, "Placeholder");
            ph.rectTransform.Stretch(18, 6, 18, 6);
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = charLimit;
            field.text = value ?? "";
            if (onChanged != null) field.onValueChanged.AddListener(v => onChanged(v));
            return field;
        }

        /// <summary>"Label   [−] value [+]" row.</summary>
        public static Text Stepper(Transform parent, string label, string value, Action minus, Action plus)
        {
            RectTransform row = Row(parent, 12f);
            row.Size(-1, 64);
            Label(row, label, Theme.BodySize).Size(-1, 60, 1);
            Button(row, "−", minus, Theme.PanelLight, 34).Size(70, 60);
            Text v = Label(row, value, Theme.BodySize, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.Size(170, 60);
            Button(row, "+", plus, Theme.PanelLight, 34).Size(70, 60);
            return v;
        }

        /// <summary>"Label   [ON/OFF]" row.</summary>
        public static Button Toggle(Transform parent, string label, bool value, Action<bool> changed)
        {
            RectTransform row = Row(parent, 12f);
            row.Size(-1, 64);
            Label(row, label, Theme.BodySize).Size(-1, 60, 1);
            bool state = value;
            Button b = null;
            b = Button(row, state ? "ON" : "OFF", () =>
            {
                state = !state;
                b.ButtonLabel().text = state ? "ON" : "OFF";
                SetSkin(b, state ? Theme.Good : Theme.Disabled);
                changed?.Invoke(state);
            }, state ? Theme.Good : Theme.Disabled, Theme.BodySize);
            b.Size(150, 60);
            return b;
        }

        /// <summary>Choice row with buttons; the selected one is highlighted.</summary>
        public static void Choice(Transform parent, string label, string[] options, int selected, Action<int> changed)
        {
            RectTransform row = Row(parent, 10f);
            row.Size(-1, 64);
            Label(row, label, Theme.BodySize).Size(260, 60);
            var buttons = new List<Button>();
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button b = Button(row, options[i], () =>
                {
                    for (int k = 0; k < buttons.Count; k++) SetSkin(buttons[k], k == index ? Theme.Accent2 : Theme.PanelLight);
                    changed?.Invoke(index);
                }, i == selected ? Theme.Accent2 : Theme.PanelLight, Theme.SmallSize + 2);
                b.Size(-1, 60, 1);
                buttons.Add(b);
            }
        }

        /// <summary>Horizontal progress bar. Returns the fill image (set its rectTransform.anchorMax.x to the value).</summary>
        public static Image Bar(Transform parent, float value01, Color color, float height = 14f)
        {
            Image bg = Image(parent, new Color(1, 1, 1, 0.12f), Sprites.RoundedSmall, "Bar");
            bg.Size(-1, height, 1);
            Image fill = Image(bg.transform, color, Sprites.RoundedSmall, "Fill");
            fill.rectTransform.Band(Vector2.zero, new Vector2(Mathf.Clamp01(value01), 1f), Vector2.zero, Vector2.zero);
            return fill;
        }

        public static void SetBar(Image fill, float value01)
        {
            RectTransform rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
        }

        /// <summary>Stat line "Speed ■■■□□".</summary>
        public static void StatBar(Transform parent, string label, float value, float max, Color color)
        {
            RectTransform row = Row(parent, 10f);
            row.Size(-1, 34);
            Label(row, label, Theme.SmallSize, Theme.TextDim).Size(170, 30);
            Bar(row, value / max, color, 14f).transform.parent.GetComponent<RectTransform>();
        }

        /// <summary>Tabs bar; returns the buttons.</summary>
        public static List<Button> Tabs(Transform parent, string[] names, int selected, Action<int> changed)
        {
            RectTransform row = Row(parent, 8f, 0, TextAnchor.MiddleLeft, true);
            row.Size(-1, 70);
            if (parent.GetComponent<LayoutGroup>() == null) row.Stretch();
            var list = new List<Button>();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Button b = Button(row, names[i], () =>
                {
                    for (int k = 0; k < list.Count; k++) SetSkin(list[k], k == index ? Theme.Accent2 : Theme.PanelLight);
                    changed?.Invoke(index);
                }, i == selected ? Theme.Accent2 : Theme.PanelLight, Theme.SmallSize + 2);
                list.Add(b);
            }
            return list;
        }

        public static string Coins(int n) => $"<color=#FFD24D>●</color> {n:N0}";
        public static string Gems(int n) => $"<color=#3FB6FF>♦</color> {n:N0}";
    }
}
