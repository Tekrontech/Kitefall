using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI.Screens
{
    /// <summary>
    /// Wardrobe (upgrade F1): Characters · Outfits · Accessories · Emotes with a 3D turntable preview of your character.
    /// Equip / Buy go through the server rules (ownership is checked there). Rarity = frame colour. Never stats.
    /// </summary>
    public sealed class WardrobeScreen : MenuScreen
    {
        public override string Title => "Wardrobe";
        protected override float BackgroundAlpha => 0.0f;
        private static int _tab;
        private static readonly string[] Tabs = { "Characters", "Outfits", "Accessories", "Emotes" };
        private string _previewCharacter, _previewOutfit;
        private string[] _previewAcc;
        private int _emoteSlot = -1;

        public override void OnShow()
        {
            PlayerProfile p = Game.Profile;
            _previewCharacter = p.characterId;
            _previewOutfit = p.outfitId;
            _previewAcc = p.accessories.ToArray();
            Game.Flow?.ShowShowroom();
            Game.Matches.Showroom.Turntable(true);
            KitefallGraphics.SetCloseUp(true);
            Preview();
        }

        public override void OnHide()
        {
            Game.Matches.Showroom.Turntable(false);
            KitefallGraphics.SetCloseUp(false);
            PlayerProfile p = Game.Profile;
            Game.Matches.Showroom.SetCharacter(p.characterId, p.outfitId, p.accessories.ToArray());
        }

        private void Preview() => Game.Matches.Showroom.SetCharacter(_previewCharacter, _previewOutfit, _previewAcc);

        protected override void BuildContent(RectTransform content)
        {
            PlayerProfile p = Game.Profile;
            // The right 45 % shows the 3D character (turntable); the list sits on the left.
            Image panel = UiKit.Panel(content, new Color(0.04f, 0.07f, 0.12f, 0.88f), "ListPanel", true);
            panel.rectTransform.Band(Vector2.zero, new Vector2(0.55f, 1f), Vector2.zero, Vector2.zero);
            RectTransform tabs = UiKit.Rect("Tabs", panel.transform);
            tabs.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -84), new Vector2(-12, -8));
            UiKit.Tabs(tabs, Tabs, _tab, i => { _tab = i; Refresh(); });
            RectTransform host = UiKit.Rect("List", panel.transform);
            host.Band(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -96));
            UiKit.Scroll(host, out RectTransform list).GetComponent<RectTransform>().Stretch();

            switch (_tab)
            {
                case 0: Grid(list, CharacterCatalog.Characters, p); break;
                case 1: Grid(list, CharacterCatalog.Outfits, p); break;
                case 2:
                    foreach (AccessorySlot slot in System.Enum.GetValues(typeof(AccessorySlot)))
                    {
                        UiKit.Label(list, slot.ToString(), 28, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 40);
                        Grid(list, CharacterCatalog.Accessories.FindAll(a => a.slot == (int)slot), p);
                    }
                    break;
                default: Emotes(list, p); break;
            }

            Text hint = UiKit.Label(content, "Cosmetic only – looks, never stats.", 22, Theme.TextDim, TextAnchor.LowerRight).Shadowed();
            hint.rectTransform.Band(new Vector2(0.56f, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(-10, 40));
        }

        private void Grid(RectTransform list, List<CosmeticItem> items, PlayerProfile p)
        {
            RectTransform grid = UiKit.Grid(list, new Vector2(300, 250), new Vector2(14, 14), 3);
            foreach (CosmeticItem item in items) Tile(grid, item, p);
        }

        private void Tile(RectTransform grid, CosmeticItem item, PlayerProfile p)
        {
            bool equipped = IsEquipped(item, p);
            Image card = Card(grid, equipped);
            // Rarity frame colour (grey / blue / purple / gold).
            card.color = Color.Lerp(Theme.Card, Theme.RarityColors[(int)item.rarity], 0.28f);
            Image frame = UiKit.Image(card.transform, Theme.RarityColors[(int)item.rarity], Sprites.RoundedSmall, "Frame");
            frame.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), Vector2.zero);
            RectTransform col = UiKit.Column(card.transform, 6, 12);
            col.Stretch();
            Image swatch = UiKit.Image(col, SwatchColor(item), Sprites.Rounded, "Swatch", true);
            swatch.Size(-1, 86);
            var pb = swatch.gameObject.AddComponent<Button>();
            pb.onClick.AddListener(() => TryOn(item));
            Image icon = UiKit.Image(swatch.transform, new Color(0, 0, 0, 0.55f), Icons.Get(item.type == CosmeticType.Emote ? "emote" : item.type == CosmeticType.Character ? "person" : "star"), "Icon");
            icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            Sprite art = Art.ForCosmetic(item);
            if (art != null)
            {
                swatch.sprite = art; swatch.color = Color.white; swatch.preserveAspect = true;
                icon.gameObject.SetActive(false);
            }
            string region = item.type == CosmeticType.Character ? $" · {item.region}" : "";
            UiKit.Label(col, $"<b>{item.name}</b>\n<size=18><color=#{ColorUtility.ToHtmlStringRGB(Theme.RarityColors[(int)item.rarity])}>{item.rarity}</color>{region}</size>", 22).Size(-1, 58);
            bool owned = EconomyService.OwnsCosmetic(p, item.id) || item.type == CosmeticType.Outfit && CharacterCatalog.DefaultOutfitOf(p.characterId) == item.id;
            string id = item.id;
            if (owned)
            {
                string op = item.type == CosmeticType.Character ? EconomyCommands.EquipCharacter : item.type == CosmeticType.Outfit ? EconomyCommands.EquipOutfit : EconomyCommands.EquipAccessory;
                string[] args = item.type == CosmeticType.Accessory ? new[] { item.slot.ToString(), equipped ? "" : id } : new[] { id };
                UiKit.Button(col, equipped ? (item.type == CosmeticType.Accessory ? "Remove" : "Equipped") : "Equip", () => Run(op, args),
                    equipped ? Theme.Good : Theme.Accent2, 22).Interactable(!equipped || item.type == CosmeticType.Accessory).Size(-1, 54);
            }
            else if (item.passOnly) UiKit.Label(col, "Season Pass reward", 20, Theme.Coin, TextAnchor.MiddleCenter).Size(-1, 54);
            else if (p.level < item.unlockLevel && item.gemPrice == 0) UiKit.Label(col, $"Unlocks at level {item.unlockLevel}", 20, Theme.TextDim, TextAnchor.MiddleCenter).Size(-1, 54);
            else
            {
                RectTransform row = UiKit.Row(col, 6);
                row.Size(-1, 54);
                UiKit.Button(row, UiKit.Gems(item.gemPrice), () => Buy(item, false), Theme.PanelLight, 20).Size(-1, 52, 1);
                if (item.coinPrice > 0) UiKit.Button(row, UiKit.Coins(item.coinPrice), () => Buy(item, true), Theme.PanelLight, 20).Size(-1, 52, 1);
            }
        }

        private static Color SwatchColor(CosmeticItem item)
        {
            if (item.type == CosmeticType.Character) return KiteVisualFactory.Hex(item.skinHex);
            if (item.type == CosmeticType.Emote) return Theme.RarityColors[(int)item.rarity];
            return KiteVisualFactory.Hex(item.primaryHex);
        }

        private static bool IsEquipped(CosmeticItem item, PlayerProfile p)
        {
            switch (item.type)
            {
                case CosmeticType.Character: return p.characterId == item.id;
                case CosmeticType.Outfit: return (string.IsNullOrEmpty(p.outfitId) ? CharacterCatalog.DefaultOutfitOf(p.characterId) : p.outfitId) == item.id;
                case CosmeticType.Accessory: return item.slot < p.accessories.Count && p.accessories[item.slot] == item.id;
                case CosmeticType.Emote: return p.emoteSlots.Contains(item.id);
            }
            return false;
        }

        /// <summary>Tapping the swatch previews the item on the 3D character (even before buying).</summary>
        private void TryOn(CosmeticItem item)
        {
            switch (item.type)
            {
                case CosmeticType.Character: _previewCharacter = item.id; _previewOutfit = ""; break;
                case CosmeticType.Outfit: _previewOutfit = item.id; break;
                case CosmeticType.Accessory:
                    _previewAcc = (string[])_previewAcc.Clone();
                    _previewAcc[item.slot] = _previewAcc[item.slot] == item.id ? "" : item.id;
                    break;
                case CosmeticType.Emote: Game.Matches.Showroom.PreviewEmote(item.id); return;
            }
            Preview();
        }

        private void Buy(CosmeticItem item, bool coins)
        {
            UI.Confirm("Buy " + item.name + "?", "Cosmetic only – it changes how you look, never how you fly.",
                coins ? $"Buy for {item.coinPrice} coins" : $"Buy for {item.gemPrice} gems", () => Run(EconomyCommands.BuyCosmetic, item.id, coins ? "1" : "0"));
        }

        private void Emotes(RectTransform list, PlayerProfile p)
        {
            UiKit.Label(list, "Emote wheel – tap a slot, then an emote to put it there.", 22, Theme.TextDim).Size(-1, 36);
            RectTransform slots = UiKit.Grid(list, new Vector2(210, 80), new Vector2(10, 10), 4);
            for (int i = 0; i < 8; i++)
            {
                int slot = i;
                string id = i < p.emoteSlots.Count ? p.emoteSlots[i] : "";
                CosmeticItem e = CharacterCatalog.EmoteItem(id);
                UiKit.Button(slots, $"{i + 1}. {(e != null ? e.name : "empty")}", () => { _emoteSlot = slot; Refresh(); }, slot == _emoteSlot ? Theme.Accent : Theme.PanelLight, 20);
            }
            RectTransform grid = UiKit.Grid(list, new Vector2(300, 250), new Vector2(14, 14), 3);
            foreach (CosmeticItem e in CharacterCatalog.Emotes)
            {
                if (_emoteSlot >= 0 && EconomyService.OwnsCosmetic(p, e.id))
                {
                    Image card = Card(grid);
                    RectTransform col = UiKit.Column(card.transform, 6, 12);
                    col.Stretch();
                    UiKit.Label(col, $"<b>{e.name}</b>", 24).Size(-1, 50);
                    string id = e.id;
                    UiKit.Button(col, "Preview", () => Game.Matches.Showroom.PreviewEmote(id), Theme.PanelLight, 22).Size(-1, 54);
                    UiKit.Button(col, $"Put in slot {_emoteSlot + 1}", () => Run(EconomyCommands.SetEmoteSlot, _emoteSlot.ToString(), id), Theme.Accent2, 22).Size(-1, 54);
                }
                else Tile(grid, e, p);
            }
        }
    }

    /// <summary>
    /// Squad lobby (upgrade F2): up to 4 friends on a rooftop stage, crown on the leader, ready ticks, the leader picks the mode
    /// and taps PLAY. Join by code, invite or share link (kitefall://party/CODE). Duels need a party of one.
    /// </summary>
    public sealed class SquadLobbyScreen : MenuScreen
    {
        public override string Title => "Squad";
        protected override float BackgroundAlpha => 0.35f;
        private static readonly GameMode[] Modes = { GameMode.TeamBattle, GameMode.FreeForAll, GameMode.LastKiteFlying, GameMode.PrivateRoom, GameMode.QuickDuel };
        private string _code = "";

        public override void OnShow()
        {
            if (Game.Party != null) Game.Party.Changed += OnChanged;
        }

        public override void OnHide()
        {
            if (Game.Party != null) Game.Party.Changed -= OnChanged;
        }

        private void OnChanged() { if (IsAlive) Refresh(); }

        protected override void BuildContent(RectTransform content)
        {
            IPartyService party = Game.Party;
            if (party == null) return;
            if (!party.InParty)
            {
                RectTransform col = UiKit.Column(content, 18, 0, TextAnchor.UpperCenter);
                col.Place(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 520));
                UiKit.Label(col, "Play with friends: make a party, share the code, queue together.", 30, Color.white, TextAnchor.MiddleCenter).Size(-1, 90);
                UiKit.Button(col, "Create party", async () => { OpResult r = await party.CreateAsync(); UI.ToastResult(r); }, Theme.Good, 34).Size(-1, 100);
                UiKit.Input(col, "Party code (6 letters)", _code, 32, v => _code = v).Size(-1, 90);
                UiKit.Button(col, "Join with code", async () =>
                {
                    UI.SetBusy(true);
                    OpResult r = await party.JoinAsync(PartyRules.CodeFromLink(_code));
                    UI.SetBusy(false);
                    UI.ToastResult(r);
                }, Theme.Accent2, 30).Size(-1, 90);
                if (!party.Online) UiKit.Label(col, "Offline: friends can join once the online service is set up (docs/ONLINE_SETUP.md).", 22, Theme.TextDim, TextAnchor.MiddleCenter).Size(-1, 60);
                return;
            }

            // Four spots on a rooftop stage (the 3D characters stand behind this panel).
            RectTransform spots = UiKit.Row(content, 18, 0, TextAnchor.MiddleCenter);
            spots.Band(new Vector2(0, 0.3f), new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < PartyRules.MaxMembers; i++)
            {
                Image card = Card(spots);
                card.Size(380, 300);
                RectTransform col = UiKit.Column(card.transform, 8, 16, TextAnchor.UpperCenter);
                col.Stretch();
                if (i < party.Members.Count)
                {
                    PartyMember m = party.Members[i];
                    CosmeticItem ch = CharacterCatalog.Character(m.characterId);
                    Image av = UiKit.Image(col, KiteVisualFactory.Hex(ch.skinHex), Sprites.Circle, "Avatar");
                    av.Size(110, 110);
                    Sprite portrait = Art.Portrait(ch.id);
                    if (portrait != null) { av.sprite = portrait; av.color = Color.white; av.preserveAspect = true; }
                    if (m.leader) UiKit.Image(av.transform, Theme.Coin, Icons.Get("crown"), "Crown").rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, -10), new Vector2(60, 50));
                    UiKit.Label(col, $"<b>{m.name}</b>\n<size=20>{RankRules.TierName(RankRules.TierOf(m.rankPoints, Game.Ranks))} · {ch.name}</size>", 26, Color.white, TextAnchor.MiddleCenter).Size(-1, 70);
                    UiKit.Label(col, m.ready ? "<color=#3BB273>✔ READY</color>" : "<color=#FFFFFF99>not ready</color>", 24, Color.white, TextAnchor.MiddleCenter).Size(-1, 40);
                }
                else
                {
                    UiKit.Label(col, "Empty spot", 26, Theme.TextDim, TextAnchor.MiddleCenter).Size(-1, 160);
                    UiKit.Button(col, "Invite", () => Invite(party), Theme.PanelLight, 24).Size(-1, 70);
                }
            }

            RectTransform bottom = UiKit.Row(content, 14, 0, TextAnchor.MiddleCenter);
            bottom.Band(new Vector2(0, 0), new Vector2(1, 0.24f), Vector2.zero, Vector2.zero);
            UiKit.Label(bottom, $"Code <b>{party.Code}</b>", 32, Theme.Coin, TextAnchor.MiddleCenter).Size(300, 90);
            UiKit.Button(bottom, "Share", () => Invite(party), Theme.PanelLight, 26).Size(170, 90);
            if (party.IsLeader)
            {
                UiKit.Choice(bottom, "Mode", System.Array.ConvertAll(Modes, m => ModeRules.For(m).displayName), System.Array.IndexOf(Modes, party.Mode),
                    i => _ = party.SetModeAsync(Modes[i]));
                UiKit.Button(bottom, "PLAY", async () =>
                {
                    OpResult r = await party.PlayAsync();
                    if (!r.Ok) UI.ToastResult(r);
                }, Theme.Accent, 40).Size(260, 100);
            }
            else
            {
                PlayerProfile me = Game.Profile;
                PartyMember mine = null;
                foreach (PartyMember m in party.Members) if (m.playerId == me.playerId) mine = m;
                bool ready = mine != null && mine.ready;
                UiKit.Button(bottom, ready ? "Not ready" : "Ready", () => _ = party.SetReadyAsync(!ready), ready ? Theme.PanelLight : Theme.Good, 34).Size(260, 100);
            }
            UiKit.Button(bottom, "Leave", () => _ = party.LeaveAsync(), Theme.Bad, 26).Size(170, 90);
        }

        private void Invite(IPartyService party)
        {
            string text = $"Join my Kitefall squad! Code {party.Code} – {PartyRules.ShareLink(party.Code)}";
            ShareService.ShareText(text);
            GUIUtility.systemCopyBuffer = party.Code;
            UI.Toast("Code copied – send it to a friend");
        }
    }

    /// <summary>
    /// Training ground (upgrade F8): every kite, dum, dor, charkhi and tuning unlocked to try; target dummies, bot sparring and
    /// wind controls. No rewards, no inventory use, no durability, no rank, no ads.
    /// </summary>
    public sealed class TrainingScreen : MenuScreen
    {
        public override string Title => "Training";
        private static readonly TrainingRequest Req = new TrainingRequest();

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Scroll(content, out RectTransform list).GetComponent<RectTransform>().Stretch();
            UiKit.Label(list, "Try anything – nothing in your inventory changes.", 26, Theme.TextDim).Size(-1, 44);
            UiKit.Label(list, "Kite", 28, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            RectTransform kites = UiKit.Grid(list, new Vector2(240, 64), new Vector2(10, 10), 5);
            foreach (SpecDefaults.KiteInfo k in SpecDefaults.Kites)
            {
                string id = k.Stats.id;
                UiKit.Button(kites, k.Name, () => { Req.Kite = id; Refresh(); }, id == Req.Kite ? Theme.Accent : Theme.PanelLight, 22);
            }
            Pick(list, "Dum", SpecDefaults.Tails, Req.Tail, id => Req.Tail = id);
            Pick(list, "Dor", SpecDefaults.Dors, Req.Dor, id => Req.Dor = id);
            Pick(list, "Charkhi", SpecDefaults.Charkhis, Req.Charkhi, id => Req.Charkhi = id);
            UiKit.Label(list, "Tunings (up to 2)", 28, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            RectTransform tun = UiKit.Grid(list, new Vector2(300, 64), new Vector2(10, 10), 4);
            foreach (var t in SpecDefaults.Tunings)
            {
                string id = t.Key;
                bool on = Req.Tunings.Contains(id);
                UiKit.Button(tun, SpecDefaults.NameOf(id), () =>
                {
                    if (Req.Tunings.Contains(id)) Req.Tunings.Remove(id);
                    else if (Req.Tunings.Count < 2) Req.Tunings.Add(id);
                    Refresh();
                }, on ? Theme.Accent : Theme.PanelLight, 20);
            }
            UiKit.Toggle(list, "Target dummies (static · slow · fast)", Req.Dummies, v => Req.Dummies = v);
            UiKit.Stepper(list, "Sparring bots", Req.Bots.ToString(), () => { Req.Bots = Mathf.Max(0, Req.Bots - 1); Refresh(); }, () => { Req.Bots = Mathf.Min(4, Req.Bots + 1); Refresh(); });
            UiKit.Choice(list, "Bot difficulty", new[] { "Easy", "Medium", "Hard" }, (int)Req.Difficulty, i => Req.Difficulty = (BotDifficulty)i);
            UiKit.Button(list, "START TRAINING", () => { UI.ClearStack(); Game.Flow.StartTraining(Req); }, Theme.Accent, 38).Size(-1, 110);
        }

        private void Pick<T>(RectTransform list, string label, List<KeyValuePair<string, T>> items, string current, System.Action<string> set)
        {
            UiKit.Label(list, label, 28, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 44);
            RectTransform grid = UiKit.Grid(list, new Vector2(240, 64), new Vector2(10, 10), 5);
            foreach (var kv in items)
            {
                string id = kv.Key;
                UiKit.Button(grid, SpecDefaults.NameOf(id), () => { set(id); Refresh(); }, id == current ? Theme.Accent : Theme.PanelLight, 22);
            }
        }
    }
}
