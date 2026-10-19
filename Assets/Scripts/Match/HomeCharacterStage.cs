using Kitefall.App;
using Kitefall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.Match
{
    /// <summary>
    /// The player's own selected character on the painted home picture: the character (with outfit and accessories) stands on a
    /// hidden stage far below the world, flying an invisible kite, and a small camera films them from behind into a transparent
    /// RenderTexture. The home screen shows that texture where the painted flyer used to stand, clipped at the parapet, so it
    /// looks like the player is on the rooftop in the picture. Changes in the wardrobe show up immediately.
    /// </summary>
    public sealed class HomeCharacterStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(0f, -400f, 0f);
        private static HomeCharacterStage _instance;

        private Camera _cam;
        private RenderTexture _rt;
        private CharacterView _view;
        private Light _light;
        private string _key;
        private float _time;
        private RawImage _image;
        private RectTransform _imageRect;

        /// <summary>Where the line leaves the character's hand, in the RawImage's 0..1 space (for the kite line overlay).</summary>
        public Vector2 HandViewport { get; private set; } = new Vector2(0.75f, 0.62f);

        public static HomeCharacterStage Get()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("HomeCharacterStage");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HomeCharacterStage>();
            _instance.Setup();
            return _instance;
        }

        private void Setup()
        {
            transform.position = Origin;
            _rt = new RenderTexture(512, 640, 16, RenderTextureFormat.ARGB32) { name = "HomeCharacter", antiAliasing = 2 };
            var camGo = new GameObject("StageCamera");
            camGo.transform.SetParent(transform, false);
            // Behind the flyer, a little to the right, looking at the upper body – the back view of the design.
            camGo.transform.localPosition = new Vector3(0.55f, 1.45f, -3.1f);
            camGo.transform.LookAt(Origin + new Vector3(0.05f, 1.25f, 0f));
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 20f;
            _cam.targetTexture = _rt;
            _cam.allowHDR = false;
            _cam.allowMSAA = true;

            // Warm low sun from the front-right, like the sunset in the picture (rim light on the character's back).
            var lightGo = new GameObject("StageLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(18f, -150f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.color = new Color(1f, 0.82f, 0.62f);
            _light.intensity = 1.1f;
            _light.cullingMask = ~0;
        }

        /// <summary>Shows the player's current look; rebuilds only when it changed.</summary>
        public Texture Show(PlayerProfile p)
        {
            string character = p?.characterId ?? CharacterCatalog.DefaultCharacter;
            string outfit = p?.outfitId ?? "";
            string[] acc = p != null ? p.accessories.ToArray() : new string[4];
            string kite = p?.loadout.kite ?? "guddi";
            string charkhi = p?.loadout.charkhi ?? "lakri";
            string key = character + "|" + outfit + "|" + string.Join(",", acc) + "|" + kite + "|" + charkhi;
            if (key != _key || _view == null)
            {
                _key = key;
                if (_view != null) Destroy(_view.gameObject);
                _view = new GameObject("StageCharacter").AddComponent<CharacterView>();
                _view.transform.SetParent(transform, false);
                _view.transform.localPosition = Vector3.zero;
                _view.Init(CharacterBuilder.LookFor(character, outfit, acc, charkhi, kite, ""), "", new Color(0.98f, 0.58f, 0.15f), true, false);
            }
            gameObject.SetActive(true);
            return _rt;
        }

        /// <summary>The home screen's RawImage; the stage camera only renders while it is on screen.</summary>
        public void Bind(RawImage image)
        {
            _image = image;
            _imageRect = image != null ? image.rectTransform : null;
        }

        private void Update()
        {
            bool visible = _image != null && _image.isActiveAndEnabled;
            if (_cam != null) _cam.enabled = visible;
            if (_light != null) _light.enabled = visible;
            if (!visible || _view == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _time += dt;
            // The (invisible) kite is up, ahead and to the right – the same place as the kite in the picture.
            Vector3 kite = Origin + new Vector3(4.5f + Mathf.Sin(_time * 0.55f) * 0.6f, 9f + Mathf.Sin(_time * 1.1f) * 0.5f, 12f);
            var f = new CharacterFrame
            {
                State = FlyerState.Flying,
                Action = Mathf.Sin(_time * 0.7f) > 0.6f ? LineAction.Pull : LineAction.Idle,
                Position = Origin,
                KitePosition = kite,
                KiteValid = true,
                WindDirection = 0f,
                Time = _time
            };
            _view.Apply(f, dt);
            if (_view.LineAnchor != null)
            {
                Vector3 vp = _cam.WorldToViewportPoint(_view.LineAnchor.position);
                HandViewport = new Vector2(vp.x, vp.y);
            }
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
            if (_instance == this) _instance = null;
        }
    }
}
