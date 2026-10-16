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
    public sealed class MatchHudScreen : UIScreen
    {
        public override bool Opaque => false;

        private IMatchSession S => Game.Matches?.Session;
        private TensionBarWidget _tension;
        private ContestMeterWidget _meter;
        private RadarWidget _radar;
        private CompassWidget _compass;
        private Text _score, _timer, _phase, _center, _sub, _dor, _launchText;
        private RectTransform _feed, _tags, _launch, _launchFill, _ring;
        private GameObject _breakPanel;
        private readonly List<Text> _tagPool = new List<Text>();
        private readonly List<(Text text, float until)> _feedItems = new List<(Text, float)>();
        private readonly List<DragHandle> _handles = new List<DragHandle>();
        private float _centerUntil;
        private bool _editing;
        private int[] _teams;

        protected override void Build()
        {
            if (S == null) return;
            _teams = new int[S.Setup.Participants.Count];
            for (int i = 0; i < _teams.Length; i++) _teams[i] = S.Setup.Participants[i].team;

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

            _feed = UiKit.Column(Root, 6, 0, TextAnchor.UpperRight);
            _feed.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -272), new Vector2(560, 320));

            _center = UiKit.Label(Root, "", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            _center.rectTransform.Place(new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 110));
            _sub = UiKit.Label(Root, "", 30, Color.white, TextAnchor.UpperCenter).Shadowed();
            _sub.rectTransform.Place(new Vector2(0.5f, 0.66f), new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(1500, 120));

            BuildLaunch();
            BuildControls();

            Button menu = UiKit.Button(Root, "II", OpenMenu, new Color(0, 0, 0, 0.5f), 34);
            menu.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(96, 76));
        }

        private void BuildTop()
        {
            Image bg = UiKit.Panel(Root, new Color(0, 0, 0, 0.45f), "Score", false);
            bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(820, 118));
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

            RectTransform stickBase = Control("Joystick", new Vector2(0, 0), new Vector2(250, 230), 300 * js, Sprites.Circle, new Color(1, 1, 1, 0.16f), left);
            Image knob = UiKit.Image(stickBase, new Color(1, 1, 1, 0.6f), Sprites.Circle, "Knob", true);
            knob.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130 * js, 130 * js));
            knob.gameObject.SetActive(false); // set controlPath before OnEnable
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = 110f * js;
            stick.controlPath = KiteInputReader.SteerStickPath;
            knob.gameObject.SetActive(true);

            Button(Control("PULL", new Vector2(1, 0), new Vector2(-220, 210), 230 * bs, Sprites.Circle, new Color(0.95f, 0.55f, 0.15f, 0.85f), left), "PULL", 44 * bs, KiteInputReader.PullButtonPath);
            Button(Control("DIVE", new Vector2(1, 0), new Vector2(-470, 150), 150 * bs, Sprites.Circle, new Color(0.85f, 0.25f, 0.3f, 0.85f), left), "DIVE", 30 * bs, KiteInputReader.DiveButtonPath);
            Button(Control("REEL", new Vector2(1, 0), new Vector2(-220, 440), 150 * bs, Sprites.Circle, new Color(0.25f, 0.55f, 0.95f, 0.85f), left), "REEL", 30 * bs, KiteInputReader.ReelButtonPath);
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

        private static void Button(RectTransform rt, string label, float fontSize, string path)
        {
            Text t = UiKit.Label(rt, label, Mathf.RoundToInt(fontSize), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            t.rectTransform.Stretch();
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
                _score.text = $"<color=#5AA0FF>BLUE {snap.TeamScores[0]}</color>   –   <color=#FF6060>{snap.TeamScores[1]} RED</color>   <size=22>(to {rules.teamCutsToWin})</size>";
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
            bool show = snap.Phase == MatchPhase.Launch;
            _launch.gameObject.SetActive(show);
            if (!show) return;
            float over = S.Setup.Phases.launchOverfill;
            _launchFill.anchorMax = new Vector2(Mathf.Clamp01(k.LaunchHold / over), 1f);
            _launchText.text = $"HOLD PULL to launch – let go in the gold zone   ({snap.PhaseTimeLeft:0.0}s)";
        }

        private void UpdateCenter(MatchSnapshot snap, in KiteSnap k)
        {
            if (Time.unscaledTime < _centerUntil) return;
            string big = "", small = "";
            switch (snap.Phase)
            {
                case MatchPhase.Intro:
                    big = ArenaLayout.DisplayName(S.Setup.Arena);
                    small = $"{S.Setup.Rules.displayName} · wind {snap.WindSpeed:0} m/s toward {Compass(snap.WindDirection)}";
                    break;
                case MatchPhase.LoadoutBreak:
                    big = $"Loadout break {snap.PhaseTimeLeft:0}";
                    small = "Change your dum or charkhi";
                    break;
                case MatchPhase.Round:
                    if (k.Status == KiteStatus.Waiting && k.RespawnIn > 0f) big = $"Next kite in {k.RespawnIn:0}";
                    else if (!k.IsFlying && k.Eliminated) { big = "Eliminated"; small = "Spectating"; }
                    else if (Game.Matches.Presenter.SpectateTarget >= 0) small = "Spectating " + S.Setup.Participants[Game.Matches.Presenter.SpectateTarget].displayName;
                    break;
                case MatchPhase.Results:
                    big = S.Result != null && S.Result.WinnerIndex == S.LocalIndex ? "VICTORY" : S.Result != null && S.Result.Draw ? "DRAW" : "MATCH OVER";
                    break;
            }
            _center.text = big;
            _sub.text = small;
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
                Vector3 sp = show ? cam.WorldToScreenPoint(views[i].KitePosition + Vector3.up * 2.2f) : Vector3.zero;
                show &= sp.z > 0f;
                tag.gameObject.SetActive(show);
                if (!show) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_tags, sp, null, out Vector2 local);
                tag.rectTransform.anchoredPosition = local;
                ParticipantSetup p = S.Setup.Participants[i];
                string team = S.Setup.Rules.teamCount > 1 ? (p.team == _teams[me] ? "<color=#5AE0E0>" : "<color=#FF7070>") : "<color=#FFFFFF>";
                tag.text = $"{team}{p.displayName}</color> <size=16>{p.country}{(p.isBot ? " BOT" : "")}{(p.captain ? " (C)" : "")}</size>";
            }

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
                    if (_feedItems[i].text != null) Object.Destroy(_feedItems[i].text.gameObject);
                    _feedItems.RemoveAt(i);
                }
            }
        }

        public void Feed(string text, Color color)
        {
            if (_feed == null) return;
            Text t = UiKit.Label(_feed, text, 24, color, TextAnchor.MiddleRight, FontStyle.Bold).Shadowed();
            t.Size(-1, 32);
            _feedItems.Add((t, Time.unscaledTime + 5f));
            if (_feedItems.Count > 6)
            {
                Object.Destroy(_feedItems[0].text.gameObject);
                _feedItems.RemoveAt(0);
            }
        }

        public void Announce(string big, string small, float seconds = 2.2f)
        {
            if (_center == null) return;
            _center.text = big;
            _sub.text = small;
            _centerUntil = Time.unscaledTime + seconds;
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
            UiKit.Label(col, "Dum (tail)", Theme.BodySize, Theme.TextDim).Size(-1, 34);
            RectTransform tails = UiKit.Row(col, 10);
            tails.Size(-1, 70);
            foreach (var t in SpecDefaults.Tails)
            {
                string id = t.Key;
                UiKit.Button(tails, SpecDefaults.NameOf(id), () => Change(id, null), Theme.PanelLight, 22).Size(-1, 64, 1);
            }
            UiKit.Label(col, "Charkhi", Theme.BodySize, Theme.TextDim).Size(-1, 34);
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
