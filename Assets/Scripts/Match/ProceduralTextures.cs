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

        /// <summary>True for an artist texture (named by its Image Mapper slot), false for a generated one.</summary>
        public static bool IsArt(Texture t) => t != null && t.name.StartsWith("tex_");

        /// <summary>
        /// Artist textures are detailed paintings, not 256 px patterns: show one repeat every <paramref name="meters"/> instead of
        /// every metre so the floor or wall does not look busy. Generated textures keep their scale.
        /// </summary>
        public static Material ArtScale(Material m, float meters)
        {
            if (m != null && IsArt(m.mainTexture)) m.mainTextureScale = Vector2.one / Mathf.Max(0.1f, meters);
            return m;
        }

        /// <summary>
        /// Town house facade: 4 bays × 4 storeys (one tile = 12 × 12 m) of plaster with windows – glass with a sky reflection,
        /// light frames and sills, some shutters closed, some small balconies and AC boxes – and a ledge between storeys.
        /// Near-white so the material colour gives each house its paint.
        /// </summary>
        public static Texture2D Facade() => Get("facade", () =>
        {
            const int size = 512, cell = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[size * size];
            Color wall = new Color(0.93f, 0.91f, 0.87f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int bx = x / cell, fy = y / cell, lx = x % cell, ly = y % cell;
                    float v = Hash(bx * 7 + 3, fy * 13 + 5);
                    float stain = Mathf.Clamp01((Fbm(x * 0.5f + 11f, y * 0.22f) - 0.6f) * 2.5f) * 0.12f;
                    Color c = Shade(wall, Fbm(x * 2f, y * 2f) * 0.08f - 0.04f - stain);
                    if (ly < 7) c = Shade(wall, -0.12f + Noise(x, y, 3) * 0.04f);            // storey ledge
                    else if (ly < 9) c = Shade(wall, -0.3f);                                    // ledge shadow
                    bool inWin = lx >= 36 && lx < 92 && ly >= 30 && ly < 104;
                    bool frame = lx >= 32 && lx < 96 && ly >= 26 && ly < 108 && !inWin;
                    if (frame) c = new Color(0.97f, 0.96f, 0.94f);
                    if (ly >= 20 && ly < 26 && lx >= 28 && lx < 100) c = new Color(0.82f, 0.8f, 0.77f); // sill
                    if (inWin)
                    {
                        if (v < 0.22f)
                        {
                            // Closed wooden shutters (louvres).
                            bool slat = (ly / 5) % 2 == 0;
                            c = new Color(0.55f, 0.5f, 0.45f) * (slat ? 1f : 0.82f);
                            if (lx == 63 || lx == 64) c *= 0.6f;
                        }
                        else
                        {
                            // Glass: dark room, a soft sky reflection toward the top, a centre mullion.
                            float k = (ly - 30) / 74f;
                            c = Color.Lerp(new Color(0.12f, 0.15f, 0.2f), new Color(0.42f, 0.52f, 0.62f), k * k * 0.9f);
                            if (Mathf.Abs(lx - 63.5f) < 2f || Mathf.Abs(ly - 72f) < 1.5f) c = new Color(0.9f, 0.89f, 0.86f);
                            if (v > 0.8f && ly < 70) c = Color.Lerp(c, new Color(0.92f, 0.86f, 0.7f), 0.55f); // curtain
                        }
                    }
                    // Small balcony railing on some windows.
                    if (v > 0.55f && v < 0.72f && ly >= 18 && ly < 52 && lx >= 24 && lx < 104 && fy > 0)
                    {
                        bool rail = ly >= 48 || ly < 22 || (lx - 24) % 8 < 2;
                        if (rail) c = new Color(0.22f, 0.22f, 0.24f);
                    }
                    // AC unit beside some windows.
                    if (v > 0.38f && v < 0.48f && lx >= 100 && lx < 122 && ly >= 40 && ly < 58)
                        c = lx < 102 || ly < 42 ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.84f, 0.84f, 0.82f) * ((lx / 3) % 2 == 0 ? 0.9f : 1f);
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        });

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
