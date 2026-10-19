using System;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
using Kitefall.Match;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Home (spec 4.1): PLAY + mode selector, loadout chip, menus, profile, currencies, missions, showroom kite.</summary>
    public sealed class HomeScreen : UIScreen
    {
        public override bool Opaque => false;

        private static readonly GameMode[] Modes = { GameMode.QuickDuel, GameMode.RankedDuel, GameMode.FreeForAll, GameMode.PrivateRoom, GameMode.TeamBattle, GameMode.LastKiteFlying };
        private static GameMode _mode = GameMode.QuickDuel;
        private static ArenaType _arena = KiteCalendar.Current(DateTime.Now)?.Arena ?? ArenaType.MediterraneanHarbour;

        // Layout follows the owner's home-screen design (1920×1080 reference): profile top-left, Settings/Inbox top-centre,
        // currencies top-right, glossy menu column left, Daily Missions right, mode grid + arena picker + PLAY bottom-right,
        // loadout bar bottom-centre, the 3D showroom (character on a rooftop flying the kite) behind everything.

        protected override void Build()
        {
            PlayerProfile p = Game.Profile;
            if (p == null) return;
            if (Enum.TryParse(p.lastMode, out GameMode last) && Array.IndexOf(Modes, last) >= 0 && _mode == GameMode.QuickDuel) _mode = last;

            // Soft shade on the left so the menu reads over a bright sky.
            Image left = UiKit.Image(Root, new Color(0.02f, 0.05f, 0.12f, 0.28f), Sprites.White, "LeftShade");
            left.rectTransform.Band(Vector2.zero, new Vector2(0.22f, 1f), Vector2.zero, Vector2.zero);

            BuildProfileChip(p);
            BuildTopCentre(p);
            BuildCurrencies(p);
            BuildLeftMenu();
            BuildMissions(p);
            BuildModes(p);
            BuildArenaPicker();
            BuildPlayButton();
            BuildLoadoutBar(p);
            BuildFestival();
        }

        // ---- Top ----------------------------------------------------------------

        private void BuildProfileChip(PlayerProfile p)
        {
            Image chip = Glossy.Panel(Root, Glossy.Style.Panel, "Profile");
            chip.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -26), new Vector2(460, 118));
            chip.gameObject.AddComponent<Button>().onClick.AddListener(() => UI.Push(new ProfileScreen()));

            // Big round portrait with a gold ring, overlapping the chip's left edge.
            Image ring = UiKit.Image(chip.transform, Glossy.Gold, Sprites.Circle, "Ring");
            ring.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12, -6), new Vector2(156, 156));
            Image avatar = UiKit.Image(ring.transform, Theme.Hex("#1C4A86"), Sprites.Circle, "Avatar");
            avatar.rectTransform.Stretch(7, 7, 7, 7);
            Sprite portrait = Art.Portrait(p.characterId ?? CharacterCatalog.DefaultCharacter);
            if (portrait != null) { avatar.sprite = portrait; avatar.color = Color.white; avatar.preserveAspect = true; }
            else
            {
                Text initial = UiKit.Label(avatar.transform, string.IsNullOrEmpty(p.displayName) ? "?" : p.displayName.Substring(0, 1).ToUpper(), 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                initial.rectTransform.Stretch();
            }

            Text name = UiKit.Label(chip.transform, $"{p.displayName}", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            name.rectTransform.Band(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(84, 0), new Vector2(-60, -6));
            Image edit = UiKit.Image(chip.transform, new Color(1, 1, 1, 0.8f), Icons.Get("pencil"), "Edit");
            edit.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -18), new Vector2(26, 26));

            RankTier tier = RankRules.TierOf(p.rankPoints, Game.Ranks);
            Sprite badge = Art.Rank(tier);
            Image b = UiKit.Image(chip.transform, badge != null ? Color.white : Glossy.Gold, badge ?? Icons.Get("star"), "Rank");
            b.preserveAspect = true;
            b.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(84, 12), new Vector2(40, 40));
            Text rank = UiKit.Label(chip.transform, $"<color=#FFC93C>{RankRules.TierName(tier)}</color>     <size=22><color=#FFFFFFCC>{p.rankPoints} RP • Lv {p.level}</color></size>", 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            rank.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(134, 8), new Vector2(-16, 0));
        }

        private void BuildTopCentre(PlayerProfile p)
        {
            Glossy.Button(Root, "Settings", Icons.Get("gear"), () => UI.Push(new SettingsScreen()), Glossy.Style.Panel, 28)
                .GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-53, -26), new Vector2(206, 78));
            int unread = p.inbox.FindAll(m => !m.claimed).Count;
            Button inbox = Glossy.Button(Root, "Inbox", Icons.Get("mail"), () => UI.Push(new InboxScreen()), Glossy.Style.Panel, 28);
            inbox.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(165, -26), new Vector2(200, 78));
            if (unread > 0)
            {
                Image dot = UiKit.Image(inbox.transform, Theme.Bad, Sprites.Circle, "Unread");
                dot.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(34, 34));
                UiKit.Label(dot.transform, unread.ToString(), 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            }
        }

        private void BuildCurrencies(PlayerProfile p)
        {
            Image bar = Glossy.Panel(Root, Glossy.Style.Panel, "Currencies");
            bar.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -26), new Vector2(600, 78));
            int parts = 0;
            foreach (ItemCount c in p.parts) parts += c.count;
            Currency(bar.transform, 0, Art.Sprite("currency_coins") ?? Icons.Get("coin"), Glossy.Gold, p.coins.ToString("N0"), () => UI.Push(new ShopScreen()));
            Currency(bar.transform, 1, Art.Sprite("currency_gems") ?? Icons.Get("gem"), Theme.Hex("#4FA8FF"), p.gems.ToString("N0"), () => UI.Push(new ShopScreen()));
            Currency(bar.transform, 2, Art.Sprite("currency_parts") ?? Icons.Get("parts"), Theme.Gem, $"{parts} parts", null);
        }

        private static void Currency(Transform bar, int slot, Sprite icon, Color tint, string value, Action plus)
        {
            RectTransform cell = UiKit.Rect("Cur" + slot, bar);
            cell.Band(new Vector2(slot / 3f, 0), new Vector2((slot + 1) / 3f, 1), new Vector2(10, 8), new Vector2(-6, -8));
            bool art = icon != null && icon.name.StartsWith("currency") || icon != null && icon.texture != null && icon.texture.width > 96;
            Image ic = UiKit.Image(cell, art ? Color.white : tint, icon, "Icon");
            ic.preserveAspect = true;
            ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(52, 52));
            Text t = UiKit.Label(cell, value, 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            t.rectTransform.Stretch(58, 0, plus != null ? 52 : 0, 0);
            if (plus == null) return;
            Button add = Glossy.Button(cell, "+", null, plus, Glossy.Style.Pill, 30);
            add.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(48, 48));
        }

        // ---- Left menu --------------------------------------------------------------

        private void BuildLeftMenu()
        {
            RectTransform col = UiKit.Column(Root, 10, 0);
            col.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -186), new Vector2(356, 9 * 74 + 8 * 10));
            Item(col, "Wardrobe", "shirt", () => UI.Push(new WardrobeScreen()));
            Item(col, "Squad", "people", () => UI.Push(new SquadLobbyScreen()));
            Item(col, "Training", "target", () => UI.Push(new TrainingScreen()));
            Item(col, "Hangar", "hangar", () => UI.Push(new HangarScreen()));
            Item(col, "Workshop", "wrench", () => UI.Push(new WorkshopScreen()));
            Item(col, "Shop", "cart", () => UI.Push(new ShopScreen()));
            Item(col, "Season Pass", "star", () => UI.Push(new SeasonPassScreen()));
            Item(col, "Leaderboard", "trophy", () => UI.Push(new LeaderboardScreen()));
            Item(col, "Friends", "people", () => UI.Push(new FriendsScreen()));
        }

        private static void Item(Transform parent, string text, string icon, Action click) =>
            Glossy.Button(parent, text, Icons.Get(icon), click, Glossy.Style.Menu, 28, chevron: true).Size(-1, 74);

        // ---- Daily missions ---------------------------------------------------------

        private void BuildMissions(PlayerProfile p)
        {
            EconomyService.EnsureMissions(p, Game.Now);
            Image panel = Glossy.Panel(Root, Glossy.Style.Panel, "Missions");
            panel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -132), new Vector2(570, 386));

            Image star = UiKit.Image(panel.transform, Glossy.Gold, Sprites.Circle, "StarBadge");
            star.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(66, 66));
            UiKit.Image(star.transform, Theme.Hex("#7A4A0A"), Icons.Get("star"), "Star").rectTransform.Stretch(12, 12, 12, 12);
            UiKit.Label(panel.transform, "Daily Missions", 34, Glossy.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed()
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -14), new Vector2(330, 62));
            long secs = (EconomyService.Day(Game.Now) + 1) * 86400L - Game.Now;
            UiKit.Image(panel.transform, new Color(1, 1, 1, 0.85f), Icons.Get("clock"), "Clock").rectTransform
                .Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-118, -30), new Vector2(26, 26));
            UiKit.Label(panel.transform, $"{secs / 3600}h {secs % 3600 / 60:00}m", 22, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleLeft)
                .rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -24), new Vector2(98, 40));

            RectTransform col = UiKit.Column(panel.transform, 8, 0);
            col.Band(new Vector2(0, 0), new Vector2(1, 1), new Vector2(26, 98), new Vector2(-22, -86));
            int shown = 0;
            foreach (MissionProgress m in p.missions)
            {
                MissionTemplate t = Catalog.Mission(m.id);
                if (t == null || shown++ >= 3) continue;
                RectTransform row = UiKit.Rect("Mission", col);
                row.Size(-1, 62);
                UiKit.Label(row, t.text, 24, Color.white, TextAnchor.UpperLeft).rectTransform.Band(new Vector2(0, 0.5f), new Vector2(0.72f, 1), Vector2.zero, Vector2.zero);
                Image bar = Glossy.Bar(row, m.progress / (float)Math.Max(1, t.target));
                bar.rectTransform.Band(new Vector2(0, 0.12f), new Vector2(0.6f, 0.36f), Vector2.zero, Vector2.zero);
                UiKit.Label(row, $"{Math.Min(m.progress, t.target)}/{t.target}", 22, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter)
                    .rectTransform.Band(new Vector2(0.6f, 0), new Vector2(0.74f, 0.5f), Vector2.zero, Vector2.zero);
                bool done = m.progress >= t.target && !m.claimed;
                string id = m.id;
                Button reward = Glossy.Button(row, m.claimed ? "Done" : done ? "Claim" : t.coins.ToString(), m.claimed || done ? null : Art.Sprite("currency_coins") ?? Icons.Get("coin"),
                    () => Run(EconomyCommands.ClaimMission, id), done ? Glossy.Style.Green : Glossy.Style.Pill, 24, iconColor: Art.Has("currency_coins") ? Color.white : Glossy.Gold);
                reward.interactable = done;
                reward.GetComponent<RectTransform>().Band(new Vector2(0.76f, 0.02f), new Vector2(1, 0.98f), Vector2.zero, Vector2.zero);
            }

            long day = EconomyService.Day(Game.Now);
            bool chest = p.lastDailyChestDay != day;
            bool canDouble = Game.Ads.CanShowRewarded(RewardedPlacement.DailyChestDouble, AdContext.Home);
            Button open = Glossy.Button(panel.transform, chest ? "Open daily chest" : canDouble ? "Daily chest ×2 (watch ad)" : "Chest opened",
                Art.Sprite("daily_chest") ?? Icons.Get("chest"), () =>
                {
                    if (chest) Run(EconomyCommands.DailyChest);
                    else Ad(RewardedPlacement.DailyChestDouble);
                }, chest || canDouble ? Glossy.Style.Orange : Glossy.Style.Locked, 28, iconColor: Art.Has("daily_chest") ? Color.white : Theme.Hex("#7A4A0A"));
            open.interactable = chest || canDouble;
            open.GetComponent<RectTransform>().Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 84));
        }

        // ---- Modes, arena, PLAY -----------------------------------------------------

        private static string ModeIcon(GameMode m)
        {
            switch (m)
            {
                case GameMode.QuickDuel: return "swords";
                case GameMode.RankedDuel: return "crown";
                case GameMode.PrivateRoom: return "door";
                case GameMode.LastKiteFlying: return "kite";
                default: return "people";
            }
        }

        private void BuildModes(PlayerProfile p)
        {
            RectTransform grid = UiKit.Grid(Root, new Vector2(279, 74), new Vector2(12, 10), 2);
            grid.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 300), new Vector2(570, 3 * 74 + 2 * 10));
            foreach (GameMode m in Modes)
            {
                GameMode mode = m;
                bool locked = m == GameMode.RankedDuel && !RankRules.IsRankedUnlocked(p, Game.Ranks);
                Glossy.Style style = m == _mode ? Glossy.Style.Selected : locked ? Glossy.Style.Locked : Glossy.Style.Menu;
                Button b = Glossy.Button(grid, ModeRules.For(m).displayName, Icons.Get(locked ? "lock" : ModeIcon(m)), () => { _mode = mode; Refresh(); },
                    style, 26, sub: locked ? "(Locked)" : null, iconColor: locked ? new Color(1, 1, 1, 0.7f) : Color.white);
                if (locked) b.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.75f);
            }
        }

        private void BuildArenaPicker()
        {
            Image row = Glossy.Panel(Root, Glossy.Style.Panel, "Arena");
            row.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 196), new Vector2(570, 92));
            Glossy.Button(row.transform, "", Icons.Get("chevron_left"), () => CycleArena(-1), Glossy.Style.Pill, 26)
                .GetComponent<RectTransform>().Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(58, 76));
            Glossy.Button(row.transform, "", Icons.Get("chevron_right"), () => CycleArena(1), Glossy.Style.Pill, 26)
                .GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(58, 76));

            // Thumbnail: the arena card painting, or the arena's own colours until the art exists.
            ArenaLook look = ArenaLook.For(_arena);
            Sprite card = Art.ForArena(_arena);
            Image thumb = UiKit.Image(row.transform, card != null ? Color.white : look.Buildings[0], card ?? Sprites.Rounded, "Thumb");
            if (card == null) thumb.type = Image.Type.Sliced;
            thumb.rectTransform.Band(Vector2.zero, Vector2.one, new Vector2(74, 8), new Vector2(-74, -8));
            if (card == null)
            {
                Image sky = UiKit.Image(thumb.transform, look.Far, Sprites.VerticalFade, "Sky");
                sky.rectTransform.Band(new Vector2(0, 0.35f), Vector2.one, Vector2.zero, Vector2.zero);
            }
            ArenaData data = Game.Db != null ? Game.Db.Arena(_arena) : null;
            Image shade = UiKit.Image(thumb.transform, new Color(0, 0, 0, 0.35f), Sprites.VerticalFade, "Shade");
            shade.rectTransform.Stretch();
            Text t = UiKit.Label(thumb.transform, ArenaLayout.DisplayName(_arena) + (data != null && !data.availableAtLaunch ? " *" : "") +
                                                  $"\n<size=17><color=#FFFFFFCC>{ArenaLayout.Region(_arena)}</color></size>", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            t.rectTransform.Stretch(6, 2, 6, 2);
        }

        private void BuildPlayButton()
        {
            Button play = Glossy.Button(Root, "PLAY", Icons.Get("swords"), Play, Glossy.Style.Orange, 84, iconColor: Theme.Hex("#FFF4C8"));
            play.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 26), new Vector2(570, 156));
            Text label = play.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.Stretch(110, 0, 20, 0);
            Image icon = play.transform.Find("Icon").GetComponent<Image>();
            icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(110, 110));
        }

        // ---- Loadout bar ------------------------------------------------------------

        private void BuildLoadoutBar(PlayerProfile p)
        {
            SavedLoadout l = p.loadout;
            Image bar = Glossy.Panel(Root, Glossy.Style.Panel, "Loadout");
            bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 26), new Vector2(740, 156));
            bar.gameObject.AddComponent<Button>().onClick.AddListener(() => UI.Push(new HangarScreen()));
            KiteOwned k = p.Kite(l.kite);
            string kiteCount = l.kite == "guddi" && (k == null || k.count == 0) ? "Free" : $"×{(k != null ? k.count : 0)}";
            Slot(bar.transform, 0, Art.Sprite("kite_" + l.kite + "_card") ?? Icons.Get("kite"), Theme.Accent, SpecDefaults.NameOf(l.kite), kiteCount);
            Slot(bar.transform, 1, Art.Sprite("dum_" + l.tail) ?? Icons.Get("ribbon"), Theme.Hex("#E5484D"), SpecDefaults.NameOf(l.tail), "-");
            Slot(bar.transform, 2, Art.Sprite("dor_" + l.dor) ?? Icons.Get("thread"), Theme.Hex("#DDE6F0"), SpecDefaults.NameOf(l.dor), $"{p.DorMeters(l.dor):0} m");
            Slot(bar.transform, 3, Art.Sprite("charkhi_" + l.charkhi) ?? Icons.Get("spool"), Theme.Hex("#C08A4E"), SpecDefaults.NameOf(l.charkhi), "-");
            for (int i = 1; i < 4; i++)
            {
                Image div = UiKit.Image(bar.transform, new Color(0.36f, 0.6f, 0.95f, 0.35f), Sprites.White, "Divider");
                div.rectTransform.Band(new Vector2(i / 4f, 0.12f), new Vector2(i / 4f, 0.88f), new Vector2(-1, 0), new Vector2(1, 0));
            }
        }

        private static void Slot(Transform bar, int i, Sprite icon, Color tint, string name, string sub)
        {
            RectTransform cell = UiKit.Rect("Slot" + i, bar);
            cell.Band(new Vector2(i / 4f, 0), new Vector2((i + 1) / 4f, 1), new Vector2(6, 10), new Vector2(-6, -10));
            bool art = icon != null && icon.texture != null && icon.texture.width > 96;
            Image ic = UiKit.Image(cell, art ? Color.white : tint, icon, "Icon");
            ic.preserveAspect = true;
            ic.rectTransform.Band(new Vector2(0.18f, 0.42f), new Vector2(0.82f, 1f), Vector2.zero, Vector2.zero);
            UiKit.Label(cell, $"{name}\n<color=#FFC93C>{sub}</color>", 21, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                .rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0.42f), Vector2.zero, Vector2.zero);
        }

        /// <summary>World Kite Calendar (D57): a small ribbon under Settings / Inbox.</summary>
        private void BuildFestival()
        {
            KiteFestival now = KiteCalendar.Current(DateTime.Now);
            KiteFestival next = KiteCalendar.Next(DateTime.Now);
            string text = now != null
                ? $"<color=#FFC93C>★ {now.Name}</color>  <size=20>{now.Region}</size>"
                : $"<color=#FFC93C>World Kite Season</color>  <size=20>next: {next.Name} · {next.Region}</size>";
            Sprite art = now != null ? Art.Sprite("festival_" + now.Id) : null;
            Image band = Glossy.Panel(Root, Glossy.Style.Pill, "Festival", false);
            band.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(56, -116), new Vector2(520, art != null ? 150 : 48));
            if (art != null)
            {
                Image pic = UiKit.Image(band.transform, Color.white, art, "FestivalArt");
                pic.rectTransform.Stretch(4, 4, 4, 4);
                pic.preserveAspect = true;
            }
            UiKit.Label(band.transform, text, 22, Color.white, art != null ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter).Shadowed().rectTransform.Stretch(10, 0, 10, 4);
        }

        private static void CycleArena(int dir)
        {
            var values = (ArenaType[])Enum.GetValues(typeof(ArenaType));
            int i = Array.IndexOf(values, _arena);
            _arena = values[(i + dir + values.Length) % values.Length];
            Game.UI.RefreshCurrent();
        }

        private void Play()
        {
            if (_mode == GameMode.PrivateRoom) { UI.Push(new PrivateRoomScreen(_arena)); return; }
            Game.Flow.StartMatch(new MatchRequest { Mode = _mode, Arena = _arena });
        }

        private async void Ad(RewardedPlacement placement)
        {
            OpResult r = await Game.Ads.ShowRewardedAsync(placement, AdContext.Home);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }

        private async void Run(string op, params string[] args)
        {
            OpResult r = await Game.Backend.RunAsync(op, args);
            UI.ToastResult(r);
            if (IsAlive) Refresh();
        }

        public override bool OnBack()
        {
            UI.Confirm("Quit Kitefall?", "See you in the sky!", "Quit", Application.Quit);
            return false;
        }

        public override void OnShow() => Game.Flow?.ShowShowroom();
    }
}
