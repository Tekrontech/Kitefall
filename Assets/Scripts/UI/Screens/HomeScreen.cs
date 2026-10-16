using System;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
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
        private static ArenaType _arena = ArenaType.OldCityRooftops;

        protected override void Build()
        {
            PlayerProfile p = Game.Profile;
            if (p == null) return;
            if (Enum.TryParse(p.lastMode, out GameMode last) && Array.IndexOf(Modes, last) >= 0 && _mode == GameMode.QuickDuel) _mode = last;

            // Soft vignette so text stays readable over the 3D showroom.
            Image left = UiKit.Image(Root, new Color(0.03f, 0.05f, 0.1f, 0.55f), Sprites.White, "LeftShade");
            left.rectTransform.Band(Vector2.zero, new Vector2(0.24f, 1f), Vector2.zero, Vector2.zero);

            BuildProfileChip(p);
            BuildTopRight(p);
            BuildLeftMenu();
            BuildMissions(p);
            BuildLoadoutChip(p);
            BuildPlay(p);
        }

        private void BuildProfileChip(PlayerProfile p)
        {
            Image chip = UiKit.Panel(Root, new Color(0, 0, 0, 0.5f), "Profile");
            chip.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(520, 110));
            var b = chip.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => UI.Push(new ProfileScreen()));
            Image avatar = UiKit.Image(chip.transform, Theme.Accent, Sprites.Circle, "Avatar");
            avatar.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(84, 84));
            Text initial = UiKit.Label(avatar.transform, string.IsNullOrEmpty(p.displayName) ? "?" : p.displayName.Substring(0, 1).ToUpper(), 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            initial.rectTransform.Stretch();
            RankTier tier = RankRules.TierOf(p.rankPoints, Game.Ranks);
            Text t = UiKit.Label(chip.transform, $"<b>{p.displayName}</b>  <size=22>{p.country}</size>\n<size=24><color=#FFD24D>{RankRules.TierName(tier)}</color> · {p.rankPoints} RP · Lv {p.level}</size>", 32, Color.white);
            t.rectTransform.Stretch(112, 8, 12, 8);
        }

        private void BuildTopRight(PlayerProfile p)
        {
            RectTransform top = UiKit.Rect("Top", Root);
            top.Band(new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(0, -116), new Vector2(-24, -24));
            MenuScreen.Currencies(top);
            int unread = p.inbox.FindAll(m => !m.claimed).Count;
            UiKit.Button(top, unread > 0 ? $"Inbox ({unread})" : "Inbox", () => UI.Push(new InboxScreen()), unread > 0 ? Theme.Accent : Theme.PanelLight, 24)
                .GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-640, 0), new Vector2(170, 76));
            UiKit.Button(top, "Settings", () => UI.Push(new SettingsScreen()), Theme.PanelLight, 24)
                .GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-826, 0), new Vector2(170, 76));
        }

        private void BuildLeftMenu()
        {
            RectTransform col = UiKit.Column(Root, 14, 0);
            col.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, -30), new Vector2(380, 640));
            Item(col, "Hangar", () => UI.Push(new HangarScreen()));
            Item(col, "Workshop", () => UI.Push(new WorkshopScreen()));
            Item(col, "Shop", () => UI.Push(new ShopScreen()));
            Item(col, "Season Pass", () => UI.Push(new SeasonPassScreen()));
            Item(col, "Leaderboard", () => UI.Push(new LeaderboardScreen()));
            Item(col, "Friends", () => UI.Push(new FriendsScreen()));
        }

        private static void Item(Transform parent, string text, Action click) => UiKit.Button(parent, text, click, new Color(0.12f, 0.17f, 0.28f, 0.92f), 32).Size(-1, 92);

        private void BuildMissions(PlayerProfile p)
        {
            EconomyService.EnsureMissions(p, Game.Now);
            Image panel = UiKit.Panel(Root, new Color(0, 0, 0, 0.5f), "Missions");
            panel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -150), new Vector2(500, 470));
            RectTransform col = UiKit.Column(panel.transform, 10, 18);
            col.Stretch();
            UiKit.Label(col, "Daily missions", 30, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 40);
            foreach (MissionProgress m in p.missions)
            {
                MissionTemplate t = Catalog.Mission(m.id);
                if (t == null) continue;
                RectTransform row = UiKit.Row(col, 8);
                row.Size(-1, 74);
                RectTransform info = UiKit.Column(row, 4);
                info.Size(-1, 70, 1);
                UiKit.Label(info, $"{t.text}  <size=20>{m.progress}/{t.target}</size>", 24).Size(-1, 34);
                UiKit.Bar(info, m.progress / (float)t.target, Theme.Accent2, 10f);
                bool done = m.progress >= t.target && !m.claimed;
                string id = m.id;
                UiKit.Button(row, m.claimed ? "Done" : done ? "Claim" : $"{t.coins}", () => Run(EconomyCommands.ClaimMission, id), done ? Theme.Good : Theme.PanelLight, 22)
                    .Interactable(done).Size(110, 64);
            }
            long day = EconomyService.Day(Game.Now);
            bool chest = p.lastDailyChestDay != day;
            UiKit.Button(col, chest ? "Open daily chest" : (Game.Ads.CanShowRewarded(RewardedPlacement.DailyChestDouble, AdContext.Home) ? "Daily chest ×2 (watch ad)" : "Chest opened"), () =>
            {
                if (chest) Run(EconomyCommands.DailyChest);
                else Ad(RewardedPlacement.DailyChestDouble);
            }, chest ? Theme.Accent : Theme.PanelLight, 24).Size(-1, 70);
        }

        private void BuildLoadoutChip(PlayerProfile p)
        {
            SavedLoadout l = p.loadout;
            Image chip = UiKit.Panel(Root, new Color(0, 0, 0, 0.55f), "Loadout");
            chip.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-90, 24), new Vector2(900, 110));
            var b = chip.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => UI.Push(new HangarScreen()));
            KiteOwned k = p.Kite(l.kite);
            string count = l.kite == "guddi" && (k == null || k.count == 0) ? "free" : $"×{(k != null ? k.count : 0)}";
            Text t = UiKit.Label(chip.transform,
                $"<b>{SpecDefaults.NameOf(l.kite)}</b> {count} · {SpecDefaults.NameOf(l.tail)} · <b>{SpecDefaults.NameOf(l.dor)}</b> {p.DorMeters(l.dor):0} m · {SpecDefaults.NameOf(l.charkhi)}\n" +
                $"<size=22><color=#FFFFFFAA>Durability {(k != null ? k.durability : 100):0}%   ·   Tap to open Hangar</color></size>", 28, Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(16, 6, 16, 6);
        }

        private void BuildPlay(PlayerProfile p)
        {
            RectTransform modes = UiKit.Column(Root, 8, 0);
            modes.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 190), new Vector2(500, 330));
            RectTransform grid = UiKit.Grid(modes, new Vector2(244, 64), new Vector2(10, 8), 2);
            foreach (GameMode m in Modes)
            {
                GameMode mode = m;
                bool update = m == GameMode.TeamBattle || m == GameMode.LastKiteFlying;
                bool locked = m == GameMode.RankedDuel && !RankRules.IsRankedUnlocked(p, Game.Ranks);
                string label = ModeRules.For(m).displayName + (locked ? " (locked)" : update ? " *" : "");
                UiKit.Button(grid, label, () => { _mode = mode; Refresh(); }, m == _mode ? Theme.Accent : new Color(0.12f, 0.17f, 0.28f, 0.92f), 22);
            }
            RectTransform arenaRow = UiKit.Row(modes, 8);
            arenaRow.Size(-1, 60);
            UiKit.Button(arenaRow, "‹", () => CycleArena(-1), Theme.PanelLight, 30).Size(70, 56);
            ArenaData data = Game.Db.Arena(_arena);
            UiKit.Label(arenaRow, ArenaLayout.DisplayName(_arena) + (data != null && !data.availableAtLaunch ? " *" : ""), 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Size(-1, 56, 1);
            UiKit.Button(arenaRow, "›", () => CycleArena(1), Theme.PanelLight, 30).Size(70, 56);

            Button play = UiKit.Button(Root, "PLAY", Play, Theme.Accent, 64);
            play.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(500, 150));
            Text sub = UiKit.Label(play.transform, ModeRules.For(_mode).displayName, 22, Color.white, TextAnchor.LowerCenter);
            sub.rectTransform.Stretch(0, 0, 0, 8);
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
