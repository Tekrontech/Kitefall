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
        private static bool _modeRestored;
        // Scope freeze (R7): a festival arena outside the launch set falls back to a launch arena.
        private static ArenaType _arena = LaunchContent.ArenaOrDefault(KiteCalendar.Current(DateTime.Now)?.Arena ?? LaunchContent.Arenas[0]);

        // Layout follows the owner's home-screen design (1920×1080 reference): profile top-left, Settings/Inbox top-centre,
        // currencies top-right, glossy menu column left, Daily Missions right, mode grid + arena picker + PLAY bottom-right,
        // loadout bar bottom-centre, the 3D showroom (character on a rooftop flying the kite) behind everything.

        protected override void Build()
        {
            PlayerProfile p = Game.Profile;
            if (p == null) return;
            // Restore the last played mode once per session. (Before: it ran on every rebuild, so tapping Quick Duel snapped back
            // to the last mode and Quick Duel could never be picked.)
            if (!_modeRestored)
            {
                _modeRestored = true;
                if (Enum.TryParse(p.lastMode, out GameMode last) && Array.IndexOf(Modes, last) >= 0) _mode = last;
            }

            bool intro = !_built;
            _built = true;
            _playingIntro = intro;
            _intro.Clear();

            // Painted home picture from the owner's design, brought to life by HomeAnimator (Settings ▸ Home scene).
            Sprite painted = Painted ? Art.Sprite("home_background") : null;
            if (painted != null)
            {
                RectTransform picture = MenuScreen.ArtBackground(Root, painted).rectTransform;
                HomeAnimator anim = HomeAnimator.Attach(picture, Art.Sprite("home_kite"));
                PlaceOwnCharacter(picture, anim, p);
            }

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
            BuildDevPipelineWarning();
            if (intro)
                foreach (var (c, offset, delay) in _intro) UiMotion.SlideIn(c, offset, delay);
        }

        /// <summary>Developer-only red banner when URP is not the active render pipeline (Editor / development builds only).</summary>
        private void BuildDevPipelineWarning()
        {
            if (!Debug.isDebugBuild) return;
            string problem = KitefallGraphics.PipelineProblem;
            if (problem == null) return;
            Image band = UiKit.Image(Root, new Color(0.6f, 0.05f, 0.05f, 0.92f), Sprites.White, "DevPipelineWarning");
            band.rectTransform.Band(new Vector2(0.23f, 0.64f), new Vector2(0.77f, 0.84f), Vector2.zero, Vector2.zero);
            UiKit.Label(band.transform, "DEV: Render pipeline is not URP – see the Console error.\n<size=17>" + problem + "</size>", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                .rectTransform.Stretch(12, 4, 12, 4);
        }

        private bool _built, _playingIntro;
        private readonly System.Collections.Generic.List<(Component, Vector2, float)> _intro = new System.Collections.Generic.List<(Component, Vector2, float)>();
        private void Intro(Component c, Vector2 from, float delay) => _intro.Add((c, from, delay));

        // Where the painted flyer stood (1600×900 design): the player's own character is shown there instead.
        private const float WallV = 1f - 672f / 900f;                  // top of the parapet – the character is clipped below it
        private static readonly Rect CharacterBox = new Rect(0.3203f - 0.078f, 0.3128f - 0.174f, 0.156f, 0.348f); // u, v, w, h

        /// <summary>The selected character (wardrobe look) on the rooftop of the painted picture, with the kite line from their hand.</summary>
        private static void PlaceOwnCharacter(RectTransform picture, HomeAnimator anim, PlayerProfile p)
        {
            HomeCharacterStage stage = HomeCharacterStage.Get();
            Texture tex = stage.Show(p);
            RectTransform clip = UiKit.Rect("CharacterClip", picture);
            clip.anchorMin = new Vector2(0f, WallV);
            clip.anchorMax = Vector2.one;
            clip.offsetMin = clip.offsetMax = Vector2.zero;
            clip.gameObject.AddComponent<RectMask2D>();
            var go = new GameObject("OwnCharacter", typeof(RectTransform));
            go.transform.SetParent(clip, false);
            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            RectTransform rt = raw.rectTransform;
            float span = 1f - WallV;
            rt.anchorMin = new Vector2(CharacterBox.xMin, (CharacterBox.yMin - WallV) / span);
            rt.anchorMax = new Vector2(CharacterBox.xMax, (CharacterBox.yMax - WallV) / span);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            stage.Bind(raw);
            anim.HandUV = () => new Vector2(CharacterBox.xMin + stage.HandViewport.x * CharacterBox.width,
                                            CharacterBox.yMin + stage.HandViewport.y * CharacterBox.height);
        }

        /// <summary>Painted design picture as the home background (default) instead of the live 3D scene.</summary>
        private static bool Painted => (Game.Settings == null || Game.Settings.Data.homeTheme == 2) && Art.Has("home_background");

        // ---- Top ----------------------------------------------------------------

        private void BuildProfileChip(PlayerProfile p)
        {
            Image chip = Glossy.Panel(Root, Glossy.Style.Panel, "Profile");
            chip.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -26), new Vector2(460, 118));
            chip.gameObject.AddComponent<Button>().onClick.AddListener(() => UI.Push(new ProfileScreen()));
            UiMotion.Bounce(chip);
            Intro(chip, new Vector2(-40, 160), 0f);

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
            Button settings = Glossy.Button(Root, "Settings", Icons.Get("gear"), () => UI.Push(new SettingsScreen()), Glossy.Style.Panel, 28);
            Intro(settings, new Vector2(0, 140), 0.05f);
            settings.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-53, -26), new Vector2(206, 78));
            int unread = p.inbox.FindAll(m => !m.claimed).Count;
            Button inbox = Glossy.Button(Root, "Inbox", Icons.Get("mail"), () => UI.Push(new InboxScreen()), Glossy.Style.Panel, 28);
            inbox.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(165, -26), new Vector2(200, 78));
            Intro(inbox, new Vector2(0, 140), 0.1f);
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
            Intro(bar, new Vector2(0, 140), 0.08f);
            int parts = 0;
            foreach (ItemCount c in p.parts) parts += c.count;
            Text coins = Currency(bar.transform, 0, Art.Sprite("currency_coins") ?? Icons.Get("coin"), Glossy.Gold, p.coins.ToString("N0"), () => UI.Push(new ShopScreen()));
            Text gems = Currency(bar.transform, 1, Art.Sprite("currency_gems") ?? Icons.Get("gem"), Theme.Hex("#4FA8FF"), p.gems.ToString("N0"), () => UI.Push(new ShopScreen()));
            if (_playingIntro)
            {
                coins.gameObject.AddComponent<CountUp>().Value = p.coins;
                gems.gameObject.AddComponent<CountUp>().Value = p.gems;
            }
            Currency(bar.transform, 2, Art.Sprite("currency_parts") ?? Icons.Get("parts"), Theme.Gem, $"{parts} parts", null);
        }

        private static Text Currency(Transform bar, int slot, Sprite icon, Color tint, string value, Action plus)
        {
            RectTransform cell = UiKit.Rect("Cur" + slot, bar);
            cell.Band(new Vector2(slot / 3f, 0), new Vector2((slot + 1) / 3f, 1), new Vector2(10, 8), new Vector2(-6, -8));
            bool art = icon != null && icon.name.StartsWith("currency") || icon != null && icon.texture != null && icon.texture.width > 96;
            Image ic = UiKit.Image(cell, art ? Color.white : tint, icon, "Icon");
            ic.preserveAspect = true;
            ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(52, 52));
            Text t = UiKit.UseButtonFont(UiKit.Label(cell, value, 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed(), false);
            t.rectTransform.Stretch(58, 0, plus != null ? 52 : 0, 0);
            if (plus == null) return t;
            Button add = Glossy.Button(cell, "+", null, plus, Glossy.Style.Orange, 28);
            add.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(42, 42));
            return t;
        }

        // ---- Left menu --------------------------------------------------------------

        private void BuildLeftMenu()
        {
            RectTransform col = UiKit.Column(Root, 10, 0);
            col.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -186), new Vector2(356, 9 * 74 + 8 * 10));
            _menuIndex = 0;
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

        private int _menuIndex;

        private void Item(Transform parent, string text, string icon, Action click)
        {
            // The column layout owns the slot; the button slides inside it (staggered, one after another).
            RectTransform slot = UiKit.Rect("Slot_" + text, parent);
            slot.Size(-1, 74);
            Button b = Glossy.Button(slot, text, Icons.Get(icon), click, Glossy.Style.Menu, 28, chevron: true);
            b.GetComponent<RectTransform>().Stretch();
            Intro(b, new Vector2(-420, 0), 0.12f + _menuIndex++ * 0.045f);
        }

        // ---- Daily missions ---------------------------------------------------------

        private void BuildMissions(PlayerProfile p)
        {
            EconomyService.EnsureMissions(p, Game.Now);
            Image panel = Glossy.Panel(Root, Glossy.Style.Panel, "Missions");
            panel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -132), new Vector2(570, 386));
            Intro(panel, new Vector2(620, 0), 0.15f);

            Image star = UiKit.Image(panel.transform, Glossy.Gold, Sprites.Circle, "StarBadge");
            star.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(66, 66));
            UiKit.Image(star.transform, Theme.Hex("#7A4A0A"), Icons.Get("star"), "Star").rectTransform.Stretch(12, 12, 12, 12);
            UiKit.Display(UiKit.Label(panel.transform, "Daily Missions", 36, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold)).Shadowed()
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
                // Round mission badge (mission_play3 …) on the left when the art is installed; text-only row otherwise.
                Sprite badge = Art.ForMission(t.id);
                float ix = badge != null ? 64f : 0f;
                if (badge != null)
                {
                    Image ic = UiKit.Image(row, Color.white, badge, "Badge");
                    ic.preserveAspect = true;
                    ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(56, 56));
                }
                UiKit.Label(row, t.text, 24, Color.white, TextAnchor.UpperLeft).rectTransform.Band(new Vector2(0, 0.5f), new Vector2(0.72f, 1), new Vector2(ix, 0), Vector2.zero);
                float value = m.progress / (float)Math.Max(1, t.target);
                Image bar = Glossy.Bar(row, value);
                Transform fill = bar.transform.Find("Fill");
                if (_playingIntro && fill != null && fill.gameObject.activeSelf) { var bf = fill.gameObject.AddComponent<BarFill>(); bf.Target = Mathf.Clamp01(value); bf.Delay = 0.5f + shown * 0.12f; }
                bar.rectTransform.Band(new Vector2(0, 0.12f), new Vector2(0.6f, 0.36f), new Vector2(ix, 0), Vector2.zero);
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
            if (chest) open.gameObject.AddComponent<Breathe>().Amount = 0.02f;
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
            Intro(grid, new Vector2(620, 0), 0.22f);
            foreach (GameMode m in Modes)
            {
                GameMode mode = m;
                bool locked = m == GameMode.RankedDuel && !RankRules.IsRankedUnlocked(p, Game.Ranks);
                Glossy.Style style = m == _mode ? Glossy.Style.Selected : locked ? Glossy.Style.Locked : Glossy.Style.Menu;
                Button b = Glossy.Button(grid, ModeRules.For(m).displayName, Icons.Get(locked ? "lock" : ModeIcon(m)), () => { _mode = mode; Refresh(); },
                    style, 26, sub: locked ? "(Locked)" : null, iconColor: locked ? new Color(1, 1, 1, 0.7f) : Color.white);
                if (locked) b.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.75f);
                // Mode card painting behind the icon and name (Image Mapper); the plain glossy button until it exists.
                Sprite card = Art.ForMode(m);
                if (card != null) MenuScreen.ArtFill(b.transform, card, m == _mode ? 5f : 3f, locked ? 0.6f : m == _mode ? 0.15f : 0.35f, Glossy.Sprite(style));
                if (m == _mode) Shine.Add((RectTransform)b.transform);
            }
        }

        private void BuildArenaPicker()
        {
            Image row = Glossy.Panel(Root, Glossy.Style.Panel, "Arena");
            row.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 196), new Vector2(570, 92));
            Intro(row, new Vector2(620, 0), 0.28f);
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
            string soon = LaunchContent.IsLaunchArena(_arena) ? "" : $"  <size=18><color=#FFC93C>({LaunchContent.ComingSoonLabel})</color></size>";
            Text t = UiKit.Label(thumb.transform, ArenaLayout.DisplayName(_arena) + (data != null && !data.availableAtLaunch ? " *" : "") + soon +
                                                  $"\n<size=17><color=#FFFFFFCC>{ArenaLayout.Region(_arena)}</color></size>", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            t.rectTransform.Stretch(6, 2, 6, 2);
        }

        private void BuildPlayButton()
        {
            Button play = Glossy.Button(Root, "PLAY", Icons.Get("swords"), Play, Glossy.Style.Play, 84, iconColor: Theme.TextOnAccent);
            play.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 26), new Vector2(570, 156));
            play.gameObject.AddComponent<Breathe>();
            Shine.Add((RectTransform)play.transform);
            Intro(play, new Vector2(0, -260), 0.34f);
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
            Intro(bar, new Vector2(0, -240), 0.3f);
            UiMotion.Bounce(bar);
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
            var values = LaunchContent.PickerArenas(); // launch arenas (+ locked "Coming soon" ones when enabled)
            int i = values.IndexOf(_arena);
            _arena = values[(i + dir + values.Count) % values.Count];
            Game.UI.RefreshCurrent();
        }

        private void Play()
        {
            if (!LaunchContent.IsLaunchArena(_arena) && _mode != GameMode.LastKiteFlying) { UI.Toast(LaunchContent.ComingSoonLabel, Theme.Warn); return; }
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

        public override void OnShow()
        {
            if (!Painted) Game.Flow?.ShowShowroom(); // the painted picture covers the screen – no 3D scene needed behind it
        }
    }
}
