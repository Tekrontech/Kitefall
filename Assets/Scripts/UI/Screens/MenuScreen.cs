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
            Sprite art = Art.ForScreen(GetType().Name);
            if (art != null) ArtBackground(Root, art);
            Image bg = UiKit.Image(Root, new Color(0.04f, 0.07f, 0.12f, art != null ? Mathf.Min(BackgroundAlpha, 0.35f) : BackgroundAlpha), Sprites.White, "Dim", true);
            bg.rectTransform.Stretch();
            RectTransform top = UiKit.Rect("TopBar", Root);
            top.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -110), new Vector2(-24, -16));
            if (ShowBack)
            {
                Glossy.Button(top, "Back", Icons.Get("chevron_left"), () => UI.Pop(), Glossy.Style.Menu, 28).GetComponent<RectTransform>()
                    .Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(180, 80));
            }
            Text title = UiKit.Label(top, Title, Theme.TitleSize, Glossy.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            title.rectTransform.Band(new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(ShowBack ? 200 : 0, 0), Vector2.zero);
            Currencies(top);
            Content = UiKit.Rect("Content", Root).Stretch(24, 130, 24, 24);
            BuildContent(Content);
        }

        protected abstract void BuildContent(RectTransform content);

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
            // Same glossy currency bar as the home screen (icons from the Image Mapper when installed).
            Image bg = Glossy.Panel(top, Glossy.Style.Panel, "Currencies", false);
            bg.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(560, 76));
            CurrencyCell(bg.transform, 0, Art.Sprite("currency_coins") ?? Icons.Get("coin"), Glossy.Gold, p.coins.ToString("N0"));
            CurrencyCell(bg.transform, 1, Art.Sprite("currency_gems") ?? Icons.Get("gem"), Theme.Hex("#4FA8FF"), p.gems.ToString("N0"));
            CurrencyCell(bg.transform, 2, Art.Sprite("currency_parts") ?? Icons.Get("parts"), Theme.Gem, parts + " parts");
        }

        private static void CurrencyCell(Transform bar, int slot, Sprite icon, Color tint, string value)
        {
            RectTransform cell = UiKit.Rect("Cur" + slot, bar);
            cell.Band(new Vector2(slot / 3f, 0), new Vector2((slot + 1) / 3f, 1), new Vector2(12, 8), new Vector2(-6, -8));
            bool art = icon != null && icon.texture != null && icon.texture.width > 96;
            Image ic = UiKit.Image(cell, art ? Color.white : tint, icon, "Icon");
            ic.preserveAspect = true;
            ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(48, 48));
            UiKit.Label(cell, value, 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed().rectTransform.Stretch(54, 0, 0, 0);
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
