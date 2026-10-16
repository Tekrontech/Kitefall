using System;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>
    /// Graphics tiers (upgrade spec 12, spec v1.2 section 15). Pipeline-specific work (URP post-processing volume,
    /// shadow distance, MSAA, render scale) lives in the optional Kitefall.Graphics assembly, which registers the hooks below
    /// when URP is installed – so this assembly never depends on a render pipeline.
    /// </summary>
    public static class KitefallGraphics
    {
        /// <summary>Applies a quality tier (set by Kitefall.Graphics when URP is present).</summary>
        public static Action<GraphicsQuality> QualityHook;
        /// <summary>Lobby / wardrobe / prep close-ups: depth of field on High (set by Kitefall.Graphics).</summary>
        public static Action<bool> CloseUpHook;

        public static GraphicsQuality Current { get; private set; } = GraphicsQuality.Medium;

        public static void Apply(GraphicsQuality q)
        {
            Current = q;
            // Characters: other players beyond 30 m drop face details (cheap LOD, upgrade 12).
            CharacterDetailDistance = q == GraphicsQuality.Low ? 18f : q == GraphicsQuality.Medium ? 30f : 45f;
            QualityHook?.Invoke(q);
        }

        public static float CharacterDetailDistance { get; private set; } = 30f;

        public static void SetCloseUp(bool on) => CloseUpHook?.Invoke(on && Current == GraphicsQuality.High);

        /// <summary>Points the gradient sky's sun at the real sun.</summary>
        public static void ApplySunHint(Light sun, float golden)
        {
            Material sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_SunDir")) sky.SetVector("_SunDir", -sun.transform.forward);
        }
    }
}
