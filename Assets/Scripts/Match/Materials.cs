using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kitefall.Match
{
    /// <summary>Creates materials that work in URP and in the Built-in pipeline. Cached by key.</summary>
    public static class Materials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static bool Urp => GraphicsSettings.currentRenderPipeline != null;

        public static Material Lit(Color color, float smoothness = 0.15f, Texture texture = null, string key = null)
        {
            key = key ?? ("lit_" + ColorUtility.ToHtmlStringRGBA(color) + smoothness + (texture != null ? texture.GetHashCode().ToString() : ""));
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            Material template = Kitefall.App.Game.Db != null ? Kitefall.App.Game.Db.litTemplate : null;
            if (template != null) m = new Material(template) { name = key, color = color };
            else
            {
                Shader shader = (Urp ? Shader.Find("Universal Render Pipeline/Lit") : null) ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                m = new Material(shader) { name = key, color = color };
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (texture != null)
            {
                m.mainTexture = texture;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            }
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // double-sided sails
            Cache[key] = m;
            return m;
        }

        /// <summary>Unlit, vertex-coloured, transparent – lines, particles, zones.</summary>
        public static Material Unlit(string key = "unlit")
        {
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            var db = Kitefall.App.Game.Db;
            Material template = db == null ? null : key.StartsWith("particle") ? db.particleTemplate : db.unlitTemplate;
            if (template != null)
            {
                m = new Material(template) { name = key };
                Cache[key] = m;
                return m;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (key.StartsWith("particle"))
                shader = (Urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null) ?? Shader.Find("Particles/Standard Unlit") ?? shader;
            m = new Material(shader) { name = key };
            if (key.StartsWith("particle") && m.HasProperty("_Surface"))
            {
                // URP particle: transparent additive-ish
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Kite sail material: the paper/fabric shader when available, else the lit fallback.</summary>
        public static Material KitePaper(Texture texture, string key)
        {
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            Material template = Kitefall.App.Game.Db != null ? Kitefall.App.Game.Db.kitePaperTemplate : null;
            Shader paper = template != null ? template.shader : Shader.Find("Kitefall/KitePaper");
            if (paper == null || !paper.isSupported) return Lit(Color.white, 0.25f, texture, key);
            m = template != null ? new Material(template) { name = key } : new Material(paper) { name = key };
            m.mainTexture = texture;
            Cache[key] = m;
            return m;
        }

        public static Material Skybox(float timeOfDay01)
        {
            Material template = Kitefall.App.Game.Db != null ? Kitefall.App.Game.Db.skyboxTemplate : null;
            Shader sky = template != null ? template.shader : Shader.Find("Skybox/Procedural");
            if (sky == null) return null;
            Shader gradient = Shader.Find("Kitefall/GradientSky");
            if ((template == null || template.shader.name != "Kitefall/GradientSky") && gradient != null && gradient.isSupported) { sky = gradient; template = null; }
            var m = template != null ? new Material(template) { name = "Sky" } : new Material(sky) { name = "Sky" };
            float golden = Mathf.Abs(timeOfDay01 - 0.5f) * 2f; // 0 noon, 1 morning/evening
            if (m.shader.name == "Kitefall/GradientSky")
            {
                // Premium stylised sky: the sky is the hero (upgrade 12). Golden hour in the morning/evening.
                m.SetColor("_ZenithColor", Color.Lerp(new Color(0.2f, 0.45f, 0.86f), new Color(0.25f, 0.33f, 0.62f), golden));
                m.SetColor("_HorizonColor", Color.Lerp(new Color(0.8f, 0.9f, 0.98f), new Color(1f, 0.82f, 0.66f), golden));
                m.SetColor("_GoldenColor", new Color(1f, 0.62f, 0.32f));
                m.SetFloat("_Golden", Mathf.SmoothStep(0.1f, 0.95f, golden));
                m.SetColor("_SunColor", Color.Lerp(new Color(1f, 0.97f, 0.9f), new Color(1f, 0.75f, 0.45f), golden));
                m.SetFloat("_SunSize", Mathf.Lerp(0.003f, 0.006f, golden));
                m.SetFloat("_CloudCover", 0.38f + Mathf.PerlinNoise(timeOfDay01 * 7f, 0.3f) * 0.25f);
                return m;
            }
            m.SetFloat("_SunSize", Mathf.Lerp(0.04f, 0.07f, golden));
            m.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.9f, 1.6f, golden));
            m.SetColor("_SkyTint", Color.Lerp(new Color(0.45f, 0.6f, 0.85f), new Color(0.7f, 0.55f, 0.55f), golden * 0.6f));
            m.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.38f));
            m.SetFloat("_Exposure", Mathf.Lerp(1.3f, 1.05f, golden));
            return m;
        }
    }
}
