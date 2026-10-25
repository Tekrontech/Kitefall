using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// "Sky Ops" UI style (v1.5, Free Fire-class menus): dark slate glass panels with two cut corners and a cool-to-amber rim,
    /// amber call-to-action buttons with dark text, cyan for the selected item, condensed display type. Sprites are generated
    /// in code (9-sliced), so no image files are needed. The older painted button images (ui_panel, ui_button_blue …) are
    /// only used when <see cref="UseArtistSkins"/> is on.
    /// </summary>
    public static class Glossy
    {
        public enum Style { Panel, Menu, Selected, Orange, Pill, Locked, Green, Play, Red }

        /// <summary>Use the Image Mapper's painted ui_* button / panel images instead of the generated Sky Ops skins.</summary>
        public static bool UseArtistSkins = false;

        private static readonly Dictionary<Style, Sprite> Cache = new Dictionary<Style, Sprite>();

        public static readonly Color Gold = Theme.Hex("#FFD24D");
        public static readonly Color SkyBlue = Theme.Hex("#5FE0FF");
        public static readonly Color BarTrack = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color BarFill = Theme.Hex("#FFB21A");

        public static Sprite Sprite(Style s)
        {
            if (Cache.TryGetValue(s, out Sprite sp) && sp != null) return sp;
            Sprite art = UseArtistSkins ? Art.Sprite(ArtKey(s)) : null;
            sp = art != null ? art : Make(s);
            Cache[s] = sp;
            return sp;
        }

        /// <summary>Label colour that reads on a skin: dark on amber / green, white elsewhere.</summary>
        public static Color TextOn(Style s) =>
            s == Style.Orange || s == Style.Play ? Theme.TextOnAccent : s == Style.Green ? Theme.TextOnGood : s == Style.Locked ? Theme.Hex("#8D97AA") : Theme.Text;

        /// <summary>Skins whose label uses the italic display face (big calls to action).</summary>
        public static bool IsCallToAction(Style s) => s == Style.Orange || s == Style.Play;

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
                //                         size cut  top        mid        bottom     rim top              rim bottom           gloss  alpha
                case Style.Panel:    return Cut(96, 16, "#1A2438", "#121A2A", "#090E18", "#A0BEF0", 0.55f, "#FFB21A", 0.55f, 0.06f, 0.93f);
                case Style.Menu:     return Cut(96, 13, "#2D3A57", "#202B42", "#141C2C", "#6D86B3", 1f, "#2B3A57", 1f, 0.10f, 0.96f);
                case Style.Selected: return Cut(96, 13, "#2BC2EE", "#16A0D0", "#0A6896", "#D4F7FF", 1f, "#2FD4FF", 1f, 0.18f, 1f);
                case Style.Orange:   return Cut(128, 16, "#FFD84A", "#FFAE1F", "#FF7A00", "#FFF3C2", 1f, "#FF9A00", 1f, 0.26f, 1f);
                case Style.Play:     return Cut(192, 26, "#FFDE55", "#FFAE1F", "#FF6F00", "#FFF6D0", 1f, "#FF9A00", 1f, 0.30f, 1f);
                case Style.Pill:     return Cut(64, 9, "#141C2D", "#0F1624", "#0A0F1A", "#96B4E6", 0.45f, "#3C5078", 0.35f, 0.04f, 0.93f);
                case Style.Locked:   return Cut(96, 13, "#2A3142", "#212736", "#171C27", "#59647D", 1f, "#2A3142", 1f, 0.05f, 0.96f);
                case Style.Red:      return Cut(96, 13, "#FF6274", "#E8394F", "#B3132A", "#FFC2C9", 1f, "#FF3B52", 1f, 0.18f, 1f);
                default:             return Cut(96, 13, "#4BE69A", "#2CC277", "#118A52", "#C9FFE0", 1f, "#2FCF7A", 1f, 0.20f, 1f);
            }
        }

        /// <summary>
        /// Cut-corner box (top-left and bottom-right at 45°): three-stop vertical gradient, a 2 px rim that blends from
        /// <paramref name="rimTop"/> to <paramref name="rimBottom"/>, a soft gloss over the top half and a 1 px inner highlight.
        /// Returned as a 9-slice sprite.
        /// </summary>
        private static Sprite Cut(int size, int cut, string top, string mid, string bottom, string rimTop, float rimTopA, string rimBottom, float rimBottomA,
            float gloss, float alpha)
        {
            Color ct = Theme.Hex(top), cm = Theme.Hex(mid), cb = Theme.Hex(bottom);
            Color rt = Theme.Hex(rimTop), rb = Theme.Hex(rimBottom);
            rt.a = rimTopA; rb.a = rimBottomA;
            const float rimWidth = 2f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "skyops" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Sprites.CutDistance(x + 0.5f, y + 0.5f, size, size, cut); // <0 inside
                    float cover = Mathf.Clamp01(0.5f - d);
                    if (cover <= 0f) { px[y * size + x] = Color.clear; continue; }
                    float v = y / (size - 1f); // 0 bottom … 1 top
                    Color c = v > 0.5f ? Color.Lerp(cm, ct, (v - 0.5f) * 2f) : Color.Lerp(cb, cm, v * 2f);
                    c.a = alpha;
                    if (v > 0.5f) c = Color.Lerp(c, new Color(1f, 1f, 1f, c.a), gloss * Mathf.InverseLerp(0.5f, 1f, v));  // gloss
                    float hl = Mathf.Clamp01(1f - Mathf.Abs(d + rimWidth + 1f));                                         // 1 px inner highlight
                    c = Color.Lerp(c, new Color(1f, 1f, 1f, Mathf.Max(c.a, 0.9f)), hl * 0.12f * v);
                    float rimT = Mathf.Clamp01(d + rimWidth + 0.5f);
                    Color rim = Color.Lerp(rb, rt, v);
                    Color outc = Color.Lerp(c, rim, rimT);
                    outc.a = Mathf.Lerp(c.a, rim.a, rimT) * cover;
                    px[y * size + x] = outc;
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = cut + rimWidth + 3f;
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
            ColorBlock disabled = btn.colors;
            disabled.disabledColor = Color.white;
            btn.colors = disabled;
            bg.gameObject.AddComponent<DisabledSkin>();
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
            Color ink = TextOn(style);
            string main = UiKit.Caps(text);
            string label = sub == null ? main : $"{main}\n<size={Mathf.RoundToInt(fontSize * 0.7f)}><color=#{ColorUtility.ToHtmlStringRGB(ink)}AA>{sub}</color></size>";
            Text t = UiKit.Label(bg.transform, label, fontSize, ink, icon != null || chevron ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.UseButtonFont(t, IsCallToAction(style));
            if (ink == Theme.Text) t.Shadowed();
            else UiKit.SoftShadow(t);
            t.rectTransform.Stretch(left, 2, chevron ? 50 : 16, 2);
            if (chevron)
            {
                Image ch = UiKit.Image(bg.transform, new Color(1f, 1f, 1f, 0.75f), Icons.Get("chevron_right"), "Chevron");
                ch.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(26, 26));
            }
            return btn;
        }

        /// <summary>Slanted progress bar (dark track + amber fill), the Sky Ops meter.</summary>
        public static Image Bar(Transform parent, float value01, Color? fill = null)
        {
            Image track = UiKit.Image(parent, BarTrack, Sprites.Slant, "Track");
            track.type = Image.Type.Sliced;
            Image f = UiKit.Image(track.transform, fill ?? BarFill, Sprites.Slant, "Fill");
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
