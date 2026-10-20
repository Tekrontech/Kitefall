using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Original surface textures generated at runtime (upgrade spec 0.1): brick, plaster, concrete, wood planks, roof tiles.
    /// Small (256 px), tileable, mipmapped – cheap on phones and nothing to download.
    /// </summary>
    public static class ProceduralTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private const int Size = 256;

        public static Texture2D Brick(Color baseColor) => Get("brick" + ColorUtility.ToHtmlStringRGB(baseColor), () => Make((x, y) =>
        {
            int row = y / 16;
            int ox = row % 2 == 0 ? 0 : 16;
            bool mortar = y % 16 < 2 || (x + ox) % 32 < 2;
            if (mortar) return Color.Lerp(new Color(0.82f, 0.8f, 0.76f), baseColor, 0.15f) * (0.92f + Noise(x, y, 3) * 0.1f);
            int bx = (x + ox) / 32;
            float tint = Hash(bx, row) * 0.18f - 0.09f;
            return Shade(baseColor, tint + Noise(x, y, 6) * 0.08f);
        }));

        public static Texture2D Plaster(Color baseColor) => Get("plaster" + ColorUtility.ToHtmlStringRGB(baseColor), () => Make((x, y) =>
        {
            float n = Fbm(x, y) * 0.16f - 0.08f;
            float stain = Mathf.Clamp01((Fbm(x * 0.5f + 40f, y * 0.25f) - 0.62f) * 3f) * 0.18f; // rain streaks
            return Shade(baseColor, n - stain);
        }));

        public static Texture2D Concrete(Color baseColor) => Get("concrete" + ColorUtility.ToHtmlStringRGB(baseColor), () => Make((x, y) =>
        {
            float n = Fbm(x * 2f, y * 2f) * 0.14f - 0.07f;
            bool joint = x % 128 < 1 || y % 128 < 1;
            return Shade(baseColor, n - (joint ? 0.15f : 0f));
        }));

        public static Texture2D Wood(Color baseColor) => Get("wood" + ColorUtility.ToHtmlStringRGB(baseColor), () => Make((x, y) =>
        {
            int plank = x / 32;
            bool gap = x % 32 < 1;
            float grain = Mathf.Sin((y * 0.12f + Noise(x, y, 8) * 6f + plank * 13f)) * 0.06f;
            return gap ? baseColor * 0.45f : Shade(baseColor, grain + Hash(plank, 7) * 0.12f - 0.06f);
        }));

        public static Texture2D Tiles(Color baseColor) => Get("tiles" + ColorUtility.ToHtmlStringRGB(baseColor), () => Make((x, y) =>
        {
            bool line = x % 32 < 1 || y % 32 < 1;
            return line ? baseColor * 0.7f : Shade(baseColor, Hash(x / 32, y / 32) * 0.1f - 0.05f + Noise(x, y, 4) * 0.04f);
        }));

        /// <summary>
        /// The artist's surface texture (Image Mapper slot tex_brick_wall, tex_plaster_wall, tex_roof_floor, tex_wood_balcony …)
        /// when installed, otherwise the generated one.
        /// </summary>
        public static Texture2D Art(string slot, Texture2D generated)
        {
            Texture2D t = Kitefall.UI.Art.Texture(slot);
            if (t == null) return generated;
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        /// <summary>A soft round glow (sun disc, lamps, particles).</summary>
        public static Texture2D Glow() => Get("glow", () =>
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply(true);
            return t;
        });

        private static Texture2D Get(string key, System.Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out Texture2D t) && t != null) return t;
            t = make();
            t.name = key;
            Cache[key] = t;
            return t;
        }

        private static Texture2D Make(System.Func<int, int, Color> pixel)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = pixel(x, y);
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        private static Color Shade(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), 1f);

        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Value noise that tiles over the 256 px texture.</summary>
        private static float Noise(float x, float y, int cell)
        {
            int cells = Size / cell;
            float fx = x / cell, fy = y / cell;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(Wrap(x0, cells), Wrap(y0, cells)), b = Hash(Wrap(x0 + 1, cells), Wrap(y0, cells));
            float c = Hash(Wrap(x0, cells), Wrap(y0 + 1, cells)), d = Hash(Wrap(x0 + 1, cells), Wrap(y0 + 1, cells));
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static int Wrap(int v, int n) => (v % n + n) % n;

        private static float Fbm(float x, float y) => Noise(x, y, 32) * 0.5f + Noise(x, y, 16) * 0.3f + Noise(x, y, 8) * 0.2f;
    }
}
