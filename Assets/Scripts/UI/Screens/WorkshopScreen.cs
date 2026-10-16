using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Workshop (spec 4.3): craft, repair, spools, charkhi, tuning, salvage – all instant, no timers.</summary>
    public sealed class WorkshopScreen : MenuScreen
    {
        public override string Title => "Workshop";
        private static int _tab;

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            EconomyConfig c = Game.Economy;
            RectTransform tabs = UiKit.Rect("Tabs", content);
            tabs.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), Vector2.zero);
            UiKit.Tabs(tabs, new[] { "Craft kite", "Repair", "Dor spools", "Charkhi", "Tuning", "Salvage" }, _tab, i => { _tab = i; Refresh(); });

            RectTransform host = UiKit.Rect("List", content);
            host.Band(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -90));
            UiKit.Scroll(host, out RectTransform list).GetComponent<RectTransform>().Stretch();
            UiKit.Label(list, $"Parts:  sail {p.Part(PartIds.Sail)} · sticks {p.Part(PartIds.Sticks)} · line {p.Part(PartIds.Line)} · wood {p.Part(PartIds.Wood)} · gears {p.Part(PartIds.Gears)}", 26, Theme.TextDim).Size(-1, 50);

            switch (_tab)
            {
                case 0:
                    foreach (SpecDefaults.KiteInfo k in SpecDefaults.Kites)
                    {
                        string id = k.Stats.id;
                        int lvl = Catalog.KiteUnlockLevel(id);
                        Line(list, $"<b>{k.Name}</b>  ×{p.Kite(id)?.count ?? 0}", $"{c.craftKiteSail} sail · {c.craftKiteSticks} sticks · {c.craftKiteCoins} coins" + (p.level < lvl ? $"  <color=#F5C400>(level {lvl})</color>" : ""),
                            "Craft", () => Run(EconomyCommands.CraftKite, id), p.level >= lvl);
                    }
                    UiKit.Label(list, "Rare / Epic looks are cosmetic only – see the Shop.", 24, Theme.TextDim).Size(-1, 50);
                    break;
                case 1:
                    foreach (KiteOwned k in p.kites)
                    {
                        if (k.count <= 0) continue;
                        string id = k.id;
                        int cost = EconomyService.RepairCost(p, id, c);
                        bool needs = k.durability < 100f;
                        RectTransform row = Line(list, $"<b>{SpecDefaults.NameOf(id)}</b> ×{k.count}  durability {k.durability:0}%", needs ? $"{cost} coins" : "Full", "Repair", () => Run(EconomyCommands.Repair, id), needs);
                        if (needs && Game.Ads.CanShowRewarded(RewardedPlacement.FreeRepair, AdContext.Workshop))
                            UiKit.Button(row, $"Free (ad) {Game.Ads.RemainingToday(RewardedPlacement.FreeRepair)}/3", () => Ad(RewardedPlacement.FreeRepair, id), Theme.Accent2, 22).Size(220, 76);
                    }
                    break;
                case 2:
                    foreach (var kv in SpecDefaults.Dors)
                    {
                        string id = kv.Key;
                        int lvl = kv.Value.unlockLevel;
                        int parts = c.spoolLineParts + lvl / 5;
                        Line(list, $"<b>{SpecDefaults.NameOf(id)}</b>  {p.DorMeters(id):0} m", $"+{c.spoolMeters} m for {parts} line parts + {c.spoolCoins} coins" + (p.level < lvl ? $"  <color=#F5C400>(level {lvl})</color>" : ""),
                            "Make", () => Run(EconomyCommands.MakeSpool, id), p.level >= lvl);
                    }
                    break;
                case 3:
                    foreach (var kv in SpecDefaults.Charkhis)
                    {
                        string id = kv.Key;
                        int lvl = kv.Value.unlockLevel;
                        bool owned = p.HasCharkhi(id);
                        Line(list, $"<b>{SpecDefaults.NameOf(id)}</b>  {HangarScreen.CharkhiRole(id)}{(owned ? "  <color=#3BB273>owned</color>" : "")}",
                            $"{c.craftCharkhiWood} wood · {c.craftCharkhiGears} gears · {c.craftCharkhiCoins} coins" + (p.level < lvl ? $"  <color=#F5C400>(level {lvl})</color>" : ""),
                            "Craft", () => Run(EconomyCommands.CraftCharkhi, id), !owned && p.level >= lvl);
                    }
                    break;
                case 4: Tuning(list, p, c); break;
                default:
                    bool any = false;
                    foreach (ItemCount ic in p.cosmetics)
                    {
                        if (ic.count < 2) continue;
                        any = true;
                        string id = ic.id;
                        CosmeticItem item = Catalog.Cosmetic(id);
                        Line(list, $"<b>{item?.name ?? id}</b> ×{ic.count}", $"Salvage one duplicate → {c.salvageParts} parts", "Salvage", () => Run(EconomyCommands.Salvage, id), true);
                    }
                    if (!any) UiKit.Label(list, "No duplicate skins yet. Duplicates from the Season Pass or events can be salvaged into parts.", 26, Theme.TextDim).Size(-1, 80);
                    break;
            }
        }

        private void Tuning(RectTransform list, PlayerProfile p, EconomyConfig c)
        {
            UiKit.Label(list, $"Tunings are sidegrades (one plus, one minus). Slot 1 unlocks at mastery {c.tuningSlot1Mastery}, slot 2 at mastery {c.tuningSlot2Mastery}. Mastery comes from playing.", 24, Theme.TextDim).Size(-1, 80);
            void Item(string id, TuningTarget target)
            {
                MasteryEntry m = p.Mastery(id);
                Image card = Card(list);
                card.Size(-1, 200);
                RectTransform col = UiKit.Column(card.transform, 8, 16);
                col.Stretch();
                UiKit.Label(col, $"<b>{SpecDefaults.NameOf(id)}</b>  mastery {m.level}/10  <size=20>({m.xp}/{c.masteryXpPerLevel} XP)</size>", 28).Size(-1, 40);
                for (int slot = 0; slot < 2; slot++)
                {
                    int s = slot;
                    int need = slot == 0 ? c.tuningSlot1Mastery : c.tuningSlot2Mastery;
                    string current = slot < m.tunings.Count ? m.tunings[slot] : "";
                    RectTransform row = UiKit.Row(col, 8);
                    row.Size(-1, 60);
                    UiKit.Label(row, $"Slot {slot + 1}: {(m.level < need ? $"<color=#F5C400>mastery {need}</color>" : string.IsNullOrEmpty(current) ? "empty" : SpecDefaults.NameOf(current))}", 24).Size(320, 56);
                    if (m.level < need) continue;
                    foreach (var t in SpecDefaults.Tunings)
                    {
                        if (t.Value.target != target) continue;
                        string tid = t.Key;
                        UiKit.Button(row, SpecDefaults.NameOf(tid), () => Run(EconomyCommands.ApplyTuning, id, tid, s.ToString()), tid == current ? Theme.Good : Theme.PanelLight, 20).Size(-1, 56, 1);
                    }
                    if (!string.IsNullOrEmpty(current)) UiKit.Button(row, "Clear", () => Run(EconomyCommands.ClearTuning, id, s.ToString()), Theme.Bad, 20).Size(100, 56);
                }
            }
            foreach (var d in SpecDefaults.Dors) if (p.DorMeters(d.Key) > 0f || p.loadout.dor == d.Key) Item(d.Key, TuningTarget.Dor);
            foreach (string ch in p.charkhis) Item(ch, TuningTarget.Charkhi);
            UiKit.Label(list, "Waxed coat: DorSpeed +5 % / Strength −1 · Double twist: Strength +2 / DorSpeed −5 % · Light thread: agility +3 % / DorCut −2 %\nOiled axle: reel +20 % / capacity −50 m · Wide drum: capacity +100 m / reel −15 % · Soft grip: sweet zone +2 / Kheench −3 %", 22, Theme.TextDim).Size(-1, 90);
        }

        private static RectTransform Line(RectTransform list, string title, string cost, string action, System.Action click, bool enabled)
        {
            Image card = Card(list);
            card.Size(-1, 110);
            RectTransform row = UiKit.Row(card.transform, 16, 16);
            row.Stretch();
            UiKit.Label(row, $"{title}\n<size=22><color=#FFFFFFAA>{cost}</color></size>", 28).Size(-1, 90, 1);
            UiKit.Button(row, action, click, Theme.Accent, 24).Interactable(enabled).Size(170, 76);
            return row;
        }

        private async void Ad(RewardedPlacement placement, string item)
        {
            OpResult r = await Game.Ads.ShowRewardedAsync(placement, AdContext.Workshop, item);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }
    }
}
