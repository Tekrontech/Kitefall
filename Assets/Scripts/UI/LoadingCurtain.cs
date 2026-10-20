using Kitefall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// The arena's loading picture (Image Mapper slots loading_01_oldcity … loading_bali_terraces) shown full screen while a
    /// match starts, then faded away. Nothing is shown when the picture is not installed.
    /// </summary>
    public sealed class LoadingCurtain : MonoBehaviour
    {
        private const float Hold = 1.4f, Fade = 0.5f;
        private CanvasGroup _group;
        private float _t;

        public static void Show(UIManager ui, ArenaType arena, string title)
        {
            Sprite art = Art.Sprite(SlotFor(arena));
            if (ui == null || art == null) return;
            RectTransform root = UiKit.Rect("LoadingCurtain", ui.ToastLayer);
            root.Stretch();
            UiKit.Image(root, Color.black, Sprites.White, "Back").rectTransform.Stretch();
            Image pic = Kitefall.UI.Screens.MenuScreen.ArtBackground(root, art);
            pic.raycastTarget = true; // swallow taps while it covers the screen
            if (!string.IsNullOrEmpty(title))
            {
                Text t = UiKit.Label(root, title, 44, Color.white, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
                t.rectTransform.Band(Vector2.zero, new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
            }
            var c = root.gameObject.AddComponent<LoadingCurtain>();
            c._group = root.gameObject.AddComponent<CanvasGroup>();
        }

        public static string SlotFor(ArenaType arena)
        {
            switch (arena)
            {
                case ArenaType.OldCityRooftops: return "loading_01_oldcity";
                case ArenaType.CoastalCliffs: return "loading_02_coast";
                case ArenaType.RiverFestival: return "loading_03_river";
                case ArenaType.RoyalField: return "loading_04_royal";
                case ArenaType.MountainValley: return "loading_05_mountain";
                case ArenaType.StormArchipelago: return "loading_06_storm";
                case ArenaType.RioHillside: return "loading_rio_hillside";
                case ArenaType.SantiagoRooftops: return "loading_santiago_rooftops";
                case ArenaType.MediterraneanHarbour: return "loading_mediterranean_harbour";
                case ArenaType.AtlanticBeach: return "loading_atlantic_beach";
                case ArenaType.CaribbeanShore: return "loading_caribbean_shore";
                default: return "loading_bali_terraces";
            }
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01((_t - Hold) / Fade);
            if (_t > Hold * 0.5f) _group.blocksRaycasts = false;
            if (_t >= Hold + Fade) Destroy(gameObject);
        }
    }
}
