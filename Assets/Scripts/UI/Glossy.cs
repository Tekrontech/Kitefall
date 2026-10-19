using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// The "glossy festival" UI style from the owner's home-screen design: deep navy glass panels with a bright blue rim,
    /// gold accents and orange call-to-action buttons. Sprites are generated in code (9-sliced), so no image files are
    /// needed; an artist sprite named ui_panel / ui_button_blue / ui_button_orange … (Image Mapper) replaces them.
    /// </summary>
    public static class Glossy
    {
        public enum Style { Panel, Menu, Selected, Orange, Pill, Locked, Green, Play }

        private static readonly Dictionary<Style, Sprite> Cache = new Dictionary<Style, Sprite>();

        public static readonly Color Gold = Theme.Hex("#FFC93C");
        public static readonly Color SkyBlue = Theme.Hex("#5DB8FF");
        public static readonly Color BarTrack = Theme.Hex("#0A1630");
        public static readonly Color BarFill = Theme.Hex("#2F9BFF");

        public static Sprite Sprite(Style s)
        {
            if (Cache.TryGetValue(s, out Sprite sp) && sp != null) return sp;
            Sprite art = Art.Sprite(ArtKey(s));
            sp = art != null ? art : Make(s);
            Cache[s] = sp;
            return sp;
        }

        private static string ArtKey(Style s)
        {
            switch (s)
            {
                case Style.Panel: return "ui_panel";
                case Style.Menu: return "ui_button_blue";
                case Style.Selected: return "ui_button_selected";
                case Style.Orange: return "ui_button_orange";
                case Style.Pill: return "ui_pill";
                case Style.Locked: return "ui_button_locked";
                case Style.Play: return "ui_button_play";
                default: return "ui_button_green";
            }
        }

        private static Sprite Make(Style s)
        {
            switch (s)
            {
                case Style.Panel: return Rounded(96, 22, "#16335E", "#0A1830", "#3F8FE0", 3f, 0.10f, 0.93f);
                case Style.Menu: return Rounded(96, 20, "#1C4A86", "#0D2550", "#4FA8FF", 3f, 0.16f, 0.95f);
                case Style.Selected: return Rounded(96, 20, "#3DA0FF", "#1559C8", "#BFE4FF", 3.5f, 0.28f, 1f);
                case Style.Orange: return Rounded(128, 30, "#FFD34A", "#F06A0F", "#FFE9A8", 4f, 0.32f, 1f);
                case Style.Pill: return Rounded(64, 30, "#0F2347", "#081530", "#2E6BB5", 2f, 0.06f, 0.95f);
                case Style.Locked: return Rounded(96, 20, "#2A3550", "#161E33", "#4C5A78", 3f, 0.08f, 0.95f);
                case Style.Play: return Make(Style.Orange);
                default: return Rounded(96, 20, "#4FD08A", "#1E8F55", "#C8FFD9", 3f, 0.25f, 1f);
            }
        }

        /// <summary>Rounded box: vertical gradient, rim, soft top gloss. Returned as a 9-slice sprite.</summary>
        private static Sprite Rounded(int size, int radius, string top, string bottom, string rim, float rimWidth, float gloss, float alpha)
        {
            Color ct = Theme.Hex(top), cb = Theme.Hex(bottom), cr = Theme.Hex(rim);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "glossy" };
            var px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius), qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius; // <0 inside
                    float cover = Mathf.Clamp01(0.5f - d);
                    if (cover <= 0f) { px[y * size + x] = Color.clear; continue; }
                    float v = y / (size - 1f); // 0 bottom … 1 top
                    Color c = Color.Lerp(cb, ct, v);
                    if (v > 0.55f) c = Color.Lerp(c, Color.white, gloss * Mathf.InverseLerp(0.55f, 1f, v) * 0.6f); // gloss band
                    float rimT = Mathf.Clamp01((d + rimWidth + 0.5f));
                    c = Color.Lerp(c, cr, rimT);
                    c.a = cover * Mathf.Lerp(alpha, 1f, rimT);
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = radius + rimWidth + 2f;
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static Image Panel(Transform parent, Style style = Style.Panel, string name = "Panel", bool raycast = true)
        {
            Image img = UiKit.Image(parent, Color.white, Sprite(style), name, raycast);
            img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>Icon + label (+ optional chevron) button in the glossy style.</summary>
        public static Button Button(Transform parent, string text, Sprite icon, Action onClick, Style style = Style.Menu, int fontSize = 30,
            bool chevron = false, Color? iconColor = null, string sub = null)
        {
            Image bg = Panel(parent, style, "Btn_" + text);
            var btn = bg.gameObject.AddComponent<Button>();
            UiMotion.Bounce(btn);
            ColorBlock cb = btn.colors;
            cb.pressedColor = new Color(0.8f, 0.85f, 0.95f);
            cb.highlightedColor = new Color(1.05f, 1.05f, 1.1f);
            btn.colors = cb;
            if (onClick != null)
                btn.onClick.AddListener(() =>
                {
                    UiKit.ClickSound?.Invoke();
                    try { onClick(); }
                    catch (Exception e) { Debug.LogException(e); }
                });
            float left = 24f;
            if (icon != null)
            {
                Image ic = UiKit.Image(bg.transform, iconColor ?? Color.white, icon, "Icon");
                ic.preserveAspect = true;
                ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(fontSize * 1.5f, fontSize * 1.5f));
                left = 30f + fontSize * 1.6f;
            }
            string label = sub == null ? text : $"{text}\n<size={Mathf.RoundToInt(fontSize * 0.7f)}><color=#FFFFFFAA>{sub}</color></size>";
            Text t = UiKit.Label(bg.transform, label, fontSize, Color.white, icon != null || chevron ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            t.rectTransform.Stretch(left, 2, chevron ? 50 : 16, 2);
            if (chevron)
            {
                Image ch = UiKit.Image(bg.transform, new Color(1f, 1f, 1f, 0.75f), Icons.Get("chevron_right"), "Chevron");
                ch.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(26, 26));
            }
            return btn;
        }

        /// <summary>Thin rounded progress bar (track + blue fill).</summary>
        public static Image Bar(Transform parent, float value01, Color? fill = null)
        {
            Image track = UiKit.Image(parent, BarTrack, Sprites.Rounded, "Track");
            track.type = Image.Type.Sliced;
            Image f = UiKit.Image(track.transform, fill ?? BarFill, Sprites.Rounded, "Fill");
            f.type = Image.Type.Sliced;
            f.rectTransform.anchorMin = Vector2.zero;
            f.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            f.rectTransform.offsetMin = Vector2.zero;
            f.rectTransform.offsetMax = Vector2.zero;
            f.gameObject.SetActive(value01 > 0.001f);
            return track;
        }
    }
}
