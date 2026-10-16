using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Shop (spec 4.4): cosmetics only, gems, no-ads. Never stats (rule 1).</summary>
    public sealed class ShopScreen : MenuScreen
    {
        public override string Title => "Shop";
        private static int _tab;
        private static readonly string[] Tabs = { "Featured", "Kite skins", "Dum effects", "Cut effects", "Dor skins", "Charkhi skins", "Gems & No-ads" };

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            RectTransform tabs = UiKit.Rect("Tabs", content);
            tabs.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), Vector2.zero);
            UiKit.Tabs(tabs, Tabs, _tab, i => { _tab = i; Refresh(); });
            RectTransform host = UiKit.Rect("List", content);
            host.Band(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -90));
            UiKit.Scroll(host, out RectTransform list).GetComponent<RectTransform>().Stretch();

            if (_tab == 6) { Gems(list, p); return; }
            List<CosmeticItem> items;
            switch (_tab)
            {
                case 0: items = Catalog.FeaturedFor(EconomyService.Day(Game.Now)); break;
                case 1: items = Catalog.Cosmetics.FindAll(c => c.type == CosmeticType.KiteSkin && !c.passOnly); break;
                case 2: items = Catalog.Cosmetics.FindAll(c => c.type == CosmeticType.DumEffect); break;
                case 3: items = Catalog.Cosmetics.FindAll(c => c.type == CosmeticType.CutEffect); break;
                case 4: items = Catalog.Cosmetics.FindAll(c => c.type == CosmeticType.DorSkin); break;
                default: items = Catalog.Cosmetics.FindAll(c => c.type == CosmeticType.CharkhiSkin); break;
            }
            RectTransform grid = UiKit.Grid(list, new Vector2(400, 300), new Vector2(16, 16), 4);
            foreach (CosmeticItem item in items) Tile(grid, item, p);
        }

        private void Tile(RectTransform grid, CosmeticItem item, PlayerProfile p)
        {
            Image card = Card(grid);
            RectTransform col = UiKit.Column(card.transform, 6, 14);
            col.Stretch();
            Image swatch = UiKit.Image(col, KiteVisualFactory.Hex(item.primaryHex), Sprites.Rounded, "Swatch");
            swatch.Size(-1, 110);
            Image accent = UiKit.Image(swatch.transform, KiteVisualFactory.Hex(item.secondaryHex), Sprites.Rounded, "Accent");
            accent.rectTransform.Band(new Vector2(0.6f, 0.15f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);
            Color rc = Theme.RarityColors[(int)item.rarity];
            UiKit.Label(col, $"<b>{item.name}</b>\n<size=20><color=#{ColorUtility.ToHtmlStringRGB(rc)}>{item.rarity}</color> · {item.type}</size>", 24).Size(-1, 70);
            int owned = p.Cosmetic(item.id);
            bool equipped = p.equippedCosmetics.Contains(item.id);
            string id = item.id;
            if (owned > 0)
                UiKit.Button(col, equipped ? "Equipped" : "Equip", () => Run(EconomyCommands.Equip, id), equipped ? Theme.Good : Theme.Accent2, 22).Interactable(!equipped).Size(-1, 60);
            else
            {
                RectTransform row = UiKit.Row(col, 6);
                row.Size(-1, 60);
                UiKit.Button(row, UiKit.Gems(item.gemPrice), () => Buy(item, false), Theme.PanelLight, 22).Size(-1, 58, 1);
                if (item.coinPrice > 0) UiKit.Button(row, UiKit.Coins(item.coinPrice), () => Buy(item, true), Theme.PanelLight, 22).Size(-1, 58, 1);
            }
        }

        private void Buy(CosmeticItem item, bool coins)
        {
            UI.Confirm("Buy " + item.name + "?", "Cosmetic only – it changes how things look, never how they fly.",
                coins ? $"Buy for {item.coinPrice} coins" : $"Buy for {item.gemPrice} gems", () => Run(EconomyCommands.BuyCosmetic, item.id, coins ? "1" : "0"));
        }

        private void Gems(RectTransform list, PlayerProfile p)
        {
            UiKit.Label(list, $"Store: {Game.Purchases.Provider?.Name}", 22, Theme.TextDim).Size(-1, 36);
            foreach (StoreProduct prod in Catalog.Products)
            {
                Image card = Card(list);
                card.Size(-1, 110);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                bool owned = prod.noAds && p.noAds || prod.premiumPass && p.passPremium;
                string desc = prod.gems > 0 ? $"{prod.gems} gems" : prod.noAds ? "Removes interstitial ads. Rewarded ad buttons stay." : "Premium track of the Season Pass + no interstitials.";
                UiKit.Label(row, $"<b>{prod.name}</b>\n<size=22>{desc}</size>", 28).Size(-1, 90, 1);
                string id = prod.productId;
                UiKit.Button(row, owned ? "Owned" : Game.Purchases.Price(id), () => BuyProduct(id), Theme.Good, 26).Interactable(!owned).Size(200, 76);
            }
            if (Game.Ads.CanShowRewarded(RewardedPlacement.FreeGems, AdContext.Shop))
                UiKit.Button(list, $"Free gems: watch an ad (+{Game.Economy.adFreeGems}) · {Game.Ads.RemainingToday(RewardedPlacement.FreeGems)} left today", () => Ad(), Theme.Accent2, 26).Size(-1, 90);
            UiKit.Button(list, "Restore purchases", () => Game.Purchases.Provider.RestorePurchases(ok => UI.Toast(ok ? "Purchases restored" : "Nothing to restore")), Theme.PanelLight, 24).Size(-1, 76);
        }

        private async void BuyProduct(string id)
        {
            UI.SetBusy(true, "Contacting the store…");
            OpResult r = await Game.Purchases.BuyAsync(id);
            UI.SetBusy(false);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }

        private async void Ad()
        {
            OpResult r = await Game.Ads.ShowRewardedAsync(RewardedPlacement.FreeGems, AdContext.Shop);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }
    }
}
