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
            key = key ?? ("lit_" + ColorUtility.ToHtmlStringRGBA(color) + smoothness + (texture != null ? texture.GetInstanceID().ToString() : ""));
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            Shader shader = (Urp ? Shader.Find("Universal Render Pipeline/Lit") : null) ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            m = new Material(shader) { name = key, color = color };
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

        public static Material Skybox(float timeOfDay01)
        {
            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky == null) return null;
            var m = new Material(sky) { name = "Sky" };
            float golden = Mathf.Abs(timeOfDay01 - 0.5f) * 2f; // 0 noon, 1 morning/evening
            m.SetFloat("_SunSize", Mathf.Lerp(0.04f, 0.07f, golden));
            m.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.9f, 1.6f, golden));
            m.SetColor("_SkyTint", Color.Lerp(new Color(0.45f, 0.6f, 0.85f), new Color(0.7f, 0.55f, 0.55f), golden * 0.6f));
            m.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.38f));
            m.SetFloat("_Exposure", Mathf.Lerp(1.3f, 1.05f, golden));
            return m;
        }
    }
}
