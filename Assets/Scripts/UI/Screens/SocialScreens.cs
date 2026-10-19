using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>Leaderboard (spec 4.4): global top 500, country, friends.</summary>
    public sealed class LeaderboardScreen : MenuScreen
    {
        public override string Title => "Leaderboard";
        private static int _tab;
        private List<LeaderboardEntry> _entries;
        private bool _loading;

        protected override void BuildContent(RectTransform content)
        {
            RectTransform tabs = UiKit.Rect("Tabs", content);
            tabs.Band(new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(0, -76), Vector2.zero);
            UiKit.Tabs(tabs, new[] { "Global", "Country", "Friends" }, _tab, i => { _tab = i; _entries = null; Refresh(); });
            if (!Game.Backend.IsOnline)
            {
                Text note = UiKit.Label(content, "Offline: sample flyers – connect UGS for real leaderboards", 22, Theme.Warn, TextAnchor.MiddleRight);
                note.rectTransform.Band(new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -76), Vector2.zero);
            }
            RectTransform host = UiKit.Rect("List", content);
            host.Band(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -90));
            UiKit.Scroll(host, out RectTransform list, true, 8).GetComponent<RectTransform>().Stretch();

            if (_entries == null) { Load(); UiKit.Label(list, "Loading…", 28).Size(-1, 60); return; }
            foreach (LeaderboardEntry e in _entries)
            {
                Image card = UiKit.Panel(list, e.isMe ? Theme.CardSelected : Theme.Card);
                card.Size(-1, 76);
                RectTransform row = UiKit.Row(card.transform, 16, 10);
                row.Stretch();
                RankTier tier = RankRules.TierOf(e.points, Game.Ranks, e.rank);
                UiKit.Label(row, $"#{e.rank}", 30, e.rank <= 3 ? Theme.Coin : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).Size(110, 60);
                UiKit.Label(row, $"{e.name}  <size=20>{e.country}</size>", 28).Size(-1, 60, 1);
                UiKit.Label(row, RankRules.TierName(tier), 24, Theme.TextDim).Size(200, 60);
                UiKit.Label(row, $"{e.points} RP", 28, Color.white, TextAnchor.MiddleRight, FontStyle.Bold).Size(180, 60);
            }
        }

        private async void Load()
        {
            if (_loading) return;
            _loading = true;
            _entries = await Game.Backend.GetLeaderboardAsync((LeaderboardScope)_tab, _tab == 0 ? 100 : 50);
            _loading = false;
            if (IsAlive) Refresh();
        }
    }

    public sealed class FriendsScreen : MenuScreen
    {
        public override string Title => "Friends";
        private string _input = "";

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            RectTransform col = UiKit.Column(content, 14);
            col.Stretch();
            UiKit.Label(col, $"Your player ID: <b>{p.playerId}</b>  (share it so friends can add you)", 26).Size(-1, 50);
            RectTransform add = UiKit.Row(col, 12);
            add.Size(-1, 84);
            UiKit.Input(add, "Friend's player ID", _input, 40, v => _input = v).Size(-1, 80, 1);
            UiKit.Button(add, "Add friend", () => Run(EconomyCommands.AddFriend, _input.Trim()), Theme.Accent, 26).Size(240, 80);
            if (!Game.Backend.IsOnline) UiKit.Label(col, "Offline: friends are saved locally. Invites and private rooms with friends need the online service.", 22, Theme.Warn).Size(-1, 50);
            foreach (string f in p.friends)
            {
                string id = f;
                Image card = Card(col);
                card.Size(-1, 84);
                RectTransform row = UiKit.Row(card.transform, 12, 10);
                row.Stretch();
                UiKit.Label(row, id, 28).Size(-1, 60, 1);
                UiKit.Button(row, "Remove", () => Run(EconomyCommands.RemoveFriend, id), Theme.PanelLight, 22).Size(160, 64);
                UiKit.Button(row, "Block", () => Run(EconomyCommands.Block, id), Theme.Bad, 22).Size(140, 64);
            }
            if (p.friends.Count == 0) UiKit.Label(col, "No friends yet.", 26, Theme.TextDim).Size(-1, 50);
        }
    }

    /// <summary>Profile (spec 4.4): rank, stats, last 20 matches, linking, name change, block list.</summary>
    public sealed class ProfileScreen : MenuScreen
    {
        public override string Title => "Profile";
        private string _name;

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            _name = _name ?? p.displayName;
            RectTransform left = UiKit.Column(content, 12);
            left.Band(Vector2.zero, new Vector2(0.48f, 1), Vector2.zero, Vector2.zero);
            RankTier tier = RankRules.TierOf(p.rankPoints, Game.Ranks);
            UiKit.Label(left, $"<b>{p.displayName}</b>  {p.country} · {p.region}", 40).Size(-1, 60);
            UiKit.Label(left, $"{RankRules.TierName(tier)} · {p.rankPoints} RP (best {RankRules.TierName((RankTier)p.bestTier)})   ·   Level {p.level}", 28, Theme.Coin).Size(-1, 44);
            UiKit.Label(left,
                $"Matches {p.matches}   Wins {p.wins} ({p.WinRate:P0})\n" +
                $"Cuts {p.cuts}   Best streak {p.bestStreak}   Current {p.winStreak}\n" +
                $"Favourite kite {SpecDefaults.NameOf(p.FavouriteKite() ?? "guddi")} · {Terms.LineLower} {SpecDefaults.NameOf(p.FavouriteDor() ?? "sooti")}", 28).Size(-1, 130);

            UiKit.Label(left, "Account", 30, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            UiKit.Label(left, p.guest ? "<color=#F5C400>Guest – link an account so you never lose progress.</color>" : $"Linked: {p.linkedProvider}", 24).Size(-1, 40);
            RectTransform link = UiKit.Row(left, 12);
            link.Size(-1, 76);
            UiKit.Button(link, "Link Google", () => Link(SignInMethod.Google), Theme.PanelLight, 24).Size(-1, 72, 1);
            UiKit.Button(link, "Link Apple", () => Link(SignInMethod.Apple), Theme.PanelLight, 24).Size(-1, 72, 1);

            UiKit.Label(left, "Change name (once every 30 days)", 24, Theme.TextDim).Size(-1, 36);
            RectTransform nameRow = UiKit.Row(left, 12);
            nameRow.Size(-1, 80);
            UiKit.Input(nameRow, "New name", _name, NameRules.MaxLength, v => _name = v).Size(-1, 76, 1);
            UiKit.Button(nameRow, "Save", () => Run(EconomyCommands.SetProfile, _name, p.region, p.country, "0"), Theme.Accent, 24).Size(160, 76);

            UiKit.Label(left, $"Blocked: {(p.blocked.Count == 0 ? "nobody" : string.Join(", ", p.blocked))}", 22, Theme.TextDim).Size(-1, 40);
            foreach (string b in p.blocked)
            {
                string id = b;
                UiKit.Button(left, "Unblock " + id, () => Run(EconomyCommands.Unblock, id), Theme.PanelLight, 22).Size(-1, 60);
            }

            RectTransform rightHost = UiKit.Rect("History", content);
            rightHost.Band(new Vector2(0.5f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Scroll(rightHost, out RectTransform hist, true, 8).GetComponent<RectTransform>().Stretch();
            UiKit.Label(hist, "Last 20 matches", 30, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            foreach (MatchHistoryEntry h in p.history)
            {
                Image card = UiKit.Panel(hist, Theme.Card);
                card.Size(-1, 70);
                string res = h.draw ? "<color=#F5C400>DRAW</color>" : h.won ? "<color=#3BB273>WIN</color>" : "<color=#E5484D>LOSS</color>";
                Text t = UiKit.Label(card.transform, $"{res}  {h.mode}  vs {h.opponent}  · #{h.placement} · {h.cuts} cuts{(h.rankDelta != 0 ? $" · {h.rankDelta:+0;-0} RP" : "")}", 24);
                t.rectTransform.Stretch(16, 4, 16, 4);
            }
            if (p.history.Count == 0) UiKit.Label(hist, "No matches yet.", 24, Theme.TextDim).Size(-1, 40);
        }

        private async void Link(SignInMethod m)
        {
            UI.SetBusy(true);
            OpResult r = await Game.Backend.LinkAccountAsync(m);
            UI.SetBusy(false);
            UI.ToastResult(r, r.Ok ? "Account linked" : null);
            if (IsAlive) Refresh();
        }
    }

    public sealed class InboxScreen : MenuScreen
    {
        public override string Title => "Inbox";

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            UiKit.Scroll(content, out RectTransform list).GetComponent<RectTransform>().Stretch();
            for (int i = p.inbox.Count - 1; i >= 0; i--)
            {
                InboxMessage m = p.inbox[i];
                string id = m.id;
                Image card = Card(list);
                card.Size(-1, 150);
                RectTransform row = UiKit.Row(card.transform, 16, 16);
                row.Stretch();
                UiKit.Label(row, $"<b>{m.title}</b>\n<size=24>{m.body}</size>", 28).Size(-1, 120, 1);
                bool hasGift = m.coins > 0 || m.gems > 0;
                if (hasGift) UiKit.Button(row, m.claimed ? "Claimed" : $"Claim {m.coins} coins", () => Run(EconomyCommands.ClaimInbox, id), m.claimed ? Theme.Disabled : Theme.Good, 22).Interactable(!m.claimed).Size(240, 76);
            }
            if (p.inbox.Count == 0) UiKit.Label(list, "No messages.", 26, Theme.TextDim).Size(-1, 50);
        }
    }
}
