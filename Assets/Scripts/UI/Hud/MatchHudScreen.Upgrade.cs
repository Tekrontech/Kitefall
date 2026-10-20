using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kitefall.UI.Hud
{
    /// <summary>
    /// Upgrade v1.4 HUD: kill feed rows with icons + streaks (F4), spectate bar (F5), emote wheel (F6), quick chat + push-to-talk (F7),
    /// training panel (F8), rooftop map / roof hopping (F9), launch ritual mini-games (F10), camera button (W2), warmup banner (F3).
    /// Every action is sent as a command; the server decides.
    /// </summary>
    public sealed partial class MatchHudScreen
    {
        private readonly FeedBuilder _feedBuilder = new FeedBuilder();
        private RectTransform _prepRoot, _ringRoot;
        private Image _ringGold, _ringGood, _ringFill, _ringNeedle, _prepIcon, _pulse;
        private Text _prepHint, _prepSub, _prepGrade;
        private float _gradeUntil;
        private RectTransform _roofOverlay, _roofMapArea, _hopPanel, _linkRow, _spectateBar, _warmupBanner, _trainingPanel, _floaters;
        private Text _roofTitle, _roofInfo, _hopText, _specName, _specInfo, _warmupText, _practiceText, _streak;
        private Button _changeRoof, _launchHere, _leaveButton, _micButton;
        private readonly List<(Image img, int roof)> _roofTiles = new List<(Image, int)>();
        private readonly List<(RectTransform rt, float t)> _floatItems = new List<(RectTransform, float)>();
        private int _linksBuiltFor = -2;
        private int _selectedRoof = -1;
        private float _streakUntil;
        private GameObject _wheel;
        private int _contestTicks, _sweetTicks;

        private void ClearUpgrade()
        {
            _roofTiles.Clear();
            _floatItems.Clear();
            _linksBuiltFor = -2;
            _wheel = null;
        }

        private void BuildUpgrade()
        {
            _floaters = UiKit.Rect("Floaters", Root).Stretch();

            // Camera toggle (W2) next to the menu button.
            Button cam = IconButton(Root, "camera", () => Game.Matches.Presenter.ToggleCamera(), new Color(0, 0, 0, 0.5f));
            cam.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(132, -24), new Vector2(96, 76));

            // Emote wheel + quick chat + push-to-talk on the right edge.
            Button emote = IconButton(Root, "emote", null, new Color(0, 0, 0, 0.5f));
            emote.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 150), new Vector2(96, 96));
            emote.onClick.RemoveAllListeners();
            emote.gameObject.AddComponent<EmoteWheelButton>().Init(this);
            Button chat = IconButton(Root, "chat", OpenQuickChat, new Color(0, 0, 0, 0.5f));
            chat.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 40), new Vector2(96, 96));
            if (VoiceRegistry.Available != null && VoiceRegistry.Available())
            {
                _micButton = IconButton(Root, "mic", null, new Color(0, 0, 0, 0.5f));
                _micButton.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, -70), new Vector2(96, 96));
                _micButton.gameObject.AddComponent<PushToTalk>();
            }

            _streak = UiKit.Label(Root, "", 58, Theme.Coin, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _streak.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 80));

            BuildPrep();
            BuildRoofOverlay();
            BuildHop();
            BuildSpectate();
            BuildWarmup();
            if (S.Setup.Rules.mode == GameMode.Training) BuildTraining();
        }

        private static Button IconButton(Transform parent, string icon, System.Action onClick, Color bg)
        {
            Button b = UiKit.Button(parent, "", onClick ?? (() => { }), bg, 20, icon);
            Image img = UiKit.Image(b.transform, Color.white, Icons.Get(icon), "Icon");
            img.rectTransform.Stretch(16, 12, 16, 12);
            img.preserveAspect = true;
            return b;
        }

        // =====================================================================
        // Launch ritual (F10) – icons + one-word hints, no long text
        // =====================================================================

        private void BuildPrep()
        {
            _prepRoot = UiKit.Rect("Prep", Root);
            _prepRoot.Place(new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 360));
            _ringRoot = UiKit.Rect("Ring", _prepRoot);
            _ringRoot.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(230, 230));
            UiKit.Image(_ringRoot, new Color(1, 1, 1, 0.18f), Sprites.Ring, "Track").rectTransform.Stretch();
            _ringGood = UiKit.Image(_ringRoot, new Color(0.45f, 0.95f, 0.55f, 0.85f), Sprites.Ring, "Good");
            _ringGood.rectTransform.Stretch();
            _ringGood.type = Image.Type.Filled; _ringGood.fillMethod = Image.FillMethod.Radial360; _ringGood.fillOrigin = 2;
            _ringGold = UiKit.Image(_ringRoot, Theme.Coin, Sprites.Ring, "Gold");
            _ringGold.rectTransform.Stretch();
            _ringGold.type = Image.Type.Filled; _ringGold.fillMethod = Image.FillMethod.Radial360; _ringGold.fillOrigin = 2;
            _ringFill = UiKit.Image(_ringRoot, new Color(1, 1, 1, 0.9f), Sprites.Ring, "Fill");
            _ringFill.rectTransform.Stretch(12, 12, 12, 12);
            _ringFill.type = Image.Type.Filled; _ringFill.fillMethod = Image.FillMethod.Radial360; _ringFill.fillOrigin = 2; _ringFill.fillClockwise = true;
            _ringNeedle = UiKit.Image(_ringRoot, Color.white, Sprites.White, "Needle");
            _ringNeedle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(8, 112));
            _pulse = UiKit.Image(_ringRoot, new Color(1f, 0.85f, 0.3f, 0.9f), Sprites.Circle, "Pulse");
            _pulse.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            _prepIcon = UiKit.Image(_ringRoot, Color.white, Icons.Get("kite"), "Icon");
            _prepIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
            _prepIcon.preserveAspect = true;
            _prepHint = UiKit.Label(_prepRoot, "", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _prepHint.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 64));
            _prepSub = UiKit.Label(_prepRoot, "", 24, Theme.TextDim, TextAnchor.MiddleCenter).Shadowed();
            _prepSub.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -28), new Vector2(0, 8));
            _prepGrade = UiKit.Label(Root, "", 64, Theme.Coin, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _prepGrade.rectTransform.Place(new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 90));
        }

        private void TickPrep(MatchSnapshot snap, in KiteSnap k)
        {
            bool show = k.Flyer == FlyerState.Prep;
            _prepRoot.gameObject.SetActive(show);
            if (Time.unscaledTime > _gradeUntil) _prepGrade.text = "";
            if (!show) return;
            PrepSettings ps = S.Setup.Prep ?? new PrepSettings();
            float now = snap.Time + S.Alpha * MatchSimulation.Dt;
            float age = Mathf.Max(0f, now - k.PrepStepStart);
            bool blocked = now < k.PrepBlockedUntil;
            float total = Mathf.Max(0f, k.PrepStart + (k.PrepFull ? ps.fullCap : ps.shortCap) - now);
            _ringGold.gameObject.SetActive(false);
            _ringGood.gameObject.SetActive(false);
            _ringFill.gameObject.SetActive(false);
            _ringNeedle.gameObject.SetActive(false);
            _pulse.gameObject.SetActive(false);
            _prepSub.text = $"{total:0}s";
            switch (k.PrepStep)
            {
                case PrepStep.PickKite:
                    Hint("kite", "READY", "");
                    break;
                case PrepStep.TieKanni:
                {
                    // Timing ring: the needle sweeps; tap when it is in the golden arc (2 taps).
                    Hint("kite", "TAP!", $"kanni {k.PrepTaps}/{ps.kanniTaps}");
                    float t = age / ps.kanniPeriod;
                    float f = t - Mathf.Floor(t);
                    float needle = f < 0.5f ? f * 2f : 2f - f * 2f;
                    const float sweep = 240f;
                    ShowArc(_ringGood, ps.kanniGoodHalf * 2f * sweep / 360f);
                    ShowArc(_ringGold, ps.kanniGoldenHalf * 2f * sweep / 360f);
                    _ringNeedle.gameObject.SetActive(true);
                    _ringNeedle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (0.5f - needle) * sweep);
                    _prepIcon.gameObject.SetActive(false);
                    break;
                }
                case PrepStep.TieLine:
                    Hint("check", "TIE", "");
                    break;
                case PrepStep.Toss:
                {
                    // Hold PULL, release when the ring is in gold (same window as the old perfect launch).
                    Hint("launch", blocked ? "AGAIN" : "HOLD · LET GO", "");
                    float max = ps.tossOverhold;
                    _ringFill.gameObject.SetActive(true);
                    _ringFill.fillAmount = Mathf.Clamp01(k.PrepTossHold / max);
                    ShowArcRange(_ringGood, ps.tossGoodMin / max, ps.tossGoodMax / max);
                    ShowArcRange(_ringGold, ps.tossPerfectMin / max, ps.tossPerfectMax / max);
                    break;
                }
                case PrepStep.Tunke:
                {
                    // Rhythm: tug when the nose points up – the pulse peaks at that moment.
                    Hint("arrow_up", "TUG!", $"{k.PrepTugs}/{(k.PrepFull ? ps.tunkeCountFull : ps.tunkeCountShort)}");
                    float t = age / ps.tunkePeriod;
                    float phase = t - Mathf.Floor(t);
                    float closeness = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.25f) / Mathf.Max(0.01f, ps.tunkeWindowHalf));
                    _pulse.gameObject.SetActive(true);
                    _pulse.rectTransform.localScale = Vector3.one * (0.7f + closeness * 0.9f);
                    _pulse.color = Color.Lerp(new Color(1, 1, 1, 0.25f), Theme.Coin, closeness);
                    break;
                }
                case PrepStep.Climb:
                    Hint("dheel", Terms.ReleaseUpper, "");
                    break;
                case PrepStep.Untangle:
                    Hint("snap", "SWIPE ◀ ▶", $"{k.PrepSwipes}/{ps.untangleSwipes}");
                    break;
            }
        }

        private void Hint(string icon, string word, string sub)
        {
            _prepIcon.gameObject.SetActive(true);
            _prepIcon.sprite = Icons.Get(icon);
            _prepHint.text = word;
            if (!string.IsNullOrEmpty(sub)) _prepSub.text = sub + "   " + _prepSub.text;
        }

        private static void ShowArc(Image arc, float fill)
        {
            arc.gameObject.SetActive(true);
            arc.fillAmount = fill;
            arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -fill * 180f);
        }

        /// <summary>Arc from fraction a to b of a full turn, starting at the top, clockwise.</summary>
        private static void ShowArcRange(Image arc, float a, float b)
        {
            arc.gameObject.SetActive(true);
            arc.fillClockwise = true;
            arc.fillAmount = Mathf.Clamp01(b - a);
            arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -a * 360f);
        }

        private void PrepGradePopup(PrepEventType type)
        {
            string text = null;
            Color c = Theme.Coin;
            switch (type)
            {
                case PrepEventType.KanniTap: break;
                case PrepEventType.KanniTied: text = "KANNI TIED"; c = Color.white; break;
                case PrepEventType.TossPerfect: text = "PERFECT!"; break;
                case PrepEventType.TossGood: text = "GOOD"; c = Theme.Good; break;
                case PrepEventType.TossMiss: text = "MISS – AGAIN"; c = Theme.Bad; break;
                case PrepEventType.TugGood: text = "+4 m"; c = Theme.Good; break;
                case PrepEventType.TugBad: text = "−2 m"; c = Theme.Bad; break;
                case PrepEventType.Snagged: text = "SNAGGED!"; c = Theme.Warn; break;
                case PrepEventType.Untangled: text = "FREE"; c = Theme.Good; break;
                case PrepEventType.Finished: text = "UP!"; c = Color.white; break;
            }
            if (text == null) return;
            _prepGrade.text = text;
            _prepGrade.color = c;
            _gradeUntil = Time.unscaledTime + 1.1f;
        }

        // =====================================================================
        // Rooftop selection map (F9)
        // =====================================================================

        private void BuildRoofOverlay()
        {
            Image bg = UiKit.Panel(Root, new Color(0.02f, 0.05f, 0.1f, 0.55f), "RoofSelect", false);
            _roofOverlay = bg.rectTransform;
            _roofOverlay.Stretch();
            _roofTitle = UiKit.Label(_roofOverlay, "", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _roofTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1400, 70));
            _roofMapArea = UiKit.Rect("Map", _roofOverlay);
            _roofMapArea.Place(new Vector2(0.42f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 700));
            Image info = UiKit.Panel(_roofOverlay, Theme.Panel, "RoofInfo", false);
            info.rectTransform.Place(new Vector2(1, 0.45f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(520, 520));
            // Roof hopping illustration (onboard_04_roof), heavily shaded behind the roof info text, when installed.
            Sprite roofArt = Art.Sprite("onboard_04_roof");
            if (roofArt != null) Screens.MenuScreen.ArtFill(info.transform, roofArt, 4f, 0.7f, info.sprite);
            _roofInfo = UiKit.Label(info.transform, "", 26, Color.white, TextAnchor.UpperLeft);
            _roofInfo.rectTransform.Stretch(22, 22, 22, 22);
            _roofOverlay.gameObject.SetActive(false);
            if (S.Arena.Roofs != null) BuildRoofMap(_roofMapArea, S.Arena.Roofs, true);
        }

        private void BuildRoofMap(RectTransform area, RoofGraph g, bool interactive)
        {
            Vector2 size = area.sizeDelta;
            float w = g.MaxX - g.MinX, d = g.MaxZ - g.MinZ;
            float scale = Mathf.Min((size.x - 60f) / Mathf.Max(1f, w), (size.y - 60f) / Mathf.Max(1f, d));
            Vector2 Map(System.Numerics.Vector3 p) => new Vector2((p.X - (g.MinX + g.MaxX) * 0.5f) * scale, (p.Z - (g.MinZ + g.MaxZ) * 0.5f) * scale);
            foreach (RoofLink l in g.Links)
            {
                Vector2 a = Map(l.PointA), b = Map(l.PointB);
                Image line = UiKit.Image(area, new Color(0.85f, 0.75f, 0.55f, 0.85f), Sprites.White, "Link");
                RectTransform rt = line.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = (a + b) * 0.5f;
                rt.sizeDelta = new Vector2(Mathf.Max(6f, (b - a).magnitude), 6f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                // Path marker at the middle of the link (only with the painted icons; the plain line stays otherwise).
                Sprite kindArt = Art.ForLink(l.Kind);
                if (kindArt != null)
                {
                    Image mark = UiKit.Image(area, Color.white, kindArt, "LinkIcon");
                    mark.preserveAspect = true;
                    mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    mark.rectTransform.anchoredPosition = (a + b) * 0.5f;
                    mark.rectTransform.sizeDelta = new Vector2(30f, 30f);
                }
            }
            foreach (RoofNode r in g.Roofs)
            {
                int id = r.Id;
                Image tile = UiKit.Image(area, Color.white, Sprites.RoundedSmall, "Roof" + id, interactive);
                RectTransform rt = tile.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Map(r.Center);
                rt.sizeDelta = new Vector2(r.SizeX * scale, r.SizeZ * scale);
                if (interactive)
                {
                    Button b = tile.gameObject.AddComponent<Button>();
                    b.onClick.AddListener(() => { _selectedRoof = id; S.SendCommand(PlayerCommand.ClaimRoof, id); Game.Audio?.Play(Sfx.Click); });
                }
                // Height shading and trait icons.
                Text h = UiKit.Label(rt, $"{r.Height:0}m", 18, new Color(0, 0, 0, 0.75f), TextAnchor.LowerCenter, FontStyle.Bold);
                h.rectTransform.Stretch(2, 2, 2, 2);
                RectTransform icons = UiKit.Row(rt, 2, 0, TextAnchor.UpperCenter);
                icons.Stretch(2, 2, 2, 2);
                foreach ((RoofTrait t, string icon) in TraitIcons)
                    if (r.Has(t)) UiKit.Image(icons, new Color(0.1f, 0.1f, 0.12f, 0.9f), Icons.Get(icon), "Trait").Size(24, 24);
                _roofTiles.Add((tile, id));
            }
            // Wind arrow: the wind blows toward +Z (up on the map).
            Image wind = UiKit.Image(area, new Color(1, 1, 1, 0.7f), Icons.Get("arrow_up"), "Wind");
            wind.rectTransform.Place(new Vector2(0.03f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            wind.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -S.Current.WindDirection);
        }

        private static readonly (RoofTrait, string)[] TraitIcons =
        {
            (RoofTrait.High, "arrow_up"), (RoofTrait.Sheltered, "defend"), (RoofTrait.Upwind, "wind"),
            (RoofTrait.CrateSpot, "crate"), (RoofTrait.Edge, "flag"), (RoofTrait.Cables, "snap")
        };

        public static string TraitText(RoofNode r, RoofSettings s)
        {
            if (r == null) return "";
            var lines = new List<string> { $"<b>Roof {r.Id + 1}</b> · {r.Height:0} m high" };
            if (r.Traits == RoofTrait.None) lines.Add("No special traits – balanced spot.");
            if (r.Has(RoofTrait.High)) lines.Add($"<color=#7CF7FF>High</color>: start +{s.highStartBonus:0} m · gusts ×{s.highGustMultiplier:0.00}, everyone sees you on radar");
            if (r.Has(RoofTrait.Sheltered)) lines.Add($"<color=#7CF7FF>Sheltered</color>: gusts ×{s.shelteredGustMultiplier:0.0}, toss never fails · start −{s.shelteredStartPenalty:0} m, walls nearby");
            if (r.Has(RoofTrait.Upwind)) lines.Add($"<color=#7CF7FF>Upwind</color>: wind +{s.upwindWindBonus * 100f:0}% in contests for {s.upwindBonusDuration:0}s · enemies notice you earlier");
            if (r.Has(RoofTrait.CrateSpot)) lines.Add($"<color=#7CF7FF>Crate spot</color>: first crate lands within {s.crateSpotRadius:0} m · everyone knows it");
            if (r.Has(RoofTrait.Edge)) lines.Add("<color=#7CF7FF>Edge</color>: calmer start · the storm reaches it first");
            if (r.Has(RoofTrait.Cables)) lines.Add("<color=#FFB347>Cables</color>: lines can snag during the launch (untangle)");
            return string.Join("\n\n", lines);
        }

        private void TickRoofSelect(MatchSnapshot snap, int me)
        {
            bool show = snap.Phase == MatchPhase.RoofSelect && S.Arena.Roofs != null;
            _roofOverlay.gameObject.SetActive(show);
            if (!show) return;
            RoofGraph g = S.Arena.Roofs;
            int mine = snap.Kites[me].RoofId;
            _roofTitle.text = $"Choose your rooftop  {snap.PhaseTimeLeft:0}s";
            bool teamMode = S.Setup.Rules.teamCount > 1;
            foreach ((Image img, int roof) in _roofTiles)
            {
                if (img == null) continue;
                int owner = -1, count = 0;
                bool friend = false;
                for (int i = 0; i < snap.Kites.Length; i++)
                {
                    if (snap.Kites[i].RoofId != roof || i == me) continue;
                    owner = i; count++;
                    if (teamMode && _teams[i] == _teams[me] || !string.IsNullOrEmpty(S.Setup.Participants[i].partyId) && S.Setup.Participants[i].partyId == S.Setup.Participants[me].partyId) friend = true;
                }
                Color c = roof == mine ? Theme.Accent : friend ? new Color(0.35f, 0.6f, 1f) : owner >= 0 ? new Color(0.35f, 0.35f, 0.4f) : new Color(0.92f, 0.9f, 0.85f);
                if (S.Setup.Rules.roofSelect == RoofSelectMode.DuelPairs && owner < 0 && roof != mine)
                {
                    // Only the offered mirrored pairs can be chosen.
                    bool offered = S.Setup.Rules.roofSelect == RoofSelectMode.DuelPairs && IsOfferedPair(g, roof);
                    if (!offered) c = new Color(0.5f, 0.5f, 0.55f, 0.6f);
                }
                img.color = c;
            }
            _roofInfo.text = TraitText(_selectedRoof >= 0 ? g.Roof(_selectedRoof) : mine >= 0 ? g.Roof(mine) : null, S.Setup.Roofs ?? new RoofSettings())
                + (mine >= 0 ? "" : "\n\n<color=#FFD24D>Tap a roof to claim it.</color>");
        }

        private RoofClaims _clientClaims;

        private bool IsOfferedPair(RoofGraph g, int roof)
        {
            if (_clientClaims == null)
            {
                var teams = new List<int>(_teams);
                _clientClaims = new RoofClaims(g, RoofSelectMode.DuelPairs, teams, S.Setup.Seed);
            }
            return _clientClaims.OfferedPairs.Contains(roof) || _clientClaims.OfferedPairs.Contains(g.Roof(roof).MirrorOf);
        }

        // =====================================================================
        // Roof hopping (F9)
        // =====================================================================

        private void BuildHop()
        {
            _hopPanel = UiKit.Rect("Hop", Root);
            _hopPanel.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1300, 240));
            _hopText = UiKit.Label(_hopPanel, "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _hopText.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), Vector2.zero);
            _linkRow = UiKit.Row(_hopPanel, 14, 0, TextAnchor.MiddleCenter);
            _linkRow.Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 90), new Vector2(0, 186));
            _launchHere = UiKit.Button(_hopPanel, "LAUNCH HERE", () => S.SendCommand(PlayerCommand.LaunchHere, 0), Theme.Good, 32);
            _launchHere.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(420, 84));
            _changeRoof = IconButton(Root, "packup", () => S.SendCommand(PlayerCommand.PackUp, 0), new Color(0.1f, 0.35f, 0.3f, 0.85f));
            _changeRoof.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-470, 380), new Vector2(120, 120));
            Text cl = UiKit.Label(_changeRoof.transform, "ROOF", 18, Color.white, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
            cl.rectTransform.Stretch(0, 0, 0, -26);
        }

        private void TickHop(MatchSnapshot snap, in KiteSnap k, int me)
        {
            bool hopping = S.Arena.Roofs != null && S.Setup.Rules.roofHopping && !S.Setup.FixedRoofs && snap.Phase == MatchPhase.Round;
            _changeRoof.gameObject.SetActive(hopping && k.Flyer == FlyerState.Flying && !k.InContest);
            bool panel = hopping && (k.Flyer == FlyerState.Grounded || k.Flyer == FlyerState.Traversing || k.Flyer == FlyerState.PackingUp);
            _hopPanel.gameObject.SetActive(panel);
            if (!panel) { _linksBuiltFor = -2; return; }
            RoofGraph g = S.Arena.Roofs;
            switch (k.Flyer)
            {
                case FlyerState.PackingUp:
                    _hopText.text = $"Packing up… {(int)(k.PackUp01 * 100f)}%   <size=22>(you can still be contacted)</size>";
                    _launchHere.gameObject.SetActive(false);
                    ClearLinks();
                    break;
                case FlyerState.Traversing:
                    string kind = k.TraverseLink >= 0 ? g.Link(k.TraverseLink).Kind.ToString().ToLower() : "";
                    _hopText.text = $"Crossing the {kind}… {(int)(k.TraverseProgress * 100f)}%";
                    _launchHere.gameObject.SetActive(false);
                    ClearLinks();
                    break;
                default:
                    float left = Mathf.Max(0f, (S.Setup.Roofs?.groundedAutoPrep ?? 25f) - k.GroundedTime);
                    _hopText.text = $"On roof {k.RoofId + 1} – pick a way across or launch.  <size=24>Auto launch in {left:0}s</size>";
                    _launchHere.gameObject.SetActive(true);
                    if (_linksBuiltFor != k.RoofId) BuildLinks(g, k.RoofId);
                    UpdateLinkStates(snap, g, me);
                    break;
            }
        }

        private readonly List<(Button b, int dest)> _linkButtons = new List<(Button, int)>();

        private void ClearLinks()
        {
            UiKit.Clear(_linkRow);
            _linkButtons.Clear();
            _linksBuiltFor = -2;
        }

        private void BuildLinks(RoofGraph g, int roof)
        {
            ClearLinks();
            _linksBuiltFor = roof;
            if (roof < 0) return;
            float camYaw = Game.Matches.Camera != null ? Game.Matches.Camera.transform.eulerAngles.y : 0f;
            foreach (int l in g.Roof(roof).Links)
            {
                RoofLink link = g.Link(l);
                int dest = link.Other(roof);
                int id = l;
                Button b = UiKit.Button(_linkRow, "", () => S.SendCommand(PlayerCommand.TraverseLink, id), new Color(0.12f, 0.18f, 0.3f, 0.92f), 22);
                b.Size(230, 92);
                RectTransform row = UiKit.Row(b.transform, 8, 8, TextAnchor.MiddleCenter);
                row.Stretch();
                // Arrow points where the roof is, relative to the camera.
                System.Numerics.Vector3 from = g.Roof(roof).Center, to = g.Roof(dest).Center;
                float yaw = Mathf.Atan2(to.X - from.X, to.Z - from.Z) * Mathf.Rad2Deg - camYaw;
                Image arrow = UiKit.Image(row, Color.white, Icons.Get("arrow_up"), "Dir");
                arrow.Size(40, 40);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -yaw);
                // Painted path icon (link_plank …) when installed; the gold code-drawn glyph otherwise.
                Sprite kindArt = Art.ForLink(link.Kind);
                Image kind = UiKit.Image(row, kindArt != null ? Color.white : Theme.Coin, kindArt ?? Icons.Get(KindIcon(link.Kind)), "Kind");
                kind.preserveAspect = true;
                kind.Size(40, 40);
                UiKit.Label(row, $"{link.TravelTime:0}s", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Size(70, 40);
                _linkButtons.Add((b, dest));
            }
        }

        private void UpdateLinkStates(MatchSnapshot snap, RoofGraph g, int me)
        {
            foreach ((Button b, int dest) in _linkButtons)
            {
                bool free = true;
                for (int i = 0; i < snap.Kites.Length; i++)
                {
                    if (i == me || snap.Kites[i].Eliminated || snap.Kites[i].RoofId != dest) continue;
                    if (S.Setup.Rules.teamCount > 1 && _teams[i] == _teams[me]) continue;
                    free = false;
                }
                b.interactable = free;
            }
        }

        private static string KindIcon(RoofLinkKind k)
        {
            switch (k)
            {
                case RoofLinkKind.Ladder: return "ladder";
                case RoofLinkKind.RopeBridge: return "bridge";
                case RoofLinkKind.Stairs: return "stairs";
                case RoofLinkKind.Jump: return "jump";
                default: return "plank";
            }
        }

        // =====================================================================
        // Spectate (F5)
        // =====================================================================

        private void BuildSpectate()
        {
            Image bar = UiKit.Panel(Root, new Color(0, 0, 0, 0.55f), "Spectate", false);
            _spectateBar = bar.rectTransform;
            _spectateBar.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1100, 110));
            Button l = UiKit.Button(_spectateBar, "◀", () => Game.Matches.Presenter.CycleSpectate(-1), Theme.PanelLight, 40);
            l.GetComponent<RectTransform>().Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(90, 90));
            Button r = UiKit.Button(_spectateBar, "▶", () => Game.Matches.Presenter.CycleSpectate(1), Theme.PanelLight, 40);
            r.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(90, 90));
            _specName = UiKit.Label(_spectateBar, "", 32, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            _specName.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(110, -56), new Vector2(-110, -8));
            _specInfo = UiKit.Label(_spectateBar, "", 22, Theme.TextDim, TextAnchor.LowerCenter);
            _specInfo.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(110, 8), new Vector2(-110, 50));
            _leaveButton = UiKit.Button(Root, "Leave match (keep rewards)", () => Game.Flow.LeaveAfterElimination(), Theme.Bad, 26);
            _leaveButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(520, 84));
            // Reactions (no text): thumbs up and fire float up for everyone.
            Button thumbs = IconButton(_spectateBar, "thumbs", () => S.SendCommand(PlayerCommand.Emote, 200), Theme.PanelLight);
            thumbs.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-110, -6), new Vector2(80, 70));
            Button fire = IconButton(_spectateBar, "fire", () => S.SendCommand(PlayerCommand.Emote, 201), Theme.PanelLight);
            fire.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-200, -6), new Vector2(80, 70));
        }

        private void TickSpectate(MatchSnapshot snap, in KiteSnap k, int me)
        {
            int target = Game.Matches.Presenter.SpectateTarget;
            bool show = target >= 0 && target < snap.Kites.Length && SpectateRules.CanSpectate(snap, me);
            _spectateBar.gameObject.SetActive(show);
            _leaveButton.gameObject.SetActive(show && k.Eliminated && S.Setup.Rules.stormZone && !S.Setup.Rules.respawn);
            if (!show) return;
            ParticipantSetup p = S.Setup.Participants[target];
            ParticipantLoadout l = p.stack.Count > 0 ? p.stack[Mathf.Min(snap.Kites[target].StackIndex, p.stack.Count - 1)] : S.Setup.FreeGuddi;
            _specName.text = $"Spectating {p.displayName}{(p.isBot ? " <size=20>[BOT]</size>" : "")}";
            _specInfo.text = $"{SpecDefaults.NameOf(l.kiteId)} · {SpecDefaults.NameOf(l.dorId)} · cuts {snap.Kites[target].Cuts}" + (k.RespawnIn > 0f ? $"   ·   next kite in {k.RespawnIn:0}s" : "");
        }

        // =====================================================================
        // Warmup (F3)
        // =====================================================================

        private void BuildWarmup()
        {
            Image b = UiKit.Panel(Root, new Color(0, 0, 0, 0.5f), "Warmup", false);
            _warmupBanner = b.rectTransform;
            _warmupBanner.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 80));
            _warmupText = UiKit.Label(_warmupBanner, "", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _warmupText.rectTransform.Stretch();
        }

        private void TickWarmup(MatchSnapshot snap)
        {
            bool show = snap.Phase == MatchPhase.Warmup;
            _warmupBanner.gameObject.SetActive(show);
            if (!show) return;
            int connected = 0;
            foreach (KiteSnap x in snap.Kites) if (x.Connected) connected++;
            int t = Mathf.CeilToInt(snap.PhaseTimeLeft);
            _warmupText.text = $"Match starts in {t / 60}:{t % 60:00} · {connected}/{S.Setup.Rules.maxPlayers} players";
        }

        // =====================================================================
        // Training (F8)
        // =====================================================================

        private void BuildTraining()
        {
            var off = S as OfflineMatchSession;
            if (off == null || off.Training == null) return;
            TrainingOptions t = off.Training;
            Image panel = UiKit.Panel(Root, Theme.Panel, "Training", true);
            _trainingPanel = panel.rectTransform;
            _trainingPanel.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 60), new Vector2(470, 560));
            RectTransform col = UiKit.Column(_trainingPanel, 8, 16);
            col.Stretch();
            UiKit.Label(col, "TRAINING", 30, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, 40);
            UiKit.Toggle(col, "Control the wind", t.overrideWind, v => t.overrideWind = v).Size(-1, 56);
            Text dir = null, speed = null;
            dir = UiKit.Stepper(col, "Direction", $"{t.windDirectionDeg:0}°",
                () => { t.windDirectionDeg = Mathf.Repeat(t.windDirectionDeg - 15f, 360f); dir.text = $"{t.windDirectionDeg:0}°"; },
                () => { t.windDirectionDeg = Mathf.Repeat(t.windDirectionDeg + 15f, 360f); dir.text = $"{t.windDirectionDeg:0}°"; });
            speed = UiKit.Stepper(col, "Speed", $"{t.windSpeed:0} m/s",
                () => { t.windSpeed = Mathf.Max(2f, t.windSpeed - 1f); speed.text = $"{t.windSpeed:0} m/s"; },
                () => { t.windSpeed = Mathf.Min(14f, t.windSpeed + 1f); speed.text = $"{t.windSpeed:0} m/s"; });
            UiKit.Toggle(col, "Gusts", t.gusts, v => t.gusts = v).Size(-1, 56);
            UiKit.Choice(col, "Phase", new[] { "Auto", "Calm", "Gust", "Storm" }, t.forcedPhase + 1, i => t.forcedPhase = i - 1);
            _practiceText = UiKit.Label(col, "", 22, Color.white, TextAnchor.UpperLeft);
            _practiceText.Size(-1, 150);
            UiKit.Button(col, "Hide panel", () => _trainingPanel.gameObject.SetActive(false), Theme.PanelLight, 22).Size(-1, 56);
        }

        private void TickTraining(MatchSnapshot snap, in KiteSnap k, int me)
        {
            if (_practiceText == null) return;
            if (k.InContest)
            {
                _contestTicks++;
                if (k.SweetCenter >= 0f && Mathf.Abs(k.Tension - k.SweetCenter) <= k.SweetWidth * 0.5f) _sweetTicks++;
            }
            string sweet = _contestTicks > 0 ? $"Sweet zone: {100f * _sweetTicks / _contestTicks:0}%" : "Sweet zone: –";
            var sim = (S as OfflineMatchSession)?.Simulation;
            string last = "";
            if (sim != null)
            {
                for (int i = sim.ContestHistory.Count - 1; i >= 0; i--)
                {
                    ContestSummary c = sim.ContestHistory[i];
                    if (!c.Involves(me)) continue;
                    var factors = ResultsExplainer.Factors(c, me);
                    last = "Last contest: " + (c.Winner == me ? "<color=#3BB273>won</color>" : c.Loser == me ? "<color=#E5484D>lost</color>" : "even") + "\n";
                    for (int f = 0; f < Mathf.Min(3, factors.Count); f++)
                        last += $"{factors[f].Name} ×{factors[f].Ratio:0.00}  ";
                    break;
                }
            }
            _practiceText.text = sweet + "\n" + last;
        }

        // =====================================================================
        // Kill feed (F4), quick chat, emotes, floaters
        // =====================================================================

        /// <summary>Kill-feed row from a match event (no new network message – built from existing events).</summary>
        public void FeedEvent(MatchEvent e)
        {
            if (S == null || _feed == null) return;
            if (!_feedBuilder.TryBuild(e, Time.unscaledTime, DorOf, i => i >= 0 && i < _teams.Length ? _teams[i] : -1, out FeedEntry entry, out int streak)) return;
            FeedRow(entry);
            if (streak >= 2)
            {
                _streak.text = (entry.Attacker == S.LocalIndex ? "" : S.Setup.Participants[entry.Attacker].displayName + ": ") + FeedBuilder.StreakText(streak);
                _streakUntil = Time.unscaledTime + 2.2f;
            }
        }

        private string DorOf(int i)
        {
            ParticipantSetup p = S.Setup.Participants[i];
            int stack = i < S.Current.Kites.Length ? S.Current.Kites[i].StackIndex : 0;
            return p.stack.Count > 0 ? p.stack[Mathf.Min(stack, p.stack.Count - 1)].dorId : "";
        }

        private void FeedRow(in FeedEntry e)
        {
            int me = S.LocalIndex;
            bool involvesMe = e.Involves(me);
            Image row = UiKit.Panel(_feed, involvesMe ? new Color(1f, 0.6f, 0.15f, 0.28f) : new Color(0, 0, 0, 0.35f), "FeedRow", false);
            row.Size(-1, 44);
            RectTransform r = UiKit.Row(row.transform, 8, 6, TextAnchor.MiddleRight);
            r.Stretch();
            if (e.Attacker >= 0)
            {
                Avatar(r, e.Attacker, 34);
                UiKit.Label(r, NameOf(e.Attacker), 22, NameColor(e.Attacker), TextAnchor.MiddleRight, FontStyle.Bold).Size(150, 40);
            }
            switch (e.Kind)
            {
                case FeedKind.Cut:
                    UiKit.Image(r, Color.white, Icons.Get(AttackIcon(e.Attack)), "Attack").Size(30, 30);
                    UiKit.Image(r, DorTint(e.DorId), Icons.Get("cut"), "Dor").Size(30, 30);
                    break;
                case FeedKind.Assist: UiKit.Image(r, Theme.Good, Icons.Get("assist"), "Assist").Size(28, 28); break;
                case FeedKind.SelfSnap: UiKit.Image(r, Theme.Warn, Icons.Get("snap"), "Snap").Size(30, 30); break;
                case FeedKind.Crash: UiKit.Image(r, Theme.Bad, Icons.Get("crash"), "Crash").Size(28, 28); break;
                case FeedKind.CrateTaken: UiKit.Image(r, Theme.Coin, Icons.Get("crate"), "Crate").Size(30, 30); break;
                case FeedKind.Eliminated: UiKit.Image(r, Theme.Bad, Icons.Get("storm"), "Out").Size(30, 30); break;
            }
            if (e.Victim >= 0)
            {
                string suffix = e.Kind == FeedKind.SelfSnap ? " overpulled" : e.Kind == FeedKind.Crash ? " crashed" : e.Kind == FeedKind.Eliminated ? " is out" : "";
                UiKit.Label(r, NameOf(e.Victim) + suffix, 22, NameColor(e.Victim), TextAnchor.MiddleLeft, FontStyle.Bold).Size(200, 40);
                Avatar(r, e.Victim, 34);
            }
            else if (e.Kind == FeedKind.CrateTaken)
                UiKit.Label(r, SkyCrate.DisplayName(e.Crate), 22, Theme.Coin, TextAnchor.MiddleLeft).Size(200, 40);
            _feedItems.Add((row.gameObject, Time.unscaledTime + 5f));
            if (_feedItems.Count > 5)
            {
                if (_feedItems[0].go != null) Object.Destroy(_feedItems[0].go);
                _feedItems.RemoveAt(0);
            }
        }

        private string NameOf(int i) => i == S.LocalIndex ? "You" : S.Setup.Participants[i].displayName;

        private Color NameColor(int i)
        {
            int me = S.LocalIndex;
            if (i == me) return Theme.Accent;                                   // own name orange
            if (S.Setup.Rules.teamCount > 1 && _teams[i] == _teams[me]) return new Color(0.45f, 0.7f, 1f); // teammates blue
            return new Color(1f, 0.45f, 0.45f);                                 // enemies red
        }

        private static string AttackIcon(AttackType a)
        {
            switch (a)
            {
                case AttackType.Kheench: return "kheench";
                case AttackType.Dheel: return "dheel";
                case AttackType.Dive: return "dive";
                default: return "hold";
            }
        }

        private static Color DorTint(string dorId)
        {
            switch (dorId)
            {
                case "reshmi": return new Color(1f, 0.7f, 0.8f);
                case "tez_dhaar": return new Color(1f, 0.75f, 0.4f);
                case "mazboot": return new Color(0.6f, 0.75f, 1f);
                case "lambi": return new Color(0.6f, 1f, 0.6f);
                case "lachakdaar": return new Color(0.85f, 0.75f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// A small round avatar: the player's chosen character portrait (char_&lt;id&gt;_portrait from the Image Mapper) inside a ring in
        /// their outfit colour; without the portrait art, the outfit colour with an initial.
        /// </summary>
        private void Avatar(Transform parent, int i, float size)
        {
            ParticipantSetup p = S.Setup.Participants[i];
            CosmeticItem ch = CharacterCatalog.Character(p.characterId);
            CosmeticItem o = CharacterCatalog.Outfit(string.IsNullOrEmpty(p.outfitId) ? ch.forItem : p.outfitId);
            Color c = o != null ? KiteVisualFactory.Hex(o.primaryHex) : Theme.PanelLight;
            Image img = UiKit.Image(parent, c, Sprites.Circle, "Avatar");
            img.Size(size, size);
            Sprite portrait = Art.Portrait(ch.id);
            if (portrait != null)
            {
                // Round crop: a Mask on the inner circle clips the square portrait.
                Image inner = UiKit.Image(img.transform, Color.white, Sprites.Circle, "Clip");
                inner.rectTransform.Stretch(2, 2, 2, 2);
                inner.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                Image face = UiKit.Image(inner.transform, Color.white, portrait, "Portrait");
                face.preserveAspect = true;
                face.rectTransform.Stretch();
                return;
            }
            Text t = UiKit.Label(img.transform, string.IsNullOrEmpty(p.displayName) ? "?" : p.displayName.Substring(0, 1).ToUpper(), Mathf.RoundToInt(size * 0.6f), Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
        }

        private void OpenQuickChat()
        {
            RectTransform body = UI.Popup("Quick chat", new Vector2(760, 520));
            RectTransform grid = UiKit.Grid(body, new Vector2(340, 96), new Vector2(14, 14), 2);
            grid.Stretch();
            for (int i = 0; i < QuickChat.Messages.Length; i++)
            {
                int id = i;
                Button b = UiKit.Button(grid, "   " + QuickChat.Messages[i], () => { S.SendCommand(PlayerCommand.QuickChat, id); UI.ClosePopupOf(body); }, Theme.PanelLight, 26);
                Image ic = UiKit.Image(b.transform, Theme.Coin, Icons.Get(QuickChat.Icons[i]), "Icon");
                ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(48, 48));
            }
        }

        /// <summary>Called by the emote wheel with the chosen slot.</summary>
        public void SendEmoteSlot(int slot)
        {
            PlayerProfile p = Game.Profile;
            if (p == null || slot < 0 || slot >= p.emoteSlots.Count || string.IsNullOrEmpty(p.emoteSlots[slot])) return;
            int index = CharacterCatalog.EmoteIndex(p.emoteSlots[slot]);
            if (index >= 0) S.SendCommand(PlayerCommand.Emote, index);
        }

        /// <summary>Shows / hides the 8-slot radial wheel; returns the slot under the screen point (or -1).</summary>
        public int Wheel(bool show, Vector2 screenPoint)
        {
            if (!show)
            {
                if (_wheel != null) Object.Destroy(_wheel);
                _wheel = null;
                return -1;
            }
            if (_wheel == null) _wheel = BuildWheel();
            RectTransform rt = (RectTransform)_wheel.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, null, out Vector2 local);
            int slot = -1;
            if (local.magnitude > 70f)
            {
                float ang = Mathf.Repeat(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg + 22.5f, 360f);
                slot = Mathf.FloorToInt(ang / 45f);
            }
            for (int i = 0; i < 8; i++)
            {
                Transform s = rt.Find("Slot" + i);
                if (s != null) s.GetComponent<Image>().color = i == slot ? Theme.Accent : new Color(0.1f, 0.14f, 0.24f, 0.92f);
            }
            return slot;
        }

        private GameObject BuildWheel()
        {
            RectTransform rt = UiKit.Rect("EmoteWheel", Root);
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 560));
            UiKit.Image(rt, new Color(0, 0, 0, 0.35f), Sprites.Circle, "Back").rectTransform.Stretch();
            PlayerProfile p = Game.Profile;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Image slot = UiKit.Image(rt, new Color(0.1f, 0.14f, 0.24f, 0.92f), Sprites.Circle, "Slot" + i, true);
                slot.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 200f, new Vector2(130, 130));
                int idx = i;
                Button b = slot.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { SendEmoteSlot(idx); Wheel(false, Vector2.zero); });
                string id = p != null && i < p.emoteSlots.Count ? p.emoteSlots[i] : "";
                CosmeticItem e = CharacterCatalog.EmoteItem(id);
                Text t = UiKit.Label(slot.transform, e != null ? e.name : "–", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.Stretch(6, 6, 6, 6);
            }
            return rt.gameObject;
        }

        private void Floater(string icon)
        {
            Image img = UiKit.Image(_floaters, Color.white, Icons.Get(icon), "React");
            img.rectTransform.Place(new Vector2(Random.Range(0.6f, 0.9f), 0.12f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            _floatItems.Add((img.rectTransform, Time.unscaledTime));
        }

        /// <summary>Upgrade HUD events (emote reactions etc.). Called from <see cref="AppFlow"/>.</summary>
        public void UpgradeEvent(MatchEvent e)
        {
            if (S == null) return;
            switch (e.Type)
            {
                case MatchEventType.Emote:
                    if (e.Code == 200) Floater("thumbs");
                    else if (e.Code == 201) Floater("fire");
                    break;
                case MatchEventType.Prep:
                    if (e.A == S.LocalIndex) PrepGradePopup((PrepEventType)e.Code);
                    break;
                case MatchEventType.QuickChat:
                    if (QuickChat.Valid(e.Code) && QuickChat.Visible(S.Setup.Rules.mode, _teams[e.A], _teams[S.LocalIndex]) && (Game.Settings.Data.quickChat || e.A == S.LocalIndex))
                        Feed($"{NameOf(e.A)}: {QuickChat.Messages[e.Code]}", new Color(0.8f, 0.9f, 1f));
                    break;
                case MatchEventType.RoofClaimed:
                    if (e.A == S.LocalIndex) Game.Haptics?.Light();
                    break;
                case MatchEventType.GroundedTooLong:
                    if (e.A == S.LocalIndex) Announce("LAUNCHING", "You stayed on the roof too long", 1.6f);
                    break;
                case MatchEventType.HelpLaunch:
                    if (e.B == S.LocalIndex) Feed($"{NameOf(e.A)} is helping your launch", Theme.Good);
                    break;
            }
        }

        // =====================================================================
        // Tick
        // =====================================================================

        private void TickUpgrade(MatchSnapshot snap, in KiteSnap k, int me)
        {
            TickPrep(snap, k);
            TickRoofSelect(snap, me);
            TickHop(snap, k, me);
            TickSpectate(snap, k, me);
            TickWarmup(snap);
            TickTraining(snap, k, me);
            if (Time.unscaledTime > _streakUntil) _streak.text = "";
            for (int i = _floatItems.Count - 1; i >= 0; i--)
            {
                (RectTransform rt, float t) = _floatItems[i];
                float age = Time.unscaledTime - t;
                if (rt == null || age > 2f) { if (rt != null) Object.Destroy(rt.gameObject); _floatItems.RemoveAt(i); continue; }
                rt.anchoredPosition = new Vector2(Mathf.Sin(age * 5f + i) * 20f, age * 260f);
                rt.GetComponent<Image>().color = new Color(1, 1, 1, 1f - age / 2f);
            }
            if (_micButton != null) _micButton.gameObject.SetActive(VoiceRegistry.Available != null && VoiceRegistry.Available());
            // Classic controls are hidden while walking on the waiting plaza / roofs only for the extra buttons.
        }
    }

    /// <summary>Hold the emote button → radial wheel → release on a slot (or tap the button, then tap a slot).</summary>
    public sealed class EmoteWheelButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MatchHudScreen _hud;
        private bool _dragged;
        private int _slot = -1;
        private bool _open, _closing;

        public void Init(MatchHudScreen hud) => _hud = hud;

        public void OnPointerDown(PointerEventData e)
        {
            _dragged = false;
            if (_open) { _hud.Wheel(false, Vector2.zero); _open = false; _closing = true; return; }
            _slot = _hud.Wheel(true, e.position);
            _open = true;
        }

        public void OnDrag(PointerEventData e)
        {
            _dragged = true;
            _slot = _hud.Wheel(true, e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_closing) { _closing = false; return; }
            if (_dragged)
            {
                if (_slot >= 0) _hud.SendEmoteSlot(_slot);
                _hud.Wheel(false, Vector2.zero);
                _open = false;
            }
            // A simple tap leaves the wheel open: tap a slot to send, tap the button again to close.
            else if (!_open) _hud.Wheel(false, Vector2.zero);
        }
    }

    /// <summary>Push-to-talk mic button (upgrade F7). Open mic is a setting; the voice service does the rest.</summary>
    public sealed class PushToTalk : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e) => VoiceRegistry.SetTransmit?.Invoke(true);
        public void OnPointerUp(PointerEventData e)
        {
            bool openMic = Game.Settings != null && !Game.Settings.Data.voicePushToTalk;
            if (!openMic) VoiceRegistry.SetTransmit?.Invoke(false);
        }
    }
}
