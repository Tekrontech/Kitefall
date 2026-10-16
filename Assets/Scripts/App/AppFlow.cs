using System;
using System.Threading.Tasks;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using Kitefall.UI;
using Kitefall.UI.Hud;
using Kitefall.UI.Screens;
using UnityEngine;

namespace Kitefall.App
{
    /// <summary>
    /// The player journey (spec 3): splash → login → terms/age → region/name → tutorial → first duel → home,
    /// and every match: matchmaking → match → results.
    /// </summary>
    public sealed class AppFlow
    {
        public MatchRequest LastRequest { get; private set; }
        public TutorialDirector Tutorial { get; private set; }
        public MatchHudScreen Hud { get; private set; }

        // =====================================================================
        // Start-up
        // =====================================================================

        public async Task BootAsync()
        {
            Game.UI.Show(new SplashScreen());
            float start = Time.realtimeSinceStartup;
            await Game.Remote.ApplyAsync(Game.Balance);
            bool ok = await Game.Backend.InitializeAsync();
            while (Time.realtimeSinceStartup - start < 2.2f) await Task.Yield();

            if (Game.Balance != null && Version.Older(Application.version, Game.Balance.minimumVersion))
            {
                Game.UI.Show(new ForceUpdateScreen());
                return;
            }
            if (!ok) Game.UI.Toast("No connection – retry later. Offline mode.", Theme.Warn, 3f);

            if (Game.Backend.IsSignedIn) await AfterSignInAsync();
            else Game.UI.Show(new LoginScreen());
        }

        public async Task SignInAsync(SignInMethod method)
        {
            Game.UI.SetBusy(true, "Signing in…");
            OpResult r = await Game.Backend.SignInAsync(method);
            Game.UI.SetBusy(false);
            if (!r.Ok) { Game.UI.Toast(r.Error, Theme.Bad); return; }
            await AfterSignInAsync();
        }

        private async Task AfterSignInAsync()
        {
            PlayerProfile p = Game.Profile;
            Game.Ads.Initialize();
            Game.Purchases.Initialize();
            Game.Analytics.Initialize(p != null && !p.under13);
            if (p == null || !p.termsAccepted) { Game.UI.Show(new TermsScreen()); return; }
            await ContinueOnboardingAsync();
        }

        public async Task AcceptTermsAsync(bool under13, bool personalisedAds)
        {
            OpResult r = await Game.Backend.RunAsync(EconomyCommands.AcceptTerms, under13 ? "1" : "0", personalisedAds ? "1" : "0");
            if (!r.Ok) { Game.UI.Toast(r.Error, Theme.Bad); return; }
            Game.Ads.Initialize();
            await ContinueOnboardingAsync();
        }

        public async Task ContinueOnboardingAsync()
        {
            PlayerProfile p = Game.Profile;
            if (!p.welcomeGranted)
            {
                Game.UI.Show(new ProfileSetupScreen());
                return;
            }
            if (!p.tutorialDone) { StartTutorial(); return; }
            if (!p.firstDuelDone)
            {
                StartMatch(new MatchRequest { Mode = GameMode.QuickDuel, FirstDuel = true, Difficulty = BotDifficulty.Easy });
                return;
            }
            await GoHomeAsync(true);
        }

        public async Task FinishProfileSetupAsync(string name, string region, string country)
        {
            OpResult r = await Game.Backend.RunAsync(EconomyCommands.SetProfile, name, region, country, "1");
            if (!r.Ok) { Game.UI.Toast(r.Error, Theme.Bad); return; }
            await Game.Backend.RunAsync(EconomyCommands.Welcome);
            await ContinueOnboardingAsync();
        }

        // =====================================================================
        // Home
        // =====================================================================

        public async Task GoHomeAsync(bool dailyCheck = false)
        {
            Game.UI.CloseAllPopups();
            Game.UI.Show(new HomeScreen());
            ShowShowroom();
            Game.Audio.PlayMusic(0);
            if (dailyCheck) await DailyLoginAsync();
        }

        public void ShowShowroom()
        {
            PlayerProfile p = Game.Profile;
            if (p == null) return;
            KiteOwned k = p.Kite(p.loadout.kite);
            Game.Matches.ShowShowroom(p.loadout.kite, k != null ? k.skin : "", p.loadout.tail, p.loadout.dor);
        }

        private async Task DailyLoginAsync()
        {
            OpResult r = await Game.Backend.RunAsync(EconomyCommands.DailyLogin);
            if (!r.Ok) return;
            Game.UI.Message("Daily reward", string.Join("\n", r.Ledger));
            Game.UI.RefreshCurrent();
        }

        // =====================================================================
        // Matches
        // =====================================================================

        public void StartMatch(MatchRequest req)
        {
            PlayerProfile p = Game.Profile;
            if (p == null) return;
            ModeRules rules = ModeRules.For(req.Mode);

            if (req.Mode == GameMode.RankedDuel)
            {
                if (!RankRules.IsRankedUnlocked(p, Game.Ranks)) { Game.UI.Message("Ranked locked", "Ranked Duel unlocks at Silver rank or after 10 matches."); return; }
                if (p.queueBanUntilUtc > Game.Now) { Game.UI.Message("Queue ban", $"You left a Ranked match. Wait {(p.queueBanUntilUtc - Game.Now) / 60 + 1} min."); return; }
                if (!Game.Backend.IsOnline && !Game.Settings.Data.devBotRanked)
                {
                    Game.UI.Message("Online required", "Ranked never uses bots, so it needs the online service.\nSet up Unity Gaming Services (docs/ONLINE_SETUP.md) or, for testing only, enable Settings ▸ Developer ▸ Bot Ranked.");
                    return;
                }
            }
            if (!req.FirstDuel)
            {
                string err = EconomyService.ValidateLoadout(p, p.loadout, Game.Economy);
                if (err != null) { Game.UI.Message("Hangar", err); return; }
            }
            LastRequest = req.Clone();
            _ = Game.Backend.RunAsync(EconomyCommands.SetMode, req.Mode.ToString());
            Game.UI.Push(new MatchmakingScreen(req));
        }

        /// <summary>Called by the matchmaking screen once players are found (offline: bots fill).</summary>
        public void BeginMatch(MatchRequest req)
        {
            MatchSetup setup = BuildSetup(req, out int local);
            Game.UI.CloseAllPopups();
            Game.Matches.StartOffline(setup, local, OnMatchFinished);
            Hud = Game.UI.Show(new MatchHudScreen());
            Game.Matches.Presenter.EventRaised -= OnMatchEvent;
            Game.Matches.Presenter.EventRaised += OnMatchEvent;
        }

        public MatchSetup BuildSetup(MatchRequest req, out int localIndex)
        {
            PlayerProfile p = Game.Profile;
            int seed = req.Seed != 0 ? req.Seed : UnityEngine.Random.Range(1, int.MaxValue);
            ArenaType arena = req.Arena;
            if (req.Mode == GameMode.LastKiteFlying) arena = ArenaType.StormArchipelago;
            MatchSetup setup = MatchFactory.NewSetup(req.Mode, arena, seed);
            Game.Db.Configure(setup);

            if (req.Mode == GameMode.PrivateRoom)
            {
                if (req.PrivateDuel)
                {
                    setup.Rules = ModeRules.For(GameMode.QuickDuel);
                    setup.Rules.mode = GameMode.PrivateRoom;
                    setup.Rules.displayName = "Private Duel";
                    setup.Rules.lossMode = false;
                    setup.Rules.durabilityLoss = false;
                }
                if (req.MatchMinutes > 0) setup.Rules.matchTime = req.MatchMinutes * 60f;
            }
            if (req.Mode == GameMode.RankedDuel) setup.Rules.allowBots = true; // dev-only path guarded in StartMatch

            // You
            var me = new ParticipantSetup
            {
                playerId = p.playerId,
                displayName = p.displayName,
                country = p.country,
                rankPoints = p.rankPoints,
                team = 0,
                captain = setup.Rules.teamCount > 1
            };
            if (setup.Rules.IsDuel)
            {
                string kite = p.loadout.kite;
                for (int i = 0; i < 5; i++) me.stack.Add(EconomyService.BuildMatchLoadout(p, p.loadout, kite));
            }
            else
            {
                foreach (string kite in p.loadout.stack)
                {
                    KiteOwned k = p.Kite(kite);
                    if (kite == "guddi" || (k != null && k.count > 0)) me.stack.Add(EconomyService.BuildMatchLoadout(p, p.loadout, kite));
                }
                if (me.stack.Count == 0) me.stack.Add(EconomyService.BuildMatchLoadout(p, p.loadout, p.loadout.kite));
            }
            setup.Participants.Add(me);
            localIndex = 0;

            // Bots (labelled; never in real Ranked – spec 5)
            BotDifficulty diff = req.Difficulty ?? PickDifficulty(p);
            int count = req.Players > 0 ? req.Players : setup.Rules.mode == GameMode.LastKiteFlying ? 12 : setup.Rules.maxPlayers;
            if (setup.Rules.IsDuel) count = 2;
            MatchFactory.FillWithBots(setup, count, diff, p.loadout.dor);
            if (req.FirstDuel && setup.Participants.Count > 1) setup.Participants[1].displayName = "Practice opponent";
            return setup;
        }

        private static BotDifficulty PickDifficulty(PlayerProfile p)
        {
            if (p.matches < 5) return BotDifficulty.Easy;
            if (p.matches >= 15 && p.WinRate > 0.6f) return BotDifficulty.Hard;
            return BotDifficulty.Medium;
        }

        private void OnMatchEvent(MatchEvent e)
        {
            IMatchSession s = Game.Matches.Session;
            if (s == null || Hud == null) return;
            int me = s.LocalIndex;
            string Name(int i) => i < 0 ? "?" : i == me ? "You" : s.Setup.Participants[i].displayName;
            switch (e.Type)
            {
                case MatchEventType.KiteCut:
                    Hud.Feed($"{Name(e.B)} cut {Name(e.A)}", e.B == me ? Theme.Good : e.A == me ? Theme.Bad : Color.white);
                    if (e.B == me) Hud.Announce("YOU CUT THEIR LINE!", $"−{e.Value:0} m of their dor cut away");
                    else if (e.A == me) Hud.Announce("YOUR LINE WAS CUT", "Read the results to see why");
                    break;
                case MatchEventType.SelfSnap:
                    Hud.Feed($"{Name(e.A)}: self-snap", e.A == me ? Theme.Bad : Theme.TextDim);
                    if (e.A == me) Hud.Announce("SELF-SNAP!", $"Tension stayed above Strength {e.Value:0} too long");
                    break;
                case MatchEventType.Crash:
                    Hud.Feed($"{Name(e.A)} crashed", Theme.TextDim);
                    if (e.A == me) Hud.Announce("CRASH!", "−15 % durability");
                    break;
                case MatchEventType.PerfectLaunch:
                    if (e.A == me) Hud.Announce("PERFECT LAUNCH", "+10 m height");
                    break;
                case MatchEventType.Assist:
                    if (e.A == me) Hud.Feed("Assist +40", Theme.Good);
                    break;
                case MatchEventType.CrateSpawned:
                    Hud.Feed("Sky crate: " + SkyCrate.DisplayName((CrateContent)e.Code), Theme.Warn);
                    break;
                case MatchEventType.CrateCollected:
                    Hud.Feed($"{Name(e.A)} took {SkyCrate.DisplayName((CrateContent)e.Code)}", e.A == me ? Theme.Good : Theme.TextDim);
                    break;
                case MatchEventType.GustWarning:
                    Hud.Feed("Gust incoming!", new Color(1f, 0.7f, 0.3f));
                    break;
                case MatchEventType.RoundPhaseChanged:
                    if ((RoundPhase)e.Code == RoundPhase.Gust) Hud.Announce("GUST PHASE", "Contest with the gust for ×1.08 wind power");
                    if ((RoundPhase)e.Code == RoundPhase.Storm) Hud.Announce("STORM", "Strong wind · sweet zone −3 · power ×1.10");
                    break;
                case MatchEventType.ArenaEvent:
                    Hud.Feed(s.Setup.Arena == ArenaType.OldCityRooftops ? "A flock of birds shakes the lines!" : "Sea gust!", Color.white);
                    break;
                case MatchEventType.StormShrink:
                    Hud.Feed("The storm closes in", new Color(0.8f, 0.5f, 1f));
                    break;
                case MatchEventType.RoundEnded:
                {
                    var rounds = s.Rounds;
                    string why = rounds.Count > 0 ? ResultsExplainer.ExplainRound(rounds[rounds.Count - 1], me) : "";
                    Hud.Announce(e.A == me ? "ROUND WON" : e.A < 0 ? "ROUND DRAW" : "ROUND LOST", why, 4f);
                    break;
                }
                case MatchEventType.Disconnected:
                    Hud.Feed($"{Name(e.A)} disconnected – kite on hold", Theme.Warn);
                    break;
            }
        }

        private async void OnMatchFinished(IMatchSession session)
        {
            MatchResult result = session.Result;
            Game.Matches.Presenter.EventRaised -= OnMatchEvent;
            if (Tutorial != null && Tutorial.Active)
            {
                await Tutorial.FinishAsync(result);
                return;
            }
            OpResult reward = null;
            if (result != null && !session.IsOnline)
            {
                int opponentRank = 0;
                if (result.Participants.Count > 1) opponentRank = session.Setup.Participants[session.LocalIndex == 0 ? 1 : 0].rankPoints;
                MatchRequest req = LastRequest;
                GameMode mode = result.Mode;
                // Bot Ranked (developer testing) never changes real rank points.
                if (mode == GameMode.RankedDuel && !Game.Backend.IsOnline) result.Mode = GameMode.QuickDuel;
                reward = await Game.Backend.SubmitMatchAsync(result, new MatchRewardContext
                {
                    MatchId = $"{session.Setup.Seed}_{Game.Now}",
                    Mode = result.Mode,
                    MyIndex = session.LocalIndex,
                    OpponentRankPoints = opponentRank,
                    VersusBotsOnly = true
                });
                result.Mode = mode;
            }
            Hud = null;
            Game.UI.Show(new ResultsScreen(session, reward));
        }

        public void QuitMatch() => Game.Matches.QuitMatch();

        /// <summary>Leaves the results screen. Interstitial (if allowed) plays here – never over results (spec 13.2).</summary>
        public async Task LeaveResultsAsync(bool rematch)
        {
            Game.Matches.EndSession();
            await Game.Ads.MaybeShowInterstitialAsync();
            if (rematch && LastRequest != null)
            {
                MatchRequest r = LastRequest.Clone();
                r.Seed = 0;
                r.FirstDuel = false;
                await GoHomeAsync();
                StartMatch(r);
            }
            else await GoHomeAsync();
        }

        // =====================================================================
        // Tutorial
        // =====================================================================

        public void StartTutorial()
        {
            Tutorial = Game.App.gameObject.GetComponent<TutorialDirector>() ?? Game.App.gameObject.AddComponent<TutorialDirector>();
            Tutorial.Begin();
        }

        internal void OnTutorialMatchStarted(MatchHudScreen hud)
        {
            Hud = hud;
            Game.Matches.Presenter.EventRaised -= OnMatchEvent;
            Game.Matches.Presenter.EventRaised += OnMatchEvent;
        }
    }

    /// <summary>Compares "1.2.3" style versions.</summary>
    public static class Version
    {
        public static bool Older(string current, string minimum)
        {
            if (string.IsNullOrEmpty(minimum)) return false;
            string[] a = (current ?? "0").Split('.'), b = minimum.Split('.');
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length && int.TryParse(a[i], out int xa) ? xa : 0;
                int y = i < b.Length && int.TryParse(b[i], out int yb) ? yb : 0;
                if (x != y) return x < y;
            }
            return false;
        }
    }
}
