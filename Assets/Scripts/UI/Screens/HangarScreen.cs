using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Hangar (spec 4.2): kite · dum · dor · charkhi · stack, stats, counts, durability, meters.</summary>
    public sealed class HangarScreen : MenuScreen
    {
        public override string Title => "Hangar";
        protected override float BackgroundAlpha => 0.35f;
        private static int _tab;

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            RectTransform tabsHost = UiKit.Rect("Tabs", content);
            tabsHost.Band(new Vector2(0, 1), new Vector2(0.62f, 1), new Vector2(0, -76), Vector2.zero);
            UiKit.Tabs(tabsHost, new[] { "Kite", Terms.Tail, Terms.Line, Terms.Spool, "Stack" }, _tab, i => { _tab = i; Refresh(); }).ForEach(b => b.Size(-1, 70, 1));

            RectTransform listHost = UiKit.Rect("List", content);
            listHost.Band(new Vector2(0, 0), new Vector2(0.62f, 1), new Vector2(0, 0), new Vector2(0, -90));
            UiKit.Scroll(listHost, out RectTransform list).GetComponent<RectTransform>().Stretch();

            RectTransform detail = UiKit.Rect("Detail", content);
            detail.Band(new Vector2(0.64f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            Image dbg = UiKit.Panel(detail, new Color(0, 0, 0, 0.55f));
            dbg.rectTransform.Stretch();
            RectTransform col = UiKit.Column(dbg.transform, 8, 24);
            col.Stretch();
            Details(col, p);

            switch (_tab)
            {
                case 0: Kites(list, p); break;
                case 1: Tails(list, p); break;
                case 2: Dors(list, p); break;
                case 3: Charkhis(list, p); break;
                default: Stack(list, p); break;
            }
        }

        private void Details(RectTransform col, PlayerProfile p)
        {
            SavedLoadout l = p.loadout;
            LoadoutStats s = EconomyService.BuildMatchLoadout(p, l, l.kite).Compose();
            UiKit.Label(col, "Your loadout", 34, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 50);
            UiKit.Label(col, $"{SpecDefaults.NameOf(l.kite)} + {SpecDefaults.NameOf(l.tail)}\n{SpecDefaults.NameOf(l.dor)} + {SpecDefaults.NameOf(l.charkhi)}", 28).Size(-1, 84);
            KiteClassStats k = SpecDefaults.Kite(l.kite);
            UiKit.StatBar(col, "Speed", k.speed, 5, Theme.Accent);
            UiKit.StatBar(col, "Agility", k.agility, 5, Theme.Accent2);
            UiKit.StatBar(col, "Stability", s.Stability, 8, Theme.Good);
            UiKit.Label(col,
                $"Turn rate {s.TurnRate:0}°/s · Sweet zone {s.SweetZone}\n" +
                $"Strength {s.Strength} · Class ×{s.ClassBase:0.00}\n" +
                $"{Terms.Line} cut ×{s.DorCut:0.00} · {Terms.Line} speed ×{s.DorSpeed:0.00}\n" +
                $"Capacity {s.CapacityMeters} m · Reel {s.ReelSpeed:0.#} · Release {s.ReleaseSpeed:0.#} m/s", 24, Theme.TextDim).Size(-1, 150);
            string err = EconomyService.ValidateLoadout(p, l, Game.Economy);
            UiKit.Label(col, err == null ? "<color=#3BB273>Ready to fly</color>" : $"<color=#E5484D>{err}</color>", 24).Size(-1, 70);
            UiKit.Label(col, "Recommended for Old City: " + Recommend(), 22, Theme.TextDim).Size(-1, 60);
        }

        private static string Recommend() => "Guddi + Short ribbon, Sooti + Lakri (shifting wind). Coastal: Rokkaku + Mazboot (strong steady wind).";

        private void Kites(RectTransform list, PlayerProfile p)
        {
            // Scope freeze (R7): launch kites, plus hidden classes this profile already has (they keep working).
            foreach (SpecDefaults.KiteInfo info in LaunchContent.PickerKites(k => (p.Kite(k)?.count ?? 0) > 0 || p.loadout.kite == k))
            {
                string id = info.Stats.id;
                KiteOwned owned = p.Kite(id);
                int count = owned != null ? owned.count : 0;
                bool free = id == "guddi";
                int unlock = Catalog.KiteUnlockLevel(id);
                bool soon = !LaunchContent.IsLaunchKite(id) && count <= 0 && p.loadout.kite != id;
                bool locked = p.level < unlock || soon;
                bool usable = !locked && (count > 0 || free);
                Image card = Card(list, p.loadout.kite == id);
                card.Size(-1, 150);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                Text t = UiKit.Label(row, $"<b>{info.Name}</b>  <size=22>{info.InspiredBy} · {info.Style}</size>\n" +
                                          $"<size=24>Speed {info.Stats.speed} · Agility {info.Stats.agility} · Stability {info.Stats.stability} · Turn {info.Stats.turnRate:0}°/s · Sweet {info.Stats.sweetZoneBase}</size>\n" +
                                          $"<size=24>{(soon ? $"<color=#F5C400>{LaunchContent.ComingSoonLabel}</color>" : locked ? $"<color=#F5C400>Unlocks at level {unlock}</color>" : free && count == 0 ? "Free basic kite (unlimited)" : $"×{count} · durability {(owned != null ? owned.durability : 100):0}%{(owned != null && owned.durability < 30 ? " <color=#E5484D>(−10 % power)</color>" : "")}")}</size>", 28);
                t.Size(-1, 120, 1);
                if (usable)
                    UiKit.Button(row, p.loadout.kite == id ? "Selected" : "Select", () => SetLoadout(p, kite: id), p.loadout.kite == id ? Theme.Good : Theme.Accent, 24).Size(170, 80);
                else if (!locked && count == 0 && Game.Ads.CanShowRewarded(RewardedPlacement.FreeKite, AdContext.Hangar))
                    UiKit.Button(row, "Free kite (ad)", () => Ad(RewardedPlacement.FreeKite, id), Theme.Accent2, 22).Size(200, 80);
                else
                    UiKit.Button(row, "Workshop", () => UI.Push(new WorkshopScreen()), Theme.PanelLight, 22).Interactable(!locked).Size(170, 80);
            }
        }

        private void Tails(RectTransform list, PlayerProfile p)
        {
            foreach (var kv in SpecDefaults.Tails)
            {
                string id = kv.Key;
                TailStats t = kv.Value;
                Image card = Card(list, p.loadout.tail == id);
                card.Size(-1, 110);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                UiKit.Label(row, $"<b>{SpecDefaults.NameOf(id)}</b>\n<size=24>Sweet zone {t.sweetZoneMod:+0;-0;0} · Turn speed {t.turnMod * 100:+0;-0;0}% · Stability {t.stabilityMod:+0;-0;0}</size>", 28).Size(-1, 90, 1);
                UiKit.Button(row, p.loadout.tail == id ? "Selected" : "Select", () => SetLoadout(p, tail: id), p.loadout.tail == id ? Theme.Good : Theme.Accent, 24).Size(170, 76);
            }
        }

        private void Dors(RectTransform list, PlayerProfile p)
        {
            float sooti = p.DorMeters("sooti");
            long day = EconomyService.Day(Game.Now);
            RectTransform refill = UiKit.Row(list, 12);
            refill.Size(-1, 84);
            UiKit.Button(refill, p.lastSootiRefillDay == day ? "Free Sooti refill used today" : "Free daily Sooti +300 m", () => Run(EconomyCommands.SootiRefill), p.lastSootiRefillDay == day ? Theme.Disabled : Theme.Good, 24)
                .Interactable(p.lastSootiRefillDay != day).Size(-1, 76, 1);
            if (sooti < Game.Economy.minDorForMatch && Game.Ads.CanShowRewarded(RewardedPlacement.DorRefill, AdContext.Hangar))
                UiKit.Button(refill, "+200 m Sooti (ad)", () => Ad(RewardedPlacement.DorRefill, "sooti"), Theme.Accent2, 24).Size(-1, 76, 1);

            foreach (var kv in SpecDefaults.Dors)
            {
                string id = kv.Key;
                DorStats d = kv.Value;
                float m = p.DorMeters(id);
                bool locked = p.level < d.unlockLevel;
                bool basic = id == EconomyService.BasicDor; // Sooti never blocks play (audit B1)
                bool enough = basic || m >= Game.Economy.minDorForMatch;
                Image card = Card(list, p.loadout.dor == id);
                card.Size(-1, 150);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                MasteryEntry mastery = p.Mastery(id);
                UiKit.Label(row, $"<b>{SpecDefaults.NameOf(id)}</b>  <size=22>{Role(id)}</size>   <color=#9FD8FF>{m:0} m</color>\n" +
                                  $"<size=24>Cut ×{d.dorCut:0.00} · Speed ×{d.dorSpeed:0.00} · Strength {d.strength}{Special(id)}</size>\n" +
                                  $"<size=22>{(locked ? $"<color=#F5C400>Unlocks at level {d.unlockLevel}</color>" : !enough ? "<color=#E5484D>Needs 100 m to fly</color>" : basic ? $"Unlimited (used only in Ranked / Last Kite Flying) · mastery {mastery.level}" : $"Mastery {mastery.level}")}</size>", 28).Size(-1, 120, 1);
                UiKit.Button(row, p.loadout.dor == id ? "Selected" : "Select", () => SetLoadout(p, dor: id), p.loadout.dor == id ? Theme.Good : Theme.Accent, 24)
                    .Interactable(!locked && enough).Size(170, 80);
            }
        }

        private void Charkhis(RectTransform list, PlayerProfile p)
        {
            foreach (var kv in SpecDefaults.Charkhis)
            {
                string id = kv.Key;
                CharkhiStats c = kv.Value;
                bool owned = p.HasCharkhi(id);
                Image card = Card(list, p.loadout.charkhi == id);
                card.Size(-1, 130);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                UiKit.Label(row, $"<b>{SpecDefaults.NameOf(id)}</b>  <size=22>{CharkhiRole(id)}</size>\n" +
                                  $"<size=24>{c.capacityMeters} m · reel {c.reelSpeed} · release {c.releaseSpeed} m/s{CharkhiSpecial(id)}</size>\n" +
                                  $"<size=22>{(owned ? $"Owned · mastery {p.Mastery(id).level}" : $"Craft in the Workshop (level {c.unlockLevel})")}</size>", 28).Size(-1, 110, 1);
                if (owned) UiKit.Button(row, p.loadout.charkhi == id ? "Selected" : "Select", () => SetLoadout(p, charkhi: id), p.loadout.charkhi == id ? Theme.Good : Theme.Accent, 24).Size(170, 80);
                else UiKit.Button(row, "Workshop", () => UI.Push(new WorkshopScreen()), Theme.PanelLight, 22).Size(170, 80);
            }
        }

        private void Stack(RectTransform list, PlayerProfile p)
        {
            UiKit.Label(list, "Free-for-All / Team: the order of kites used when you respawn. Empty stack → free Guddi.", 26, Theme.TextDim).Size(-1, 80);
            for (int slot = 0; slot < 3; slot++)
            {
                int s = slot;
                string current = slot < p.loadout.stack.Count ? p.loadout.stack[slot] : "guddi";
                Image card = Card(list);
                card.Size(-1, 110);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                UiKit.Label(row, $"Slot {slot + 1}:  <b>{SpecDefaults.NameOf(current)}</b>", 30).Size(-1, 80, 1);
                UiKit.Button(row, "Change", () =>
                {
                    var options = SpecDefaults.Kites.FindAll(k => k.Stats.id == "guddi" || (p.Kite(k.Stats.id)?.count ?? 0) > 0);
                    int i = options.FindIndex(k => k.Stats.id == current);
                    string next = options[(i + 1) % options.Count].Stats.id;
                    var stack = new System.Collections.Generic.List<string>(p.loadout.stack);
                    while (stack.Count < 3) stack.Add("guddi");
                    stack[s] = next;
                    SetLoadout(p, stack: stack.ToArray());
                }, Theme.Accent, 24).Size(170, 76);
            }
        }

        private void SetLoadout(PlayerProfile p, string kite = null, string tail = null, string dor = null, string charkhi = null, string[] stack = null)
        {
            SavedLoadout l = p.loadout;
            var args = new System.Collections.Generic.List<string> { kite ?? l.kite, tail ?? l.tail, dor ?? l.dor, charkhi ?? l.charkhi };
            args.AddRange(stack ?? l.stack.ToArray());
            Run(EconomyCommands.SetLoadout, args.ToArray());
        }

        public override void Refresh()
        {
            base.Refresh();
            Game.Flow?.ShowShowroom();
        }

        private async void Ad(RewardedPlacement placement, string item)
        {
            OpResult r = await Game.Ads.ShowRewardedAsync(placement, AdContext.Hangar, item);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }

        public static string Role(string dor)
        {
            switch (dor)
            {
                case "reshmi": return "Fast";
                case "tez_dhaar": return "Power";
                case "mazboot": return "Tank";
                case "lambi": return "Reach";
                case "lachakdaar": return "Defence";
                default: return "Balanced";
            }
        }

        private static string Special(string dor)
        {
            switch (dor)
            {
                case "reshmi": return " · agility +5 %, tension +10 % faster";
                case "tez_dhaar": return " · tension +15 % faster, wears 30 % faster";
                case "mazboot": return " · survives the red longer, agility −8 %";
                case "lambi": return " · angle bonus 1.15 beyond 500 m";
                case "lachakdaar": return " · sweet zone +4, " + Terms.Release + " counter 1.20";
                default: return " · no weakness, free daily refill";
            }
        }

        public static string CharkhiRole(string id)
        {
            switch (id)
            {
                case "speed": return "Quickdraw";
                case "bari": return "Scope";
                case "brake": return "Stabilizer";
                case "gear": return "Compensator";
                default: return "Standard";
            }
        }

        private static string CharkhiSpecial(string id)
        {
            switch (id)
            {
                case "speed": return " · tension falls 20 % faster on release";
                case "bari": return " · long reach, launch 1 s slower";
                case "brake": return " · " + Terms.Release + " cut tension never below 55";
                case "gear": return " · " + Terms.Pull + " ×1.05, pull tension +15 % faster";
                default: return "";
            }
        }
    }
}
