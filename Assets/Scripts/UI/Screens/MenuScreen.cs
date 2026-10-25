using Kitefall.App;
using Kitefall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Common frame for menus: dim background, back button, title, currencies.</summary>
    public abstract class MenuScreen : UIScreen
    {
        public override bool Opaque => false;
        protected virtual bool ShowBack => true;
        protected virtual float BackgroundAlpha => 0.72f;
        protected RectTransform Content { get; private set; }

        protected sealed override void Build()
        {
            Sprite art = BackgroundArt;
            if (art != null) ArtBackground(Root, art);
            // Screens that show the 3D scene behind them (Wardrobe, Hangar, Squad) keep it; the rest get the Sky Ops backdrop:
            // the home painting, blurred and darkened, under fine diagonal hairlines.
            else if (BackgroundAlpha >= 0.6f) Backdrop(Root);
            float dim = art != null ? Mathf.Min(BackgroundAlpha, 0.35f) : BackgroundAlpha >= 0.6f ? 0.35f : BackgroundAlpha;
            Image bg = UiKit.Image(Root, new Color(0.02f, 0.04f, 0.08f, dim), Sprites.White, "Dim", true);
            bg.rectTransform.Stretch();
            Image topShade = UiKit.Image(Root, new Color(0.02f, 0.03f, 0.06f, 0.7f), Sprites.VerticalFade, "TopShade");
            topShade.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), Vector2.zero);

            RectTransform top = UiKit.Rect("TopBar", Root);
            top.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -124), new Vector2(-24, -16));
            if (ShowBack)
            {
                Button back = Glossy.Button(top, "", Icons.Get("chevron_left"), () => UI.Pop(), Glossy.Style.Menu, 30);
                back.GetComponent<RectTransform>().Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(88, 88));
                Transform ic = back.transform.Find("Icon");
                if (ic != null) ((RectTransform)ic).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            }
            float x = ShowBack ? 118 : 0;
            Text kicker = UiKit.Label(top, UiKit.Caps(Kicker ?? ""), 19, Theme.Hex("#FFB21A"), TextAnchor.UpperLeft, FontStyle.Bold, "Kicker");
            UiKit.UseButtonFont(kicker, false);
            kicker.rectTransform.Band(new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(x + 2, -30), new Vector2(0, 0));
            Text title = UiKit.Display(UiKit.Label(top, Title, 60, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold, "Title")).Shadowed();
            title.rectTransform.Band(new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(x, 6), new Vector2(0, -22));
            Image underline = UiKit.Image(top, Theme.Hex("#FF9A10"), Sprites.Slant, "Underline");
            underline.type = Image.Type.Sliced;
            underline.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, -2), new Vector2(130, 7));
            Currencies(top);
            Content = UiKit.Rect("Content", Root).Stretch(24, 144, 24, 24);
            BuildContent(Content);
        }

        protected abstract void BuildContent(RectTransform content);

        /// <summary>Small amber line above the title ("Kites &amp; loadout"); override per screen.</summary>
        protected virtual string Kicker
        {
            get
            {
                switch (GetType().Name)
                {
                    case "HangarScreen": return "Kites & loadout";
                    case "WorkshopScreen": return "Craft · repair · tune";
                    case "ShopScreen": return "New this week";
                    case "SeasonPassScreen": return "Season rewards";
                    case "WardrobeScreen": return "Characters & style";
                    case "SquadLobbyScreen": return "Play together";
                    case "TrainingScreen": return "Practice your cuts";
                    case "LeaderboardScreen": return "Season ranking";
                    case "FriendsScreen": return "Your flyers";
                    case "ProfileScreen": return "Your record";
                    case "InboxScreen": return "Messages & gifts";
                    case "SettingsScreen": return "Make it yours";
                    case "MatchmakingScreen": return "Online";
                    case "PrivateRoomScreen": return "Play with friends";
                    case "ResultsScreen": return "Match over";
                    default: return null;
                }
            }
        }

        private static Texture _backdrop;

        /// <summary>
        /// Full-screen menu backdrop: the home painting shrunk to 1/10 size on the GPU (bilinear upscaling = a cheap blur),
        /// darkened, under diagonal hairlines. Falls back to a dusk gradient when the painting is not installed.
        /// </summary>
        public static void Backdrop(RectTransform parent)
        {
            if (_backdrop == null)
            {
                Texture2D src = Art.Texture("home_background");
                if (src != null)
                {
                    var rt = new RenderTexture(192, 108, 0, RenderTextureFormat.ARGB32) { name = "MenuBackdrop", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    var half = RenderTexture.GetTemporary(480, 270, 0, RenderTextureFormat.ARGB32);
                    UnityEngine.Graphics.Blit(src, half);
                    UnityEngine.Graphics.Blit(half, rt);
                    RenderTexture.ReleaseTemporary(half);
                    _backdrop = rt;
                }
                else _backdrop = DuskGradient();
            }
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var raw = go.AddComponent<RawImage>();
            raw.texture = _backdrop;
            raw.raycastTarget = false;
            raw.color = new Color(0.62f, 0.6f, 0.62f, 1f);
            raw.rectTransform.Stretch(-40, -40, -40, -40);
            var fit = go.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;
            var hatchGo = new GameObject("Hatch", typeof(RectTransform));
            hatchGo.transform.SetParent(parent, false);
            var hatch = hatchGo.AddComponent<RawImage>();
            hatch.texture = Sprites.Hatch;
            hatch.raycastTarget = false;
            hatch.uvRect = new Rect(0, 0, 1920f / 32f, 1080f / 32f);
            hatch.rectTransform.Stretch();
        }

        private static Texture2D DuskGradient()
        {
            const int w = 64, h = 36;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Dusk" };
            var px = new Color[w * h];
            Color top = Theme.Hex("#0B1426"), mid = Theme.Hex("#3A2240"), low = Theme.Hex("#B4542A");
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (h - 1f);
                    Color c = v > 0.45f ? Color.Lerp(mid, top, (v - 0.45f) / 0.55f) : Color.Lerp(low, mid, v / 0.45f);
                    float gx = (x - w * 0.5f) / (w * 0.5f), gy = (y - h * 0.3f) / (h * 0.5f);
                    c += new Color(0.35f, 0.18f, 0.05f) * Mathf.Clamp01(1f - (gx * gx + gy * gy)) * 0.6f;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Painted full-screen background (Image Mapper), or null to keep the 3D scene behind the dim.</summary>
        protected virtual Sprite BackgroundArt => Art.ForScreen(GetType().Name);

        /// <summary>
        /// Picture filling a button / panel / banner without stretching (crops the edges), clipped to the parent's rounded
        /// shape and drawn behind its icon and label. Optional dark shade keeps the text readable. Not part of any layout.
        /// </summary>
        public static Image ArtFill(Transform parent, Sprite art, float inset = 0f, float shade = 0f, Sprite clipShape = null)
        {
            RectTransform clip;
            if (clipShape != null)
            {
                // Rounded skins (glossy buttons, panels): stencil mask in the same 9-sliced shape.
                Image shape = UiKit.Image(parent, Color.white, clipShape, "ArtClip");
                shape.type = Image.Type.Sliced;
                shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                clip = shape.rectTransform;
            }
            else
            {
                clip = UiKit.Rect("ArtClip", parent);
                clip.gameObject.AddComponent<RectMask2D>();
            }
            clip.Stretch(inset, inset, inset, inset);
            clip.SetAsFirstSibling();
            clip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image img = UiKit.Image(clip, Color.white, art, "Art");
            img.type = Image.Type.Simple;
            img.rectTransform.Stretch();
            var fit = img.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = art.rect.width / Mathf.Max(1f, art.rect.height);
            if (shade > 0f) UiKit.Image(clip, new Color(0.02f, 0.05f, 0.12f, shade), Sprites.White, "Shade").rectTransform.Stretch();
            return img;
        }

        /// <summary>Full-screen painted background that fills the screen without stretching (crops the edges).</summary>
        public static Image ArtBackground(RectTransform parent, Sprite art)
        {
            Image img = UiKit.Image(parent, Color.white, art, "ArtBackground");
            img.rectTransform.Stretch();
            var fit = img.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = art.rect.width / Mathf.Max(1f, art.rect.height);
            return img;
        }

        public static void Currencies(RectTransform top)
        {
            PlayerProfile p = Game.Profile;
            if (p == null) return;
            int parts = 0;
            foreach (ItemCount c in p.parts) parts += c.count;
            // Sky Ops currency pill: coin + gems with amber "+" (opens the Shop), parts count (icons from the Image Mapper when installed).
            Image bg = Glossy.Panel(top, Glossy.Style.Pill, "Currencies", false);
            bg.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(560, 70));
            CurrencyCell(bg.transform, 0, Art.Sprite("currency_coins") ?? Icons.Get("coin"), Glossy.Gold, p.coins.ToString("N0"), true);
            CurrencyCell(bg.transform, 1, Art.Sprite("currency_gems") ?? Icons.Get("gem"), Theme.GemBlue, p.gems.ToString("N0"), true);
            CurrencyCell(bg.transform, 2, Art.Sprite("currency_parts") ?? Icons.Get("parts"), Theme.Gem, parts.ToString(), false);
            for (int i = 1; i < 3; i++)
            {
                Image div = UiKit.Image(bg.transform, new Color(0.63f, 0.75f, 0.94f, 0.25f), Sprites.White, "Divider");
                div.rectTransform.Band(new Vector2(i / 3f, 0.22f), new Vector2(i / 3f, 0.78f), new Vector2(-1, 0), new Vector2(1, 0));
            }
        }

        private static void CurrencyCell(Transform bar, int slot, Sprite icon, Color tint, string value, bool plus)
        {
            RectTransform cell = UiKit.Rect("Cur" + slot, bar);
            cell.Band(new Vector2(slot / 3f, 0), new Vector2((slot + 1) / 3f, 1), new Vector2(14, 8), new Vector2(-8, -8));
            bool art = icon != null && icon.texture != null && icon.texture.width > 96;
            Image ic = UiKit.Image(cell, art ? Color.white : tint, icon, "Icon");
            ic.preserveAspect = true;
            ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(40, 40));
            Text t = UiKit.Label(cell, value, 26, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            UiKit.UseButtonFont(t, false);
            t.rectTransform.Stretch(46, 0, plus ? 40 : 0, 0);
            if (!plus) return;
            Button add = Glossy.Button(cell, "+", null, () => { if (!(Game.UI.Current is ShopScreen)) Game.UI.Push(new ShopScreen()); }, Glossy.Style.Orange, 26);
            add.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(36, 36));
        }

        /// <summary>Runs a backend op, shows the result, refreshes the screen.</summary>
        protected async void Run(string op, params string[] args)
        {
            UI.SetBusy(true);
            OpResult r = await Game.Backend.RunAsync(op, args);
            UI.SetBusy(false);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }

        protected static Image Card(Transform parent, bool selected = false) =>
            UiKit.Panel(parent, selected ? Theme.CardSelected : Theme.Card, "Card");
    }
}
