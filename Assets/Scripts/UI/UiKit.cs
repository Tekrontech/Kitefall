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
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
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
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
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
            Image img = Image(parent, color ?? Theme.Panel, Sprites.Rounded, name, raycast);
            return img;
        }

        public static Text Label(Transform parent, string text, int size = Theme.BodySize, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, string name = "Label")
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.fontStyle = style;
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
            Image img = Image(parent, color ?? Theme.Accent, Sprites.Rounded, name ?? ("Button " + text), true);
            var b = img.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.targetGraphic = img;
            if (!string.IsNullOrEmpty(text))
            {
                Text label = Label(img.transform, text, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.rectTransform.Stretch(8, 4, 8, 4);
                label.Shadowed();
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
                b.image.color = state ? Theme.Good : Theme.Disabled;
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
                    for (int k = 0; k < buttons.Count; k++) buttons[k].image.color = k == index ? Theme.Accent : Theme.PanelLight;
                    changed?.Invoke(index);
                }, i == selected ? Theme.Accent : Theme.PanelLight, Theme.SmallSize + 2);
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
                    for (int k = 0; k < list.Count; k++) list[k].image.color = k == index ? Theme.Accent : Theme.PanelLight;
                    changed?.Invoke(index);
                }, i == selected ? Theme.Accent : Theme.PanelLight, Theme.SmallSize + 2);
                list.Add(b);
            }
            return list;
        }

        public static string Coins(int n) => $"<color=#FFD24D>●</color> {n:N0}";
        public static string Gems(int n) => $"<color=#C77DFF>♦</color> {n:N0}";
    }
}
