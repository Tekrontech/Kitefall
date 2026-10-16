#if KITEFALL_URP
using Kitefall.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kitefall.Graphics
{
    /// <summary>
    /// URP quality tiers and post-processing (upgrade spec 12): SRP Batcher on; Bloom, warm Color Adjustments, ACES tonemapping
    /// and a light Vignette by tier; Depth of Field in lobby / wardrobe / prep close-ups on High; shadow distance, MSAA and
    /// render scale per tier (Low keeps 30 FPS on 3 GB phones).
    /// </summary>
    public static class UrpGraphics
    {
        private static Volume _volume;
        private static Bloom _bloom;
        private static Tonemapping _tonemap;
        private static ColorAdjustments _color;
        private static Vignette _vignette;
        private static DepthOfField _dof;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            KitefallGraphics.QualityHook = Apply;
            KitefallGraphics.CloseUpHook = CloseUp;
        }

        private static void Apply(GraphicsQuality q)
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = true;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                asset.shadowDistance = q == GraphicsQuality.Low ? 0f : q == GraphicsQuality.Medium ? 60f : 110f;
                asset.msaaSampleCount = q == GraphicsQuality.High ? 4 : q == GraphicsQuality.Medium ? 2 : 1;
                asset.renderScale = q == GraphicsQuality.Low ? 0.8f : 1f;
                asset.supportsHDR = q != GraphicsQuality.Low;
            }
            EnsureVolume();
            _bloom.active = q != GraphicsQuality.Low;
            _bloom.intensity.Override(q == GraphicsQuality.High ? 0.7f : 0.45f);
            _bloom.threshold.Override(1.0f);
            _bloom.scatter.Override(0.65f);
            _tonemap.active = q != GraphicsQuality.Low;
            _tonemap.mode.Override(TonemappingMode.ACES);
            _color.active = true;
            _color.postExposure.Override(q == GraphicsQuality.Low ? 0f : 0.15f);
            _color.saturation.Override(12f);
            _color.contrast.Override(8f);
            _color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
            _vignette.active = q != GraphicsQuality.Low;
            _vignette.intensity.Override(0.18f);
            _dof.active = false;

            Camera cam = Camera.main;
            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = q != GraphicsQuality.Low;
                    data.antialiasing = q == GraphicsQuality.Low ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
                }
            }
        }

        private static void CloseUp(bool on)
        {
            EnsureVolume();
            _dof.active = on;
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(4f);
            _dof.gaussianEnd.Override(18f);
        }

        private static void EnsureVolume()
        {
            if (_volume != null) return;
            var go = new GameObject("Kitefall PostProcess");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>(true);
            _tonemap = profile.Add<Tonemapping>(true);
            _color = profile.Add<ColorAdjustments>(true);
            _vignette = profile.Add<Vignette>(true);
            _dof = profile.Add<DepthOfField>(true);
            _volume.sharedProfile = profile;
        }
    }
}
#endif
