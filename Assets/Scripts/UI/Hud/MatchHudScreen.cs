using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Input;
using Kitefall.Match;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Kitefall.UI.Hud
{
    /// <summary>The in-match HUD (spec 10) + touch controls + pause menu + loadout break.</summary>
    public sealed partial class MatchHudScreen : UIScreen
    {
        public override bool Opaque => false;

        private IMatchSession S => Game.Matches?.Session;
        private TensionBarWidget _tension;
        private ContestMeterWidget _meter;
        private RadarWidget _radar;
        private CompassWidget _compass;
        private Text _score, _timer, _phase, _center, _sub, _dor, _launchText;
        private Image _scoreTintA, _scoreTintB;
        private RectTransform _feed, _tags, _launch, _launchFill, _ring;
        private GameObject _breakPanel;
        private readonly List<Text> _tagPool = new List<Text>();
        private readonly List<(GameObject go, float until)> _feedItems = new List<(GameObject, float)>();
        private readonly List<DragHandle> _handles = new List<DragHandle>();
        private float _centerUntil;
        private const float AnnounceFade = 0.4f;
        private bool _editing;
        private int[] _teams;

        protected override void Build()
        {
            // Build() also runs on Refresh (e.g. back from Settings): forget widgets of the old build.
            _tagPool.Clear();
            _feedItems.Clear();
            ClearUpgrade();
            _breakPanel = null;
            _editing = false;
            if (S == null) return;
            _teams = new int[S.Setup.Participants.Count];
            for (int i = 0; i < _teams.Length; i++) _teams[i] = S.Setup.Participants[i].team;

            BuildMarkers();
            _tags = UiKit.Rect("NameTags", Root).Stretch();
            _ring = UiKit.Image(Root, new Color(1f, 0.85f, 0.2f, 0.8f), Sprites.Ring, "ApproachRing").rectTransform;
            _ring.sizeDelta = new Vector2(150, 150);

            // Dive gesture area on the right half (behind the buttons).
            Image gesture = UiKit.Image(Root, new Color(0, 0, 0, 0.001f), Sprites.White, "DiveGesture", true);
            gesture.rectTransform.Band(new Vector2(0.5f, 0f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);
            gesture.gameObject.AddComponent<DiveGestureArea>();

            BuildTop();
            _tension = new TensionBarWidget(Root);
            _meter = new ContestMeterWidget(Root);
            _radar = new RadarWidget(Root);
            _compass = new CompassWidget(Root);

            Image dorBg = UiKit.Panel(Root, new Color(0, 0, 0, 0.45f), "DorPanel", false);
            dorBg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(760, 70));
            _dor = UiKit.Label(dorBg.transform, "", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _dor.rectTransform.Stretch(12, 4, 12, 4);

            // Fixed HUD zones (audit F1), canvas 1920×1080 reference, checked down to 20:9 (≈ 2150×966 canvas units):
            //   top centre: score 16–134 · wind compass 150–276 · banner zone 286–396 (spectate bar / warmup, or the announcement –
            //   never both: the bar waits until the announcement has faded) · announce sub-line 396–476 · streak 480–550.
            //   top left: menu + camera 24–100 · kill feed 120–370.   top right: radar 24–254 · emote/chat/mic row 266–346 ·
            //   tension bar 396 → 130 px above the bottom, right edge.   bottom: controls in the corners, line info centre.
            _feed = UiKit.Column(Root, 6, 0, TextAnchor.UpperLeft);
            _feed.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -120), new Vector2(480, 250));

            _center = UiKit.Label(Root, "", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _center.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -286), new Vector2(1000, 110));
            _center.resizeTextForBestFit = true;
            _center.resizeTextMinSize = 36;
            _center.resizeTextMaxSize = 64;
            _center.horizontalOverflow = HorizontalWrapMode.Wrap;
            _center.verticalOverflow = VerticalWrapMode.Truncate;
            _sub = UiKit.Label(Root, "", 30, Color.white, TextAnchor.UpperCenter).Shadowed();
            _sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -396), new Vector2(1000, 80));

            BuildLaunch();
            BuildControls();

            Button menu = UiKit.Button(Root, "II", OpenMenu, new Color(0, 0, 0, 0.5f), 34);
            menu.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(96, 76));
            BuildUpgrade();
        }

        private void BuildTop()
        {
            Image bg = UiKit.Panel(Root, new Color(0, 0, 0, 0.45f), "Score", false);
            bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(820, 118));
            // Team modes: white score text on blue / red tinted halves (readable for everyone, colour-blind included – the words say the team).
            _scoreTintA = UiKit.Image(bg.transform, new Color(0.25f, 0.5f, 0.95f, 0.55f), Sprites.RoundedSmall, "TintBlue");
            _scoreTintA.rectTransform.Band(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(10, -54), new Vector2(-2, -4));
            _scoreTintB = UiKit.Image(bg.transform, new Color(0.9f, 0.28f, 0.28f, 0.55f), Sprites.RoundedSmall, "TintRed");
            _scoreTintB.rectTransform.Band(new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(2, -54), new Vector2(-10, -4));
            bool teams = S.Setup.Rules.teamCount > 1 && !S.Setup.Rules.IsDuel;
            _scoreTintA.gameObject.SetActive(teams);
            _scoreTintB.gameObject.SetActive(teams);
            _score = UiKit.Label(bg.transform, "", 34, Color.white, TextAnchor.UpperCenter, FontStyle.Bold).Shadowed();
            _score.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -52), new Vector2(-16, -6));
            _timer = UiKit.Label(bg.transform, "", 26, Theme.Coin, TextAnchor.MiddleCenter, FontStyle.Bold);
            _timer.rectTransform.Band(new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(16, 6), new Vector2(0, 58));
            _phase = UiKit.Label(bg.transform, "", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _phase.rectTransform.Band(new Vector2(0.4f, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(-16, 58));
        }

        private void BuildLaunch()
        {
            Image bg = UiKit.Panel(Root, new Color(0, 0, 0, 0.5f), "Launch", false);
            _launch = bg.rectTransform;
            _launch.Place(new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 150));
            _launchText = UiKit.Label(_launch, "HOLD PULL to launch – let go in the gold zone", 30, Color.white, TextAnchor.UpperCenter, FontStyle.Bold).Shadowed();
            _launchText.rectTransform.Band(new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -60), new Vector2(-20, -12));
            Image track = UiKit.Image(_launch, new Color(1, 1, 1, 0.15f), Sprites.RoundedSmall, "Track");
            track.rectTransform.Band(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 30), new Vector2(-40, 62));
            PhaseSettings ps = S.Setup.Phases;
            float lo = ps.launchPerfectMin / ps.launchOverfill, hi = ps.launchPerfectMax / ps.launchOverfill;
            Image gold = UiKit.Image(track.transform, Theme.Coin, Sprites.White, "Perfect");
            gold.rectTransform.Band(new Vector2(lo, 0), new Vector2(hi, 1), Vector2.zero, Vector2.zero);
            Image fill = UiKit.Image(track.transform, new Color(1, 1, 1, 0.85f), Sprites.RoundedSmall, "Fill");
            _launchFill = fill.rectTransform;
            _launchFill.Band(Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        }

        // ---- Touch controls ---------------------------------------------------

        private void BuildControls()
        {
            SettingsData set = Game.Settings.Data;
            float js = set.joystickScale, bs = set.buttonScale;
            bool left = set.leftHanded;
            _handles.Clear();

            RectTransform stickBase = Control("Joystick", new Vector2(0, 0), new Vector2(250, 230), 300 * js, Sprites.Circle, new Color(0.04f, 0.06f, 0.1f, 0.38f), left);
            UiKit.Image(stickBase, new Color(0.63f, 0.75f, 0.94f, 0.45f), Sprites.Ring, "Rim").rectTransform.Stretch(-2, -2, -2, -2);
            Image knob = UiKit.Image(stickBase, Theme.Hex("#3A4A6B"), Sprites.Orb, "Knob", true);
            knob.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130 * js, 130 * js));
            knob.gameObject.SetActive(false); // set controlPath before OnEnable
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = 110f * js;
            stick.controlPath = KiteInputReader.SteerStickPath;
            knob.gameObject.SetActive(true);

            Button(Control("PULL", new Vector2(1, 0), new Vector2(-220, 210), 230 * bs, Sprites.Orb, new Color(1f, 0.66f, 0.1f, 0.96f), left), "PULL", 44 * bs, KiteInputReader.PullButtonPath, "hud_kheench");
            Button(Control("DIVE", new Vector2(1, 0), new Vector2(-470, 150), 150 * bs, Sprites.Orb, new Color(0.13f, 0.72f, 0.92f, 0.94f), left), "DIVE", 30 * bs, KiteInputReader.DiveButtonPath);
            Button(Control("REEL", new Vector2(1, 0), new Vector2(-220, 440), 150 * bs, Sprites.Orb, new Color(0.24f, 0.31f, 0.46f, 0.94f), left), "REEL", 30 * bs, KiteInputReader.ReelButtonPath);
        }

        private RectTransform Control(string id, Vector2 anchor, Vector2 pos, float size, Sprite sprite, Color color, bool mirror)
        {
            if (mirror) { anchor.x = 1f - anchor.x; pos.x = -pos.x; }
            ControlPosition custom = Game.Settings.Data.customLayout.Find(c => c.id == id + (mirror ? "_L" : ""));
            if (custom != null) pos = custom.anchoredPosition;
            Image img = UiKit.Image(Root, color, sprite, id, true);
            img.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var handle = img.gameObject.AddComponent<DragHandle>();
            string key = id + (mirror ? "_L" : "");
            handle.Moved = p =>
            {
                List<ControlPosition> layout = Game.Settings.Data.customLayout;
                ControlPosition e = layout.Find(c => c.id == key);
                if (e == null) layout.Add(new ControlPosition { id = key, anchoredPosition = p });
                else e.anchoredPosition = p;
            };
            _handles.Add(handle);
            return img.rectTransform;
        }

        private static void Button(RectTransform rt, string label, float fontSize, string path, string artKey = null)
        {
            // The artist's button icon (Image Mapper hud_* slot) above a smaller label, when installed.
            Sprite art = artKey != null ? Art.Sprite(artKey) : null;
            if (art != null)
            {
                Image ic = UiKit.Image(rt, Color.white, art, "Icon");
                ic.preserveAspect = true;
                ic.rectTransform.Band(new Vector2(0.2f, 0.34f), new Vector2(0.8f, 0.86f), Vector2.zero, Vector2.zero);
                fontSize *= 0.55f;
            }
            bool pull = label == "PULL";
            Text t = UiKit.Label(rt, label, Mathf.RoundToInt(fontSize), pull ? Theme.TextOnAccent : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.UseButtonFont(t, true);
            if (pull) UiKit.SoftShadow(t); else t.Shadowed();
            if (art != null) t.rectTransform.Band(new Vector2(0f, 0.08f), new Vector2(1f, 0.36f), Vector2.zero, Vector2.zero);
            else t.rectTransform.Stretch();
            rt.gameObject.SetActive(false); // set controlPath before OnEnable
            var b = rt.gameObject.AddComponent<OnScreenButton>();
            b.controlPath = path;
            rt.gameObject.SetActive(true);
        }

        // ---- Frame update ------------------------------------------------------

        public override void Tick()
        {
            IMatchSession s = S;
            if (s == null || _score == null) return;
            MatchSnapshot snap = s.Current;
            int me = s.LocalIndex;
            if (me < 0 || me >= snap.Kites.Length) return;
            KiteSnap k = snap.Kites[me];
            var combat = Game.Db != null ? Game.Db.combat : null;
            bool cb = Game.ColorBlind;
            float camYaw = Game.Matches.Camera != null ? Game.Matches.Camera.transform.eulerAngles.y : 0f;

            _tension.Update(k, combat != null ? combat.safeZoneMax : 60f, combat != null ? combat.strongZoneMax : 85f, cb, Game.Settings.Data.sweetZoneHighlight);
            _meter.Update(snap.ContestOf(me), me, Game.Settings.Data.contestMeterStyle == 1);
            _radar.Update(snap, me, camYaw, _teams);
            _compass.Update(snap, camYaw);
            UpdateScore(snap, me);
            UpdatePhase(snap);
            UpdateDor(k);
            UpdateLaunch(snap, k);
            UpdateCenter(snap, k);
            UpdateTags(snap, me);
            UpdateFeed();
            UpdateBreak(snap);
            TickUpgrade(snap, k, me);
        }

        private void UpdateScore(MatchSnapshot snap, int me)
        {
            ModeRules rules = S.Setup.Rules;
            List<ParticipantSetup> ps = S.Setup.Participants;
            if (rules.IsDuel)
            {
                int other = me == 0 ? 1 : 0;
                if (other >= snap.Kites.Length) other = me;
                _score.text = $"{Short(ps[me].displayName)}  {snap.Kites[me].RoundsWon} – {snap.Kites[other].RoundsWon}  {Short(ps[other].displayName)}{(ps[other].isBot ? " <size=20>[BOT]</size>" : "")}";
                _timer.text = $"R{snap.RoundNumber}  {Clock(snap.Phase == MatchPhase.Round ? snap.PhaseTimeLeft : rules.roundMaxTime)}";
            }
            else if (rules.teamCount > 1)
            {
                _score.text = $"BLUE {snap.TeamScores[0]}   –   {snap.TeamScores[1]} RED   <size=22>(to {rules.teamCutsToWin})</size>";
                _timer.text = Clock(rules.matchTime - snap.MatchTime);
            }
            else
            {
                int place = 1;
                for (int i = 0; i < snap.Kites.Length; i++) if (snap.Kites[i].Score > snap.Kites[me].Score) place++;
                int alive = 0;
                foreach (KiteSnap x in snap.Kites) if (!x.Eliminated) alive++;
                _score.text = rules.stormZone ? $"Alive {alive}/{snap.Kites.Length}   Cuts {snap.Kites[me].Cuts}"
                    : $"#{place}   Score {snap.Kites[me].Score}   Cuts {snap.Kites[me].Cuts}";
                _timer.text = rules.matchTime > 0 ? Clock(rules.matchTime - snap.MatchTime) : "";
            }
        }

        private void UpdatePhase(MatchSnapshot snap)
        {
            if (snap.Phase != MatchPhase.Round) { _phase.text = snap.Phase.ToString().ToUpper(); return; }
            string p = snap.RoundPhase.ToString().ToUpper();
            if (snap.GustActive) p = "<color=#FFB347>GUST!</color>";
            else if (snap.RoundPhase == RoundPhase.Gust && snap.NextGustIn >= 0f) p += $"  next gust {snap.NextGustIn:0}s";
            else if (snap.RoundPhase == RoundPhase.Storm) p = "<color=#C77DFF>STORM</color>  power ×1.10";
            if (snap.BirdsActive) p += "  <color=#FFFFFF>birds!</color>";
            _phase.text = p;
        }

        private void UpdateDor(in KiteSnap k)
        {
            ParticipantLoadout l = S.Setup.Participants[S.LocalIndex].stack[Mathf.Min(k.StackIndex, S.Setup.Participants[S.LocalIndex].stack.Count - 1)];
            string dive = k.DiveCooldown > 0f ? $"  DIVE {k.DiveCooldown:0.0}s" : "";
            Color durC = k.Durability < 30f ? Theme.Bad : Color.white;
            _dor.text = $"{SpecDefaults.NameOf(l.dorId)} · {SpecDefaults.NameOf(l.charkhiId)}   Line {k.LineOut:0}/{k.MaxLineOut:0} m   " +
                        $"<color=#{ColorUtility.ToHtmlStringRGB(durC)}>Kite {k.Durability:0}%</color>{dive}";
        }

        private void UpdateLaunch(MatchSnapshot snap, in KiteSnap k)
        {
            bool show = snap.Phase == MatchPhase.Launch && !S.Setup.UseLaunchRitual;
            _launch.gameObject.SetActive(show);
            if (!show) return;
            float over = S.Setup.Phases.launchOverfill;
            _launchFill.anchorMax = new Vector2(Mathf.Clamp01(k.LaunchHold / over), 1f);
            _launchText.text = $"HOLD PULL to launch – let go in the gold zone   ({snap.PhaseTimeLeft:0.0}s)";
        }

        /// <summary>True while an announcement ("YOUR LINE WAS CUT" …) is on screen, fade included.</summary>
        private bool Announcing => Time.unscaledTime < _centerUntil;

        private void UpdateCenter(MatchSnapshot snap, in KiteSnap k)
        {
            if (Announcing)
            {
                // Fade out over the last 0.4 s, so it is gone before the spectate bar takes the same zone (audit F1).
                float a = Mathf.Clamp01((_centerUntil - Time.unscaledTime) / AnnounceFade);
                SetAlpha(_center, a);
                SetAlpha(_sub, a);
                return;
            }
            SetAlpha(_center, 1f);
            SetAlpha(_sub, 1f);
            string big = "", small = "";
            switch (snap.Phase)
            {
                case MatchPhase.Intro:
                    big = ArenaLayout.DisplayName(S.Setup.Arena);
                    small = $"{S.Setup.Rules.displayName} · wind {snap.WindSpeed:0} m/s toward {Compass(snap.WindDirection)}";
                    break;
                case MatchPhase.LoadoutBreak:
                    big = $"Loadout break {snap.PhaseTimeLeft:0}";
                    small = $"Change your {Terms.TailLower} or {Terms.SpoolLower}";
                    break;
                case MatchPhase.Round:
                    // While the spectate bar is up it carries this (name, next kite, eliminated) – one thing per zone.
                    if (SpectateBarWanted(snap, S.LocalIndex)) break;
                    if (k.Status == KiteStatus.Waiting && k.RespawnIn > 0f) big = $"Next kite in {k.RespawnIn:0}";
                    else if (!k.IsFlying && k.Eliminated) { big = "Eliminated"; small = "Spectating"; }
                    break;
                case MatchPhase.Results:
                    big = S.Result != null && S.Result.WinnerIndex == S.LocalIndex ? "VICTORY" : S.Result != null && S.Result.Draw ? "DRAW" : "MATCH OVER";
                    break;
            }
            _center.text = big;
            _sub.text = small;
        }

        private static void SetAlpha(Text t, float a)
        {
            Color c = t.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            t.color = c;
        }

        private void UpdateTags(MatchSnapshot snap, int me)
        {
            Camera cam = Game.Matches.Camera;
            KiteView[] views = Game.Matches.Presenter.Kites;
            if (cam == null || views == null) return;
            while (_tagPool.Count < views.Length)
            {
                Text t = UiKit.Label(_tags, "", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
                t.rectTransform.sizeDelta = new Vector2(320, 34);
                _tagPool.Add(t);
            }
            for (int i = 0; i < views.Length; i++)
            {
                Text tag = _tagPool[i];
                bool show = i != me && i < snap.Kites.Length && snap.Kites[i].IsFlying && views[i].Visible;
                Vector3 sp = show ? cam.WorldToScreenPoint(views[i].KitePosition) : Vector3.zero;
                show &= sp.z > 0f;
                tag.gameObject.SetActive(show);
                if (!show) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_tags, sp, null, out Vector2 local);
                // Above the kite marker by screen pixels (a far kite's tag no longer sits on top of the kite).
                tag.rectTransform.anchoredPosition = local + new Vector2(0f, KiteCanvasHeight(cam, views[i]) * 0.5f + 46f);
                ParticipantSetup p = S.Setup.Participants[i];
                bool cbTag = Game.ColorBlind;
                string team = S.Setup.Rules.teamCount > 1
                    ? "<color=#" + ColorUtility.ToHtmlStringRGB(p.team == _teams[me] ? (cbTag ? Theme.ZoneSafe(true) : new Color(0.35f, 0.88f, 0.88f)) : (cbTag ? Theme.ZoneDanger(true) : new Color(1f, 0.44f, 0.44f))) + ">"
                    : "<color=#FFFFFF>";
                tag.text = $"{team}{p.displayName}</color> <size=16>{p.country}{(p.isBot ? " BOT" : "")}{(p.captain ? " (C)" : "")}</size>";
            }

            UpdateMarkers(snap, me, cam, views);

            // Approach ring around my kite (spec 9.3).
            bool approach = snap.Kites[me].Approaching && snap.Kites[me].IsFlying && !snap.Kites[me].InContest;
            _ring.gameObject.SetActive(approach);
            if (approach)
            {
                Vector3 sp = cam.WorldToScreenPoint(views[me].KitePosition);
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Root, sp, null, out Vector2 l)) _ring.anchoredPosition = l;
                _ring.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f));
            }
        }

        private void UpdateFeed()
        {
            for (int i = _feedItems.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime > _feedItems[i].until)
                {
                    if (_feedItems[i].go != null) Object.Destroy(_feedItems[i].go);
                    _feedItems.RemoveAt(i);
                }
            }
        }

        public void Feed(string text, Color color)
        {
            if (_feed == null) return;
            Text t = UiKit.Label(_feed, text, 24, color, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            t.Size(-1, 32);
            _feedItems.Add((t.gameObject, Time.unscaledTime + 5f));
            if (_feedItems.Count > 6)
            {
                if (_feedItems[0].go != null) Object.Destroy(_feedItems[0].go);
                _feedItems.RemoveAt(0);
            }
        }

        /// <summary>Big centre message: fully shown for <paramref name="seconds"/>, then fades out over 0.4 s.</summary>
        public void Announce(string big, string small, float seconds = 2.2f)
        {
            if (_center == null) return;
            _center.text = big;
            _sub.text = small;
            SetAlpha(_center, 1f);
            SetAlpha(_sub, 1f);
            _centerUntil = Time.unscaledTime + seconds + AnnounceFade;
        }

        // ---- Loadout break (Ranked, spec 5.1) ---------------------------------

        private void UpdateBreak(MatchSnapshot snap)
        {
            bool show = snap.Phase == MatchPhase.LoadoutBreak && !S.Setup.Participants[S.LocalIndex].isBot;
            if (show && _breakPanel == null) BuildBreak();
            if (!show && _breakPanel != null) { Object.Destroy(_breakPanel); _breakPanel = null; }
        }

        private void BuildBreak()
        {
            Image panel = UiKit.Panel(Root, Theme.Panel, "LoadoutBreak");
            _breakPanel = panel.gameObject;
            panel.rectTransform.Place(new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 330));
            RectTransform col = UiKit.Column(panel.transform, 14, 24);
            col.Stretch();
            UiKit.Label(col, Terms.SouthAsian ? "Dum (tail)" : "Tail", Theme.BodySize, Theme.TextDim).Size(-1, 34);
            RectTransform tails = UiKit.Row(col, 10);
            tails.Size(-1, 70);
            foreach (var t in SpecDefaults.Tails)
            {
                string id = t.Key;
                UiKit.Button(tails, SpecDefaults.NameOf(id), () => Change(id, null), Theme.PanelLight, 22).Size(-1, 64, 1);
            }
            UiKit.Label(col, Terms.Spool, Theme.BodySize, Theme.TextDim).Size(-1, 34);
            RectTransform ch = UiKit.Row(col, 10);
            ch.Size(-1, 70);
            PlayerProfile p = Game.Profile;
            foreach (var c in SpecDefaults.Charkhis)
            {
                string id = c.Key;
                if (p != null && !p.HasCharkhi(id)) continue;
                UiKit.Button(ch, SpecDefaults.NameOf(id), () => Change(null, id), Theme.PanelLight, 22).Size(-1, 64, 1);
            }
        }

        private void Change(string tail, string charkhi)
        {
            ParticipantLoadout cur = S.Setup.Participants[S.LocalIndex].stack[0];
            var sim = (S as OfflineMatchSession)?.Simulation;
            string t = tail ?? (sim != null ? sim.Participants[S.LocalIndex].Current.tailId : cur.tailId);
            string c = charkhi ?? (sim != null ? sim.Participants[S.LocalIndex].Current.charkhiId : cur.charkhiId);
            bool ok = S.RequestLoadoutChange(t, c);
            UI.Toast(ok ? $"Loadout: {SpecDefaults.NameOf(t)} + {SpecDefaults.NameOf(c)}" : "Can't change now", ok ? Theme.Good : Theme.Bad);
        }

        // ---- Menu -------------------------------------------------------------

        private void OpenMenu()
        {
            bool offline = S != null && !S.IsOnline;
            if (offline) { Time.timeScale = 0f; Game.Matches.InputBlocked = true; }
            RectTransform body = UI.Popup("Menu", new Vector2(720, 640), false);
            RectTransform col = UiKit.Column(body, 18, 0);
            col.Stretch();
            void Resume()
            {
                UI.ClosePopupOf(body);
                Time.timeScale = 1f;
                Game.Matches.InputBlocked = false;
            }
            UiKit.Button(col, "Resume", Resume, Theme.Good).Size(-1, 96);
            UiKit.Button(col, "Settings", () => { Resume(); UI.Push(new Screens.SettingsScreen()); }, Theme.PanelLight).Size(-1, 96);
            UiKit.Button(col, "Edit controls", () => { Resume(); SetEditing(true); }, Theme.PanelLight).Size(-1, 96);
            bool ranked = S.Setup.Rules.ranked;
            UiKit.Button(col, ranked ? "Quit (counts as a loss)" : "Quit match", () =>
            {
                UI.Confirm("Quit match?", ranked ? "Quitting a Ranked match is a loss: −35 rank points and a 10-minute queue ban." : "You will lose this match.",
                    "Quit", () => { Resume(); Game.Flow.QuitMatch(); });
            }, Theme.Bad).Size(-1, 96);
        }

        private void SetEditing(bool on)
        {
            _editing = on;
            foreach (DragHandle h in _handles)
            {
                h.Editing = on;
                var g = h.GetComponent<OnScreenControl>();
                if (g == null) g = h.GetComponentInChildren<OnScreenControl>();
                if (g != null) g.enabled = !on;
            }
            if (on)
            {
                Button done = UiKit.Button(Root, "Done – save layout", () =>
                {
                    SetEditing(false);
                    Game.Settings.Save();
                }, Theme.Good);
                done.name = "LayoutDone";
                done.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 100));
                UI.Toast("Drag the controls, then tap Done");
            }
            else
            {
                Transform d = Root.Find("LayoutDone");
                if (d != null) Object.Destroy(d.gameObject);
            }
        }

        public override bool OnBack()
        {
            if (_editing) { SetEditing(false); return false; }
            OpenMenu();
            return false;
        }

        private static string Clock(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            return $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";
        }

        private static string Short(string name) => string.IsNullOrEmpty(name) ? "?" : name.Length > 12 ? name.Substring(0, 12) : name;

        private static string Compass(float deg)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int i = Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8;
            return names[i];
        }
    }
}
