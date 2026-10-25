using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.UI
{
    /// <summary>Generates UI sprites at runtime (rounded boxes, circles, gradients) – no image files needed.</summary>
    public static class Sprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // "Sky Ops" look: plain boxes have two cut corners (top-left, bottom-right) instead of round ones.
        public static Sprite Rounded => Get("rounded", () => CutRect(64, 14));
        public static Sprite RoundedSmall => Get("rounded_s", () => CutRect(32, 7));
        /// <summary>The old round-cornered box (avatars, chips that should stay soft).</summary>
        public static Sprite Soft9 => Get("soft9", () => RoundedRect(64, 22));
        /// <summary>Round HUD button: radial body, bright top, thin rim – tint it with the button colour.</summary>
        public static Sprite Orb => Get("orb", () => OrbSprite(192));
        /// <summary>Parallelogram leaning right (title underline, tags, bars).</summary>
        public static Sprite Slant => Get("slant", () => SlantSprite(64, 24, 18));
        /// <summary>Repeating diagonal hairlines (texture is set to Repeat; use with RawImage.uvRect for tiling).</summary>
        public static Texture2D Hatch => GetTex("hatch", () => HatchTex(32));

        private static readonly Dictionary<string, Texture2D> TexCache = new Dictionary<string, Texture2D>();

        private static Texture2D GetTex(string key, System.Func<Texture2D> make)
        {
            if (!TexCache.TryGetValue(key, out Texture2D t) || t == null) { t = make(); TexCache[key] = t; }
            return t;
        }

        /// <summary>Box with the top-left and bottom-right corners cut at 45° (anti-aliased, 9-sliced).</summary>
        private static Sprite CutRect(int size, int cut)
        {
            Texture2D tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = CutDistance(x + 0.5f, y + 0.5f, size, size, cut);
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            float b = cut + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>
        /// Signed distance (pixels, &lt;0 inside) to a w×h box whose top-left and bottom-right corners are cut by <paramref name="cut"/>.
        /// y grows upwards (texture space). Shared by the glossy skins.
        /// </summary>
        public static float CutDistance(float x, float y, float w, float h, float cut)
        {
            float box = Mathf.Max(Mathf.Max(-x, x - w), Mathf.Max(-y, y - h));
            const float inv = 0.70710678f;
            float tl = (cut - (x + (h - y))) * inv;        // top-left corner
            float br = (cut - ((w - x) + y)) * inv;        // bottom-right corner
            return Mathf.Max(box, Mathf.Max(tl, br));
        }

        private static Sprite OrbSprite(int size)
        {
            Texture2D tex = NewTex(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - 1f - d);
                    if (a <= 0f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    float n = d / r;                                         // 0 centre … 1 edge
                    float top = Mathf.Clamp01(dy / r * 0.5f + 0.5f);          // 0 bottom … 1 top
                    float v = Mathf.Lerp(0.62f, 1f, top) * Mathf.Lerp(1f, 0.82f, n * n);
                    float rim = Mathf.Clamp01((d - (r - 6f)) / 3f);           // bright 4 px rim
                    v = Mathf.Lerp(v, 1f, rim * 0.85f);
                    // gloss crescent at the top
                    float gx = dx / (r * 0.7f), gy = (dy - r * 0.38f) / (r * 0.42f);
                    float gloss = Mathf.Clamp01(1f - (gx * gx + gy * gy)) * 0.35f;
                    byte c = (byte)(Mathf.Clamp01(v + gloss) * 255);
                    px[y * size + x] = new Color32(c, c, c, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite SlantSprite(int w, int h, int lean)
        {
            Texture2D tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float off = lean * (y + 0.5f) / h;                        // leans right as it goes up
                    float left = x + 0.5f - off, right = (w - lean) - (x + 0.5f - off);
                    float a = Mathf.Clamp01(Mathf.Min(left, right) + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(lean + 2, 0, lean + 2, 0));
        }

        private static Texture2D HatchTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "hatch" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int k = ((x + y) % 16 + 16) % 16;                          // 45° lines every 16 px
                    byte a = (byte)(k < 2 ? 22 : 0);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
        public static Sprite Circle => Get("circle", () => CircleSprite(128, 0f));
        public static Sprite Ring => Get("ring", () => CircleSprite(128, 0.78f));
        public static Sprite White => Get("white", () => Solid());
        public static Sprite VerticalFade => Get("vfade", () => Gradient());
        public static Sprite Triangle => Get("tri", () => TriangleSprite(64));
        /// <summary>Soft round glow (clouds, sparkles, sun) – alpha falls off smoothly to the edge.</summary>
        public static Sprite Soft => Get("soft", () => SoftSprite(64));

        private static Sprite SoftSprite(int size)
        {
            Texture2D tex = NewTex(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (!Cache.TryGetValue(key, out Sprite s) || s == null) { s = make(); Cache[key] = s; }
            return s;
        }

        private static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            return tex;
        }

        private static Sprite RoundedRect(int size, int radius)
        {
            Texture2D tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CircleSprite(int size, float inner)
        {
            Texture2D tex = NewTex(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float a = Mathf.Clamp01(r - d);
                    if (inner > 0f) a *= Mathf.Clamp01(d - r * inner);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite TriangleSprite(int size)
        {
            Texture2D tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float half = (y + 0.5f) / size * 0.5f * size;
                    float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - size * 0.5f) + 0.5f);
                    px[(size - 1 - y) * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Solid()
        {
            Texture2D tex = NewTex(4, 4);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Gradient()
        {
            Texture2D tex = NewTex(4, 64);
            var px = new Color32[4 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 4; x++)
                    px[y * 4 + x] = new Color32(255, 255, 255, (byte)(255 * (y / 63f)));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
        }
    }
}
