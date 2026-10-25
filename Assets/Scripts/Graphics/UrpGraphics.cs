#if KITEFALL_URP
using Kitefall.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kitefall.Graphics
{
    /// <summary>
    /// URP quality tiers and post-processing (upgrade spec 12, Sky Ops grade v1.5): SRP Batcher on; Bloom, ACES tonemapping,
    /// a punchy colour grade (saturation, contrast, warm white balance, teal-shadow / warm-highlight split toning) and a light
    /// Vignette by tier; Depth of Field in lobby / wardrobe / prep close-ups on High; shadow distance, MSAA and render scale per
    /// tier (Low keeps 30 FPS on 3 GB phones).
    /// <para>
    /// Audit G2: the tiers used to write straight into the project's URP asset, so in the Editor every Play session saved the
    /// last tier's shadow distance / MSAA / render scale into the asset file. The tiers now change a runtime copy of the asset.
    /// </para>
    /// </summary>
    public static class UrpGraphics
    {
        private static Volume _volume;
        private static Bloom _bloom;
        private static Tonemapping _tonemap;
        private static ColorAdjustments _color;
        private static WhiteBalance _white;
        private static SplitToning _split;
        private static Vignette _vignette;
        private static DepthOfField _dof;
        private static UniversalRenderPipelineAsset _runtimeAsset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            KitefallGraphics.QualityHook = Apply;
            KitefallGraphics.CloseUpHook = CloseUp;
        }

        /// <summary>The pipeline asset the game may change: a copy of the project's asset, made once per session.</summary>
        private static UniversalRenderPipelineAsset RuntimeAsset()
        {
            if (_runtimeAsset != null) return _runtimeAsset;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)) return null;
            _runtimeAsset = Object.Instantiate(source);
            _runtimeAsset.name = source.name + " (runtime)";
            QualitySettings.renderPipeline = _runtimeAsset;
            return _runtimeAsset;
        }

        private static void Apply(GraphicsQuality q)
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = true;
            UniversalRenderPipelineAsset asset = RuntimeAsset();
            int msaa = q == GraphicsQuality.High ? 4 : q == GraphicsQuality.Medium ? 2 : 1;
            if (asset != null)
            {
                asset.shadowDistance = q == GraphicsQuality.Low ? 0f : q == GraphicsQuality.Medium ? 60f : 110f;
                asset.msaaSampleCount = msaa;
                asset.renderScale = q == GraphicsQuality.Low ? 0.8f : 1f;
                asset.supportsHDR = q != GraphicsQuality.Low;
            }
            EnsureVolume();
            bool high = q == GraphicsQuality.High;
            bool post = q != GraphicsQuality.Low;

            _bloom.active = post;
            _bloom.intensity.Override(high ? 0.9f : 0.6f);
            _bloom.threshold.Override(0.95f);
            _bloom.scatter.Override(0.7f);
            _bloom.tint.Override(new Color(1f, 0.92f, 0.82f));

            _tonemap.active = post;
            _tonemap.mode.Override(TonemappingMode.ACES);

            // Sky Ops grade: saturated and contrasty like the menus, a touch warm.
            _color.active = true;
            _color.postExposure.Override(post ? 0.12f : 0f);
            _color.saturation.Override(18f);
            _color.contrast.Override(12f);
            _color.colorFilter.Override(new Color(1f, 0.98f, 0.94f));

            _white.active = post;
            _white.temperature.Override(6f);
            _white.tint.Override(2f);

            _split.active = high;
            _split.shadows.Override(new Color(0.44f, 0.5f, 0.56f));
            _split.highlights.Override(new Color(0.58f, 0.52f, 0.45f));
            _split.balance.Override(-15f);

            _vignette.active = post;
            _vignette.intensity.Override(high ? 0.22f : 0.18f);
            _vignette.smoothness.Override(0.45f);
            _dof.active = false;

            Camera cam = Camera.main;
            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = post;
                    // Audit G3: High/Medium already use MSAA – FXAA on top only blurred the image and cost a full-screen pass.
                    data.antialiasing = AntialiasingMode.None;
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
            _white = profile.Add<WhiteBalance>(true);
            _split = profile.Add<SplitToning>(true);
            _vignette = profile.Add<Vignette>(true);
            _dof = profile.Add<DepthOfField>(true);
            _volume.sharedProfile = profile;
        }
    }
}
#endif
