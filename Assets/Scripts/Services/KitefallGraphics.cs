using System;
using UnityEngine;
using UnityEngine.Rendering;

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

        // ---- Render pipeline check (audit R9) -------------------------------------------------------------------------

        private static bool _pipelineChecked;
        private static string _pipelineProblem;

        /// <summary>
        /// Null when URP is the active render pipeline; otherwise a short plain-English description of what is wrong and how to
        /// fix it (shown as a developer banner on Home in the Editor / development builds). Without URP every material is
        /// pink or flat and the post-processing never runs.
        /// </summary>
        public static string PipelineProblem
        {
            get { CheckPipeline(); return _pipelineProblem; }
        }

        /// <summary>Runs once at startup (after the first scene loads) and logs ONE clear error when URP is not active.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckPipeline()
        {
            if (_pipelineChecked) return;
            _pipelineChecked = true;
#if UNITY_SERVER
            return; // dedicated server renders nothing
#else
            RenderPipelineAsset active = GraphicsSettings.currentRenderPipeline;
            string level = QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?";
            if (active == null)
                _pipelineProblem = $"No render pipeline asset is assigned (quality level \"{level}\"), so Unity uses the old Built-in renderer " +
                                   "and Kitefall's URP materials and effects do not work.";
            else if (!IsUrp(active))
                _pipelineProblem = $"The active render pipeline is {active.GetType().Name} (\"{active.name}\"), not URP.";
            else
            {
                // URP is active now – still warn about quality levels that would switch it off (phones often use Low).
                var bad = new System.Collections.Generic.List<string>();
                bool defaultUrp = IsUrp(GraphicsSettings.defaultRenderPipeline);
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    RenderPipelineAsset q = QualitySettings.GetRenderPipelineAssetAt(i);
                    if (q != null ? !IsUrp(q) : !defaultUrp) bad.Add(QualitySettings.names[i]);
                }
                if (bad.Count > 0)
                    _pipelineProblem = "URP is active now, but these quality levels have no URP asset: " + string.Join(", ", bad) + ".";
            }
            if (_pipelineProblem == null) return;
            _pipelineProblem += " Fix: (1) Window ▸ Package Manager: the Universal RP package is installed. (2) If you have no URP asset yet: " +
                                "Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer). (3) Edit ▸ Project Settings ▸ Graphics ▸ " +
                                "Default Render Pipeline = that URP asset. (4) Edit ▸ Project Settings ▸ Quality ▸ click EACH level ▸ " +
                                "Render Pipeline Asset = the URP asset (or empty to use the default). Then press Play again.";
            Debug.LogError("[Kitefall] Render pipeline problem: " + _pipelineProblem);
#endif
        }

        /// <summary>URP's asset type lives in a package this assembly does not reference, so it is matched by name.</summary>
        private static bool IsUrp(RenderPipelineAsset a) => a != null && a.GetType().Name.Contains("UniversalRenderPipeline");

        /// <summary>Points the gradient sky's sun at the real sun.</summary>
        public static void ApplySunHint(Light sun, float golden)
        {
            Material sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_SunDir")) sky.SetVector("_SunDir", -sun.transform.forward);
        }
    }
}
