using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.UI
{
    /// <summary>
    /// Vector-style icons drawn in code (upgrade spec 0.1 UI): white shapes with soft anti-aliased edges, tinted by the Image colour.
    /// Used by the kill feed, quick chat, emote wheel, prep hints, roof map, spectate bar and HUD buttons.
    /// </summary>
    public static class Icons
    {
        private const int S = 96;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out Sprite s) && s != null) return s;
            // Artist glyphs (white on transparent, tinted like the drawn ones) win over the code-drawn shape.
            s = Art.Sprite("ui_icon_" + name) ?? Art.Sprite("hud_" + name) ?? Art.Sprite("chat_" + name) ?? Art.Sprite("trait_" + name);
            if (s != null) { Cache[name] = s; return s; }
            var p = new Painter();
            Draw(name, p);
            s = p.ToSprite(name);
            Cache[name] = s;
            return s;
        }

        private static void Draw(string name, Painter p)
        {
            switch (name)
            {
                case "camera":
                    p.RoundRect(14, 26, 68, 46, 8); p.Rect(34, 70, 22, 8);
                    p.Circle(48, 49, 16, true); p.Circle(48, 49, 9);
                    break;
                case "mic":
                    p.RoundRect(36, 34, 24, 44, 12); p.Ring(48, 46, 26, 4, 180f, 360f); p.Rect(46, 14, 4, 12); p.Rect(34, 12, 28, 5);
                    break;
                case "speaker":
                    p.Rect(16, 36, 16, 24); p.Tri(32, 36, 54, 18, 54, 78); p.Tri(32, 60, 54, 78, 32, 36);
                    p.Ring(54, 48, 18, 4, -50f, 50f); p.Ring(54, 48, 30, 4, -50f, 50f);
                    break;
                case "mute":
                    Draw("speaker", p); p.Line(62, 30, 86, 66, 6, true); p.Line(62, 66, 86, 30, 6, true);
                    break;
                case "emote":
                    p.Circle(48, 48, 36); p.Circle(36, 56, 5, true); p.Circle(60, 56, 5, true); p.Ring(48, 46, 18, 5, 200f, 340f, true);
                    break;
                case "chat":
                    p.RoundRect(12, 30, 72, 50, 14); p.Tri(24, 32, 22, 14, 42, 32);
                    p.Circle(32, 55, 5, true); p.Circle(48, 55, 5, true); p.Circle(64, 55, 5, true);
                    break;
                case "lock":
                    p.RoundRect(22, 14, 52, 42, 6); p.Ring(48, 58, 18, 7, 0f, 180f); p.Circle(48, 36, 6, true);
                    break;
                case "crown":
                    p.Tri(14, 26, 14, 70, 34, 44); p.Tri(34, 44, 48, 76, 62, 44); p.Tri(62, 44, 82, 70, 82, 26); p.Rect(14, 20, 68, 26);
                    break;
                case "check":
                    p.Line(20, 50, 40, 28, 10); p.Line(40, 28, 78, 70, 10);
                    break;
                case "kite":
                    p.Tri(48, 88, 20, 52, 76, 52); p.Tri(20, 52, 48, 10, 76, 52); p.Line(48, 10, 40, 2, 3);
                    break;
                case "scissors":
                case "cut":
                    p.Ring(28, 24, 12, 6); p.Ring(68, 24, 12, 6); p.Line(34, 34, 66, 84, 7); p.Line(62, 34, 30, 84, 7);
                    break;
                case "snap":
                    p.Line(10, 48, 40, 48, 6); p.Line(56, 48, 86, 48, 6); p.Line(40, 48, 46, 62, 4); p.Line(56, 48, 50, 34, 4);
                    p.Circle(48, 48, 4);
                    break;
                case "crash":
                    p.Line(20, 20, 76, 76, 12); p.Line(20, 76, 76, 20, 12);
                    break;
                case "kheench":
                case "arrow_back":
                    p.Line(78, 48, 26, 48, 10); p.Tri(14, 48, 38, 68, 38, 28);
                    break;
                case "dheel":
                    for (int i = 0; i < 3; i++) p.Wave(10, 34 + i * 14, 76, 6, 5);
                    break;
                case "dive":
                case "arrow_down":
                    p.Line(48, 84, 48, 30, 10); p.Tri(48, 12, 26, 38, 70, 38);
                    break;
                case "hold":
                case "defend":
                    p.Tri(14, 74, 82, 74, 48, 10); p.Rect(14, 74, 68, 10);
                    break;
                case "attack":
                    p.Line(20, 20, 70, 70, 10); p.Tri(84, 84, 56, 76, 76, 56); p.Line(24, 66, 34, 76, 8);
                    break;
                case "up":
                case "arrow_up":
                    p.Line(48, 12, 48, 66, 10); p.Tri(48, 84, 26, 58, 70, 58);
                    break;
                case "help":
                    p.Ring(48, 60, 18, 8, -90f, 200f); p.Line(48, 42, 48, 30, 8); p.Circle(48, 16, 6);
                    break;
                case "crate":
                    p.Rect(18, 16, 60, 48); p.Rect(18, 62, 60, 8, true); p.Line(48, 64, 48, 88, 3); p.Circle(48, 88, 6);
                    break;
                case "thanks":
                    p.Circle(34, 56, 16); p.Circle(62, 56, 16); p.Tri(20, 50, 76, 50, 48, 14);
                    break;
                case "gg":
                    p.Ring(32, 48, 18, 7, 20f, 330f); p.Rect(32, 44, 16, 7); p.Ring(68, 48, 18, 7, 20f, 330f); p.Rect(68, 44, 16, 7);
                    break;
                case "plus":
                case "assist":
                    p.Rect(42, 14, 12, 68); p.Rect(14, 42, 68, 12);
                    break;
                case "storm":
                    p.Circle(34, 58, 16); p.Circle(56, 64, 20); p.Circle(70, 54, 14); p.Rect(22, 44, 58, 16);
                    p.Line(52, 40, 42, 22, 6); p.Line(42, 22, 56, 22, 6); p.Line(56, 22, 46, 4, 6);
                    break;
                case "thumbs":
                    p.RoundRect(16, 14, 22, 44, 4); p.RoundRect(38, 14, 40, 44, 10); p.RoundRect(44, 54, 16, 30, 8);
                    break;
                case "fire":
                    p.Circle(48, 34, 24); p.Tri(26, 40, 48, 92, 70, 40); p.Circle(48, 30, 11, true);
                    break;
                case "flag":
                    p.Rect(20, 8, 6, 80); p.Tri(26, 86, 26, 52, 78, 70);
                    break;
                case "person":
                    p.Circle(48, 66, 16); p.RoundRect(22, 10, 52, 40, 18);
                    break;
                case "ladder":
                    p.Rect(24, 8, 8, 80); p.Rect(64, 8, 8, 80); for (int i = 0; i < 5; i++) p.Rect(24, 16 + i * 16, 48, 6);
                    break;
                case "plank":
                    p.RoundRect(8, 40, 80, 16, 4);
                    break;
                case "bridge":
                    p.Ring(48, 90, 52, 6, 205f, 335f); for (int i = 0; i < 6; i++) p.Rect(16 + i * 12, 38, 6, 10);
                    break;
                case "stairs":
                    for (int i = 0; i < 4; i++) p.Rect(12 + i * 18, 12, 18, 18 + i * 18);
                    break;
                case "jump":
                    p.Ring(48, 30, 32, 7, 30f, 150f); p.Tri(80, 30, 70, 50, 90, 46);
                    break;
                case "launch":
                    p.Tri(48, 90, 22, 56, 74, 56); p.Tri(22, 56, 48, 26, 74, 56); p.Line(48, 26, 48, 6, 4); p.Line(30, 8, 66, 8, 4);
                    break;
                case "packup":
                    p.Ring(48, 48, 30, 8); p.Line(48, 48, 48, 22, 6); p.Line(48, 48, 66, 58, 6);
                    break;
                case "wind":
                    p.Wave(8, 60, 70, 8, 6); p.Wave(14, 40, 74, 8, 6); p.Wave(8, 20, 60, 8, 6);
                    break;
                case "star":
                    p.Star(48, 48, 40, 18);
                    break;
                // ---- Home screen (glossy design) -----------------------------------
                case "bird":
                    p.Ring(30, 30, 20, 7, 40f, 140f); p.Ring(66, 30, 20, 7, 40f, 140f);
                    break;
                case "chevron_right":
                    p.Line(36, 22, 60, 48, 9); p.Line(60, 48, 36, 74, 9);
                    break;
                case "chevron_left":
                    p.Line(60, 22, 36, 48, 9); p.Line(36, 48, 60, 74, 9);
                    break;
                case "shirt":
                    p.Tri(10, 62, 28, 80, 30, 52); p.Tri(86, 62, 68, 80, 66, 52); p.RoundRect(28, 14, 40, 66, 6);
                    p.Circle(48, 80, 9, true);
                    break;
                case "people":
                    p.Circle(34, 64, 13); p.RoundRect(14, 16, 40, 36, 14);
                    p.Circle(64, 62, 11); p.RoundRect(52, 16, 32, 32, 12);
                    break;
                case "target":
                    p.Ring(48, 48, 34, 8); p.Ring(48, 48, 18, 8); p.Circle(48, 48, 6); p.Line(48, 48, 82, 82, 6);
                    break;
                case "hangar": // kite on a stand
                    p.Tri(48, 88, 74, 56, 48, 30); p.Tri(48, 88, 48, 30, 22, 56); p.Line(48, 30, 48, 8, 6); p.Rect(30, 6, 36, 6);
                    break;
                case "wrench":
                    p.Line(24, 24, 62, 62, 14); p.Circle(68, 68, 18); p.Circle(76, 76, 9, true); p.Circle(24, 24, 9);
                    break;
                case "cart":
                    p.Line(10, 76, 24, 76, 7); p.Line(24, 76, 34, 36, 7); p.RoundRect(28, 36, 54, 32, 5); p.Circle(38, 22, 7); p.Circle(72, 22, 7);
                    break;
                case "trophy":
                    p.RoundRect(28, 44, 40, 40, 12); p.Ring(26, 66, 12, 6, 90f, 270f); p.Ring(70, 66, 12, 6, -90f, 90f);
                    p.Rect(44, 22, 8, 24); p.RoundRect(30, 10, 36, 12, 4);
                    break;
                case "gear":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        p.Line(48 + Mathf.Cos(a) * 24, 48 + Mathf.Sin(a) * 24, 48 + Mathf.Cos(a) * 38, 48 + Mathf.Sin(a) * 38, 13);
                    }
                    p.Circle(48, 48, 28); p.Circle(48, 48, 11, true);
                    break;
                case "mail":
                    p.RoundRect(12, 22, 72, 52, 6); p.Line(14, 72, 48, 44, 6, true); p.Line(82, 72, 48, 44, 6, true);
                    break;
                case "chest":
                    p.RoundRect(14, 14, 68, 40, 6); p.RoundRect(14, 50, 68, 26, 12); p.Rect(14, 50, 68, 5, true);
                    p.RoundRect(42, 40, 12, 18, 3, true); p.Rect(45, 43, 6, 10);
                    break;
                case "swords":
                    p.Line(18, 18, 78, 78, 9); p.Line(78, 18, 18, 78, 9);
                    p.Line(22, 34, 34, 22, 9); p.Line(62, 22, 74, 34, 9);
                    break;
                case "door":
                    p.RoundRect(26, 10, 44, 78, 4); p.RoundRect(32, 16, 32, 66, 2, true); p.Tri(32, 16, 58, 24, 58, 76); p.Tri(32, 16, 58, 76, 32, 82); p.Circle(52, 48, 4, true);
                    break;
                case "pencil":
                    p.Line(24, 24, 70, 70, 14); p.Tri(14, 14, 30, 18, 18, 30); p.Line(66, 66, 76, 76, 14, true); p.Line(70, 70, 80, 80, 10);
                    break;
                case "clock":
                    p.Ring(48, 48, 34, 8); p.Line(48, 48, 48, 72, 7); p.Line(48, 48, 64, 40, 7);
                    break;
                case "coin":
                    p.Circle(48, 48, 38); p.Ring(48, 48, 26, 5, 0f, 360f, true); p.Star(48, 48, 16, 7);
                    break;
                case "gem":
                    p.Tri(48, 88, 18, 56, 78, 56); p.Tri(18, 56, 48, 8, 78, 56);
                    p.Line(30, 56, 48, 80, 3, true); p.Line(66, 56, 48, 80, 3, true);
                    break;
                case "parts":
                    p.RoundRect(14, 24, 68, 48, 10); p.Rect(30, 42, 36, 12, true); p.Circle(48, 48, 8);
                    break;
                case "ribbon":
                    p.Wave(10, 56, 76, 10, 10); p.Wave(14, 36, 70, 10, 8);
                    break;
                case "spool":
                    p.RoundRect(26, 10, 44, 10, 4); p.RoundRect(26, 76, 44, 10, 4); p.RoundRect(32, 18, 32, 60, 4);
                    p.Line(32, 30, 64, 34, 3, true); p.Line(32, 44, 64, 48, 3, true); p.Line(32, 58, 64, 62, 3, true);
                    break;
                case "thread":
                    p.Circle(48, 48, 32); p.Ring(48, 48, 22, 3, 0f, 360f, true); p.Ring(48, 48, 12, 3, 0f, 360f, true); p.Line(70, 30, 90, 10, 4);
                    break;
                default:
                    p.Circle(48, 48, 30);
                    break;
            }
        }

        /// <summary>Tiny software rasteriser: coverage-based shapes on a 96×96 alpha canvas.</summary>
        private sealed class Painter
        {
            private readonly float[] _a = new float[S * S];

            public Sprite ToSprite(string name)
            {
                var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "icon_" + name };
                var px = new Color32[S * S];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(_a[i]) * 255f));
                t.SetPixels32(px);
                t.Apply(true);
                return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            }

            private void Shade(Func<float, float, float> sdf, bool erase)
            {
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float d = sdf(x + 0.5f, y + 0.5f);
                        float cov = Mathf.Clamp01(0.5f - d);
                        if (cov <= 0f) continue;
                        int i = y * S + x;
                        _a[i] = erase ? Mathf.Min(_a[i], 1f - cov) : Mathf.Max(_a[i], cov);
                    }
            }

            public void Circle(float cx, float cy, float r, bool erase = false) => Shade((x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, erase);

            public void Ring(float cx, float cy, float r, float w, float a0 = 0f, float a1 = 360f, bool erase = false) => Shade((x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < 0) ang += 360f;
                bool inArc = a1 - a0 >= 359.9f || (a0 <= a1 ? ang >= a0 && ang <= a1 : ang >= a0 || ang <= a1) || (a0 < 0 && ang >= a0 + 360f);
                if (!inArc) return 99f;
                return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - w * 0.5f;
            }, erase);

            public void Rect(float x0, float y0, float w, float h, bool erase = false) => Shade((x, y) =>
            {
                float dx = Mathf.Max(x0 - x, x - (x0 + w)), dy = Mathf.Max(y0 - y, y - (y0 + h));
                return Mathf.Max(dx, dy);
            }, erase);

            public void RoundRect(float x0, float y0, float w, float h, float r, bool erase = false) => Shade((x, y) =>
            {
                float cx = x0 + w * 0.5f, cy = y0 + h * 0.5f;
                float qx = Mathf.Abs(x - cx) - (w * 0.5f - r), qy = Mathf.Abs(y - cy) - (h * 0.5f - r);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            }, erase);

            public void Line(float x0, float y0, float x1, float y1, float w, bool erase = false) => Shade((x, y) =>
            {
                Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), p = new Vector2(x, y);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                return (p - (a + ab * t)).magnitude - w * 0.5f;
            }, erase);

            public void Tri(float ax, float ay, float bx, float by, float cx, float cy, bool erase = false) => Shade((x, y) =>
            {
                float e0 = Edge(ax, ay, bx, by, x, y), e1 = Edge(bx, by, cx, cy, x, y), e2 = Edge(cx, cy, ax, ay, x, y);
                bool ccw = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax) > 0f;
                if (!ccw) { e0 = -e0; e1 = -e1; e2 = -e2; }
                return -Mathf.Min(e0, Mathf.Min(e1, e2));
            }, erase);

            private static float Edge(float ax, float ay, float bx, float by, float px, float py)
            {
                float ex = bx - ax, ey = by - ay;
                float len = Mathf.Max(1e-4f, Mathf.Sqrt(ex * ex + ey * ey));
                return ((px - ax) * ey - (py - ay) * ex) / -len;
            }

            public void Wave(float x0, float y0, float length, float amp, float w)
            {
                for (int i = 0; i < 24; i++)
                {
                    float t0 = i / 24f, t1 = (i + 1) / 24f;
                    Line(x0 + t0 * length, y0 + Mathf.Sin(t0 * Mathf.PI * 2f) * amp, x0 + t1 * length, y0 + Mathf.Sin(t1 * Mathf.PI * 2f) * amp, w);
                }
            }

            public void Star(float cx, float cy, float ro, float ri)
            {
                for (int i = 0; i < 5; i++)
                {
                    float a0 = (90f + i * 72f) * Mathf.Deg2Rad, a1 = (90f + i * 72f + 36f) * Mathf.Deg2Rad, a2 = (90f + i * 72f - 36f) * Mathf.Deg2Rad;
                    Tri(cx + Mathf.Cos(a0) * ro, cy + Mathf.Sin(a0) * ro, cx + Mathf.Cos(a1) * ri, cy + Mathf.Sin(a1) * ri, cx + Mathf.Cos(a2) * ri, cy + Mathf.Sin(a2) * ri);
                }
                Circle(cx, cy, ri);
            }
        }
    }
}
