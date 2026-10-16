using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Season Pass (spec 4.4): 50 levels, free + premium track, cosmetics and parts only.</summary>
    public sealed class SeasonPassScreen : MenuScreen
    {
        public override string Title => "Season Pass";

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            EconomyConfig c = Game.Economy;
            int level = EconomyService.PassLevel(p, c);
            float progress = level >= c.passLevels ? 1f : (p.passXp % c.passXpPerLevel) / (float)c.passXpPerLevel;

            RectTransform head = UiKit.Row(content, 16);
            head.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), Vector2.zero);
            RectTransform info = UiKit.Column(head, 6);
            info.Size(-1, 96, 1);
            UiKit.Label(info, $"Level <b>{level}</b>/{c.passLevels}   ·   {p.passXp % c.passXpPerLevel}/{c.passXpPerLevel} XP   ·   {(p.passPremium ? "<color=#FFD24D>PREMIUM</color>" : "Free track")}", 30).Size(-1, 44);
            UiKit.Bar(info, progress, Theme.Accent, 16f);
            if (!p.passPremium)
            {
                UiKit.Button(head, $"Premium {c.premiumPassGems} gems", () => UI.Confirm("Unlock Premium?", "Premium track: extra cosmetics, gems and parts. Also removes interstitial ads.", "Unlock", () => Run(EconomyCommands.BuyPassGems)), Theme.Gem, 24).Size(300, 84);
                UiKit.Button(head, Game.Purchases.Price("kitefall.pass.premium"), () => BuyPremium(), Theme.Good, 24).Size(200, 84);
            }
            if (Game.Ads.CanShowRewarded(RewardedPlacement.PassBonusXp, AdContext.SeasonPass))
                UiKit.Button(head, $"+{c.adPassXp} XP (ad)", () => Ad(), Theme.Accent2, 24).Size(220, 84);

            RectTransform host = UiKit.Rect("Levels", content);
            host.Band(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -116));
            ScrollRect scroll = UiKit.Scroll(host, out RectTransform list, false, 14);
            scroll.GetComponent<RectTransform>().Stretch();
            for (int lvl = 1; lvl <= c.passLevels; lvl++)
            {
                Image card = Card(list, lvl == level);
                card.Size(230, -1);
                RectTransform col = UiKit.Column(card.transform, 10, 12);
                col.Stretch();
                UiKit.Label(col, $"Level {lvl}", 28, lvl <= level ? Theme.Accent : Theme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold).Size(-1, 40);
                Reward(col, p, lvl, false, level);
                Reward(col, p, lvl, true, level);
            }
        }

        private void Reward(RectTransform col, PlayerProfile p, int lvl, bool premium, int level)
        {
            PassReward r = Catalog.PassRewards.Find(x => x.level == lvl && x.premium == premium);
            string text = r == null ? "" : !string.IsNullOrEmpty(r.cosmeticId) ? (Catalog.Cosmetic(r.cosmeticId)?.name ?? "Cosmetic")
                : r.gems > 0 ? $"{r.gems} gems" : r.parts > 0 ? $"{r.parts} {r.partId}" + (r.coins > 0 ? $" + {r.coins} coins" : "") : $"{r.coins} coins";
            Image box = UiKit.Panel(col, premium ? new Color(0.35f, 0.25f, 0.05f, 0.9f) : Theme.PanelLight, premium ? "Premium" : "Free");
            box.Size(-1, 210);
            RectTransform inner = UiKit.Column(box.transform, 6, 10);
            inner.Stretch();
            UiKit.Label(inner, (premium ? "<color=#FFD24D>PREMIUM</color>\n" : "FREE\n") + text, 22, Color.white, TextAnchor.MiddleCenter).Size(-1, 120);
            bool claimed = premium ? p.passClaimedPremium.Contains(lvl) : p.passClaimedFree.Contains(lvl);
            bool can = lvl <= level && !claimed && (!premium || p.passPremium);
            int l = lvl;
            UiKit.Button(inner, claimed ? "Claimed" : can ? "Claim" : premium && !p.passPremium ? "Locked" : "—", () => Run(EconomyCommands.ClaimPass, l.ToString(), premium ? "1" : "0"), can ? Theme.Good : Theme.Disabled, 22)
                .Interactable(can).Size(-1, 56);
        }

        private async void BuyPremium()
        {
            OpResult r = await Game.Purchases.BuyAsync("kitefall.pass.premium");
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }

        private async void Ad()
        {
            OpResult r = await Game.Ads.ShowRewardedAsync(RewardedPlacement.PassBonusXp, AdContext.SeasonPass);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }
    }
}
