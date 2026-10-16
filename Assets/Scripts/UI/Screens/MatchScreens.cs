using System.Text;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>
    /// Matchmaking (spec 6): same region, rank ±1 tier → ±2 after 10 s → labelled bots after 20 s (non-ranked).
    /// Offline there are no other humans, so bots are found quickly and labelled.
    /// </summary>
    public sealed class MatchmakingScreen : MenuScreen
    {
        public override string Title => "Finding players";
        protected override bool ShowBack => false;
        private readonly MatchRequest _req;
        private float _time;
        private Text _status;
        private bool _started;

        public MatchmakingScreen(MatchRequest req) => _req = req;

        protected override void BuildContent(RectTransform content)
        {
            ModeRules rules = ModeRules.For(_req.Mode);
            Image panel = UiKit.Panel(content, Theme.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 560));
            RectTransform col = UiKit.Column(panel.transform, 18, 40);
            col.Stretch();
            UiKit.Label(col, rules.displayName, Theme.TitleSize, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).Size(-1, 80);
            UiKit.Label(col, $"{ArenaLayout.DisplayName(_req.Mode == GameMode.LastKiteFlying ? ArenaType.StormArchipelago : _req.Arena)} · {Game.Profile.region}", 30, Theme.TextDim, TextAnchor.MiddleCenter).Size(-1, 50);
            _status = UiKit.Label(col, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _status.Size(-1, 160);
            UiKit.Button(col, "Cancel", () => UI.Pop(), Theme.PanelLight, 30).Size(-1, 90);
        }

        public override void Tick()
        {
            if (_started || _status == null) return;
            _time += Time.unscaledDeltaTime;
            var mm = Game.Balance != null ? Game.Balance.matchmaking : new MatchmakingSettings();
            int range = MatchmakingRules.TierRange(_time, mm);
            bool online = Game.Backend.IsOnline;
            float botAfter = online ? mm.botFillAfter : 2.5f;
            _status.text = $"Searching {(int)_time}s  ·  rank ±{range} tier\n<size=26>{(online ? $"Bots join after {mm.botFillAfter:0}s (never in Ranked)" : "Offline: practice flyers (labelled BOT)")}</size>";
            if (_time >= botAfter)
            {
                _started = true;
                Game.Flow.BeginMatch(_req);
            }
        }

        public override bool OnBack() => true;
    }

    /// <summary>Private room (spec 5): 2–8 players, custom 5–20 min, no loss. Offline: you vs chosen bots.</summary>
    public sealed class PrivateRoomScreen : MenuScreen
    {
        public override string Title => "Private Room";
        private readonly MatchRequest _req;

        public PrivateRoomScreen(ArenaType arena)
        {
            _req = new MatchRequest { Mode = GameMode.PrivateRoom, Arena = arena, Players = 4, MatchMinutes = 10, Difficulty = BotDifficulty.Medium };
        }

        protected override void BuildContent(RectTransform content)
        {
            RectTransform col = UiKit.Column(content, 16);
            col.Band(Vector2.zero, Vector2.one, new Vector2(300, 0), new Vector2(-300, 0));
            UiKit.Choice(col, "Format", new[] { "Free-for-all", "1v1 Duel" }, _req.PrivateDuel ? 1 : 0, i => { _req.PrivateDuel = i == 1; Refresh(); });
            if (!_req.PrivateDuel)
            {
                UiKit.Stepper(col, "Players", _req.Players.ToString(), () => { _req.Players = Mathf.Max(2, _req.Players - 1); Refresh(); }, () => { _req.Players = Mathf.Min(8, _req.Players + 1); Refresh(); });
                UiKit.Stepper(col, "Minutes", _req.MatchMinutes.ToString("0"), () => { _req.MatchMinutes = Mathf.Max(5, _req.MatchMinutes - 1); Refresh(); }, () => { _req.MatchMinutes = Mathf.Min(20, _req.MatchMinutes + 1); Refresh(); });
            }
            UiKit.Choice(col, "Bots", new[] { "Easy", "Medium", "Hard" }, (int)(_req.Difficulty ?? BotDifficulty.Medium), i => _req.Difficulty = (BotDifficulty)i);
            UiKit.Label(col, Game.Backend.IsOnline ? "Online: share the room code with friends (Relay). Empty seats fill with bots." : "Offline: you vs bots. Online rooms need UGS – see docs/ONLINE_SETUP.md.", 24, Theme.TextDim).Size(-1, 70);
            UiKit.Button(col, "Start", () => { UI.Pop(); Game.Flow.StartMatch(_req); }, Theme.Accent, 34).Size(-1, 110);
            if (Game.Backend.IsOnline && PrivateRoomOnline.Create != null)
            {
                UiKit.Button(col, "Create online room", () => PrivateRoomOnline.Create(_req), Theme.Good, 30).Size(-1, 90);
                UiKit.Button(col, "Join with code", () => PrivateRoomOnline.Join?.Invoke(), Theme.PanelLight, 30).Size(-1, 90);
            }
        }
    }

    /// <summary>Hooks filled by Kitefall.Online when Netcode + Relay are installed.</summary>
    public static class PrivateRoomOnline
    {
        public static System.Action<MatchRequest> Create;
        public static System.Action Join;
    }

    /// <summary>Results (spec 6, 9.7): win/loss, WHY, rewards ledger, Double / Rematch / Next / Share.</summary>
    public sealed class ResultsScreen : MenuScreen
    {
        public override string Title => _title;
        protected override bool ShowBack => false;
        private readonly IMatchSession _session;
        private OpResult _reward;
        private string _title = "Results";

        public ResultsScreen(IMatchSession session, OpResult reward)
        {
            _session = session;
            _reward = reward;
            MatchResult r = session.Result;
            if (r != null)
            {
                ParticipantResult me = r.Participants[session.LocalIndex];
                _title = r.Draw ? "Draw" : me.Won ? "Victory!" : me.Quit ? "You quit" : "Defeat";
            }
        }

        protected override void BuildContent(RectTransform content)
        {
            MatchResult r = _session.Result;
            if (r == null) { UiKit.Button(content, "Home", Home); return; }
            int me = _session.LocalIndex;
            ParticipantResult mine = r.Participants[me];

            RectTransform left = UiKit.Column(content, 12);
            left.Band(Vector2.zero, new Vector2(0.58f, 1), Vector2.zero, new Vector2(0, -10));
            UiKit.Label(left, Summary(r, mine), 30).Size(-1, 90);
            UiKit.Label(left, "Why", 32, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            UiKit.Label(left, Why(r, me), 26).Size(-1, 300);

            if (r.Participants.Count > 2)
            {
                UiKit.Label(left, "Standings", 32, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
                var ordered = new System.Collections.Generic.List<ParticipantResult>(r.Participants);
                ordered.Sort((a, b) => a.Placement.CompareTo(b.Placement));
                var sb = new StringBuilder();
                foreach (ParticipantResult p in ordered)
                {
                    string name = p.Index == me ? $"<color=#FF8C1A>{p.DisplayName}</color>" : p.DisplayName + (p.IsBot ? " (bot)" : "");
                    sb.Append($"#{p.Placement} {name} · {p.Score} pts · {p.Cuts} cuts · {p.Assists} assists\n");
                }
                UiKit.Label(left, sb.ToString(), 24).Size(-1, 300);
            }

            RectTransform right = UiKit.Column(content, 12);
            right.Band(new Vector2(0.6f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            Image box = UiKit.Panel(right, new Color(0, 0, 0, 0.5f));
            box.Size(-1, 470);
            RectTransform ledger = UiKit.Column(box.transform, 6, 20);
            ledger.Stretch();
            UiKit.Label(ledger, "Rewards & ledger", 30, Theme.Coin, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            if (_reward != null && _reward.Ok)
                foreach (string line in _reward.Ledger) UiKit.Label(ledger, line, 24).Size(-1, 32);
            else if (_reward != null) UiKit.Label(ledger, _reward.Error, 24, Theme.Bad).Size(-1, 32);

            bool canDouble = _reward != null && _reward.Ok && Game.Profile.lastReward != null && !Game.Profile.lastReward.doubled
                             && Game.Ads.CanShowRewarded(RewardedPlacement.DoubleRewards, AdContext.Results);
            if (canDouble) UiKit.Button(right, "Double coins + parts (watch ad)", DoubleRewards, Theme.Accent2, 26).Size(-1, 90);
            RectTransform row = UiKit.Row(right, 12);
            row.Size(-1, 100);
            UiKit.Button(row, "Rematch", () => _ = Game.Flow.LeaveResultsAsync(true), Theme.PanelLight, 28).Size(-1, 96, 1);
            UiKit.Button(row, "Next", Home, Theme.Accent, 30).Size(-1, 96, 1);
            UiKit.Button(right, "Share", Share, Theme.PanelLight, 26).Size(-1, 80);
        }

        private string Summary(MatchResult r, ParticipantResult me)
        {
            ModeRules rules = _session.Setup.Rules;
            if (rules.IsDuel)
            {
                int other = _session.LocalIndex == 0 ? 1 : 0;
                return $"{rules.displayName} · Rounds {me.RoundsWon}–{r.Participants[other].RoundsWon} vs {r.Participants[other].DisplayName}" +
                       $"\n<size=24>Cuts {me.Cuts} · contests won {me.ContestsWon}/{me.ContestsWon + me.ContestsLost} · sweet zone {me.SweetFraction:P0}</size>";
            }
            return $"{rules.displayName} · Place #{me.Placement} of {r.Participants.Count} · {me.Score} pts" +
                   $"\n<size=24>Cuts {me.Cuts} · assists {me.Assists} · self-snaps {me.SelfSnaps} · crashes {me.Crashes}</size>";
        }

        private static string Why(MatchResult r, int me)
        {
            var sb = new StringBuilder();
            if (r.Rounds.Count > 0)
            {
                foreach (RoundRecord rr in r.Rounds)
                {
                    string res = rr.Winner < 0 ? "Draw" : rr.Winner == me ? "<color=#3BB273>Won</color>" : "<color=#E5484D>Lost</color>";
                    sb.Append($"Round {rr.Number} – {res}: {ResultsExplainer.ExplainRound(rr, me)}\n");
                }
                return sb.ToString();
            }
            int shown = 0;
            for (int i = r.Contests.Count - 1; i >= 0 && shown < 4; i--)
            {
                ContestSummary c = r.Contests[i];
                if (!c.Involves(me)) continue;
                string text = ResultsExplainer.Explain(c, me);
                if (string.IsNullOrEmpty(text)) continue;
                sb.Append("• ").Append(text).Append('\n');
                shown++;
            }
            return shown == 0 ? "No contests – cross an opponent's line to start one." : sb.ToString();
        }

        private async void DoubleRewards()
        {
            OpResult r = await Game.Ads.ShowRewardedAsync(RewardedPlacement.DoubleRewards, AdContext.Results);
            UI.ToastResult(r);
            if (r.Ok && _reward != null) _reward.Ledger.AddRange(r.Ledger);
            if (IsAlive) Refresh();
        }

        private void Home() => _ = Game.Flow.LeaveResultsAsync(false);

        private void Share()
        {
            ParticipantResult me = _session.Result.Participants[_session.LocalIndex];
            string text = me.Won ? $"I just won a {_session.Setup.Rules.displayName} in Kitefall with {me.Cuts} cuts!" : $"Flying in Kitefall – {me.Cuts} cuts in {_session.Setup.Rules.displayName}.";
            Game.Share.ShareResult(text, Game.Balance.storeUrl);
            UI.Toast("Screenshot saved · share sheet opened");
        }

        public override bool OnBack() { Home(); return false; }
    }
}
