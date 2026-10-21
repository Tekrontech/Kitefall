using System;
using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
using Kitefall.Services;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Draws a match from an <see cref="IMatchSession"/>: arena + rooftops, kites, animated characters (upgrade W1),
    /// NPC helpers, crates, storm, effects, the shoulder / kite camera (W2), spectating (F5), emotes and chat bubbles,
    /// audio ambience, haptics, slow motion and the round-end replay. Pure view – never changes rules.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public IMatchSession Session { get; private set; }
        public MatchCamera Camera { get; private set; }
        public KiteView[] Kites { get; private set; }
        public CharacterView[] Characters { get; private set; }
        public event Action<MatchEvent> EventRaised;
        public bool Replaying => _replayLeft > 0f;
        public int SpectateTarget { get; private set; } = -1;
        public int CutBy { get; private set; } = -1;
        /// <summary>The camera currently follows the kite (true) or the character's shoulder (false).</summary>
        public bool KiteCamActive { get; private set; }

        private GameObject _world;
        private FlyerView[] _flyers;
        private readonly Dictionary<int, CrateView> _crates = new Dictionary<int, CrateView>();
        private readonly Dictionary<int, HelperNpc> _helpers = new Dictionary<int, HelperNpc>();
        private StormZoneView _storm;
        private Effects _effects;
        private ReplayBuffer _replay;
        private float _replayLeft;
        private int _replayVictim = -1;
        private float _replayCursor;
        private float _slowMoLeft;
        private readonly KiteSnap[] _interp = new KiteSnap[32];
        private Transform _lookAhead;
        private float _kiteCamUntil;
        private float _manualUntil;
        private bool _manualKiteCam;
        private GameObject _waitingWorld;

        public void Begin(IMatchSession session, ArenaData arena, Camera cam)
        {
            Session = session;
            MatchSetup setup = session.Setup;
            _crates.Clear();
            _world = new GameObject("MatchWorld");
            ArenaBuilder.Build(session.Arena, arena, setup.TimeOfDay01, _world.transform);
            if (session.Arena.Roofs != null && setup.WarmupTime > 0f && setup.Rules.warmup)
            {
                WaitingArea area = session.Arena.BuildWaitingRoof(setup.Participants.Count);
                _waitingWorld = ArenaBuilder.BuildWaitingArea(area, session.Arena.Type, _world.transform);
            }

            int n = setup.Participants.Count;
            Kites = new KiteView[n];
            Characters = new CharacterView[n];
            _flyers = new FlyerView[n];
            int tailSegments = Game.Settings != null ? Game.Settings.TailSegments : 10;
            TailRopeSettings rope = Game.Balance != null ? Game.Balance.tail : new TailRopeSettings();
            for (int i = 0; i < n; i++)
            {
                ParticipantSetup p = setup.Participants[i];
                ParticipantLoadout l = p.stack.Count > 0 ? p.stack[0] : setup.FreeGuddi;
                var kv = new GameObject("KiteView_" + i).AddComponent<KiteView>();
                kv.transform.SetParent(_world.transform, false);
                kv.Init(i, l.kiteId, l.skinId, l.tailId, DorColor(l.dorId), tailSegments, rope);
                Kites[i] = kv;

                Color team = TeamColor(setup, p, i, session.LocalIndex);
                Vector3 anchor = ArenaBuilder.ToUnity(session.Arena.Anchors[i]);
                try
                {
                    var cv = new GameObject("Character_" + i).AddComponent<CharacterView>();
                    cv.transform.SetParent(_world.transform, false);
                    cv.transform.position = anchor - Vector3.up * 1.35f;
                    CharacterBuilder.Look look = CharacterBuilder.LookFor(p.characterId, p.outfitId, p.accessories, l.charkhiId, l.kiteId, l.skinId);
                    string label = p.displayName + (p.isBot ? " (bot)" : "");
                    cv.Init(look, label, team, i == session.LocalIndex, true);
                    Characters[i] = cv;
                    kv.LineStart = cv.LineAnchor;
                    kv.HideWhenWaiting = true;
                }
                catch (Exception ex)
                {
                    // Safety fallback (upgrade W1): the old primitive flyer if the character cannot be built.
                    Debug.LogWarning($"[Kitefall] Character '{p.characterId}' could not be built – showing the simple fallback flyer instead. Reason: {ex.Message}");
                    Debug.LogException(ex);
                    var fv = new GameObject("Flyer_" + i).AddComponent<FlyerView>();
                    fv.transform.SetParent(_world.transform, false);
                    fv.Init(anchor, team);
                    _flyers[i] = fv;
                }
            }

            if (setup.Rules.stormZone)
            {
                _storm = new GameObject("StormZone").AddComponent<StormZoneView>();
                _storm.transform.SetParent(_world.transform, false);
                _storm.Init();
            }
            _effects = new GameObject("Effects").AddComponent<Effects>();
            _effects.transform.SetParent(_world.transform, false);
            _effects.Init();
            _lookAhead = new GameObject("CameraLookAhead").transform;
            _lookAhead.SetParent(_world.transform, false);

            Camera = cam.GetComponent<MatchCamera>();
            if (Camera == null) Camera = cam.gameObject.AddComponent<MatchCamera>();
            ApplyCameraSettings();
            int local = Mathf.Clamp(session.LocalIndex, 0, n - 1);
            Camera.Anchor = ArenaBuilder.ToUnity(session.Arena.Anchors[local]);
            Camera.OrbitCenter = ArenaBuilder.ToUnity(session.Arena.SkyCenter);
            Camera.Target = Kites[local].transform.GetChild(0);
            BindCharacter(local);
            Camera.Mode = CameraMode.FlyOver;
            if (session.Arena.Roofs != null)
            {
                RoofGraph g = session.Arena.Roofs;
                Camera.MapCenter = new Vector3((g.MinX + g.MaxX) * 0.5f, g.MaxHeight, (g.MinZ + g.MaxZ) * 0.5f);
                Camera.MapHeight = Mathf.Max(70f, (g.MaxX - g.MinX) * 0.9f);
            }
            Camera.Snap();

            _replay = new ReplayBuffer(80, n);
            Game.Audio?.PlayMusic(arena != null ? (int)arena.music : 0);
        }

        private static Color TeamColor(MatchSetup setup, ParticipantSetup p, int i, int local)
        {
            if (i == local) return new Color(0.98f, 0.58f, 0.15f);
            if (setup.Rules.teamCount > 1) return p.team == 0 ? new Color(0.25f, 0.5f, 0.95f) : new Color(0.9f, 0.28f, 0.28f);
            return Color.HSVToRGB(i * 0.13f % 1f, 0.55f, 0.85f);
        }

        private void BindCharacter(int index)
        {
            CharacterView cv = index >= 0 && index < Characters.Length ? Characters[index] : null;
            Camera.Character = cv != null ? cv.transform : null;
            Camera.CharacterHead = cv != null ? cv.Rig.Head : null;
        }

        public void ApplyCameraSettings()
        {
            if (Camera == null || Game.Settings == null) return;
            Camera.Distance = Game.Settings.Data.cameraDistance;
            Camera.Follow = Game.Settings.Data.cameraFollow;
            Camera.ShakeEnabled = Game.Settings.Data.cameraShake;
        }

        /// <summary>HUD camera button: toggle Shoulder ↔ KiteCam for 10 s (manual look, upgrade W2).</summary>
        public void ToggleCamera()
        {
            _manualKiteCam = !KiteCamActive;
            _manualUntil = Time.unscaledTime + 10f;
        }

        /// <summary>Spectate ◀ ▶ (upgrade F5).</summary>
        public void CycleSpectate(int dir)
        {
            if (Session == null) return;
            SpectateTarget = SpectateRules.Cycle(Session.Current, Session.LocalIndex, SpectateTarget, dir, Teams(), Session.Setup.Rules.teamCount > 1);
        }

        private List<int> Teams()
        {
            var t = new List<int>();
            foreach (ParticipantSetup p in Session.Setup.Participants) t.Add(p.team);
            return t;
        }

        private static Color DorColor(string dorId)
        {
            switch (dorId)
            {
                case "reshmi": return new Color(1f, 0.85f, 0.9f, 0.95f);
                case "tez_dhaar": return new Color(1f, 0.8f, 0.55f, 0.95f);
                case "mazboot": return new Color(0.75f, 0.85f, 1f, 0.95f);
                case "lambi": return new Color(0.85f, 1f, 0.85f, 0.95f);
                case "lachakdaar": return new Color(0.95f, 0.9f, 1f, 0.95f);
                default: return new Color(0.97f, 0.96f, 0.92f, 0.95f);
            }
        }

        public void End()
        {
            Time.timeScale = 1f;
            if (_world != null) Destroy(_world);
            _world = null;
            _helpers.Clear();
            _crates.Clear(); // their objects went with the world – a stale list would break the next match
            Session = null;
            Game.Audio?.SetAmbience(0.2f, 0f, 0f);
        }

        private void OnDestroy() => Time.timeScale = 1f;

        private void LateUpdate()
        {
            if (Session == null) return;
            float dt = Time.deltaTime;
            MatchSnapshot cur = Session.Current, prev = Session.Previous;

            foreach (MatchEvent e in Session.DrainEvents()) HandleEvent(e, cur);

            if (_slowMoLeft > 0f)
            {
                _slowMoLeft -= Time.unscaledDeltaTime;
                if (_slowMoLeft <= 0f) Time.timeScale = 1f;
            }

            KiteSnap[] frame = cur.Kites;
            if (_replayLeft > 0f && _replay.Count > 0)
            {
                // Slow-motion replay (0.5×) of the last ~1.8 s before the cut.
                _replayLeft -= Time.unscaledDeltaTime;
                _replayCursor += Time.unscaledDeltaTime * 0.5f * MatchSimulation.TickRate;
                int age = Mathf.Max(0, Mathf.Min(_replay.Count - 1, 36) - (int)_replayCursor);
                frame = _replay.Get(age) ?? frame;
                if (_replayLeft <= 0f) EndReplay();
            }
            else
            {
                _replay.Record(cur);
                int n = Mathf.Min(cur.Kites.Length, prev.Kites.Length);
                for (int i = 0; i < n; i++) _interp[i] = Lerp(prev.Kites[i], cur.Kites[i], Session.Alpha);
                for (int i = n; i < cur.Kites.Length; i++) _interp[i] = cur.Kites[i];
                frame = _interp;
            }

            if (_waitingWorld != null) _waitingWorld.SetActive(cur.Phase == MatchPhase.Warmup);
            Vector3 wind = new Vector3(Mathf.Sin(cur.WindDirection * Mathf.Deg2Rad), 0f, Mathf.Cos(cur.WindDirection * Mathf.Deg2Rad)) * cur.WindSpeed;
            int local = Session.LocalIndex;
            int nearestEnemy = local >= 0 && local < cur.Kites.Length && cur.Kites[local].Approaching ? cur.Kites[local].NearestEnemy : -1;
            for (int i = 0; i < Kites.Length && i < cur.Kites.Length; i++)
            {
                bool inContest = cur.Kites[i].InContest;
                Kites[i].Apply(frame[i], wind, inContest, i == nearestEnemy, Time.time, dt);
                if (Characters[i] != null) Characters[i].Apply(FrameFor(i, frame[i], cur, prev), dt);
                else _flyers[i]?.Apply(frame[i], Kites[i].KitePosition, dt);
                UpdateHelper(i, frame[i], cur, dt);
            }

            foreach (ContestSnap c in cur.Contests)
                if (UnityEngine.Random.value < 0.5f) _effects.Sparks(ArenaBuilder.ToUnity(c.Point), 3);

            UpdateCrates(cur);
            if (_storm != null) _storm.Apply(ArenaBuilder.ToUnity(cur.StormCenter), cur.StormRadius);
            UpdateCamera(cur);
            UpdateAudio(cur, local);
        }

        private CharacterFrame FrameFor(int i, in KiteSnap k, MatchSnapshot cur, MatchSnapshot prev)
        {
            KiteSnap p = i < prev.Kites.Length ? prev.Kites[i] : k;
            Vector3 pos = Vector3.Lerp(ArenaBuilder.ToUnity(p.CharacterPosition), ArenaBuilder.ToUnity(k.CharacterPosition), Mathf.Clamp01(Session.Alpha));
            if (Vector3.Distance(ArenaBuilder.ToUnity(p.CharacterPosition), ArenaBuilder.ToUnity(k.CharacterPosition)) > 6f) pos = ArenaBuilder.ToUnity(k.CharacterPosition);
            var f = new CharacterFrame
            {
                State = k.Flyer,
                Action = k.Action,
                Diving = k.Diving,
                InContest = k.InContest,
                PrepStep = k.PrepStep,
                PrepStepAge = cur.Time - k.PrepStepStart,
                PrepHelped = k.PrepHelped,
                PrepTaps = k.PrepTaps,
                PrepTugs = k.PrepTugs,
                PrepTossHold = k.PrepTossHold,
                Position = pos,
                Yaw = k.CharacterYaw,
                Speed = k.CharacterSpeed,
                KitePosition = Kites[i].KitePosition,
                KiteValid = k.IsFlying,
                WindDirection = cur.WindDirection,
                Time = Time.time
            };
            float period = Session.Setup.Prep != null ? Session.Setup.Prep.tunkePeriod : 1.1f;
            float tp = Mathf.Max(0f, f.PrepStepAge) / Mathf.Max(0.1f, period);
            f.NosePhase = tp - Mathf.Floor(tp);
            if (k.Flyer == FlyerState.Traversing && Session.Arena.Roofs != null && k.TraverseLink >= 0 && k.TraverseLink < Session.Arena.Roofs.Links.Count)
            {
                RoofLink link = Session.Arena.Roofs.Link(k.TraverseLink);
                bool fromA = k.RoofId == link.A;
                f.LinkKind = link.Kind;
                f.LinkFrom = ArenaBuilder.ToUnity(fromA ? link.PointA : link.PointB);
                f.LinkTo = ArenaBuilder.ToUnity(fromA ? link.PointB : link.PointA);
                f.TraverseProgress = k.TraverseProgress;
            }
            return f;
        }

        private void UpdateHelper(int i, in KiteSnap k, MatchSnapshot cur, float dt)
        {
            CharacterView cv = Characters[i];
            if (cv == null) return;
            bool wants = k.Flyer == FlyerState.Prep && k.PrepFull && !k.PrepHelped && (k.PrepStep == PrepStep.TieLine || k.PrepStep == PrepStep.Toss);
            _helpers.TryGetValue(i, out HelperNpc helper);
            if (wants && helper == null && Session.Arena.Roofs != null && k.RoofId >= 0)
            {
                RoofNode roof = Session.Arena.Roofs.Roof(k.RoofId);
                Vector3 c = ArenaBuilder.ToUnity(roof.Center);
                helper = HelperNpc.Spawn(_world.transform, ArenaBuilder.ToUnity(k.CharacterPosition), cur.WindDirection, c, new Vector2(roof.SizeX, roof.SizeZ),
                    Session.Setup.Participants[i].stack.Count > 0 ? Session.Setup.Participants[i].stack[0].kiteId : "guddi",
                    Session.Setup.Participants[i].stack.Count > 0 ? Session.Setup.Participants[i].stack[0].skinId : "");
                _helpers[i] = helper;
            }
            if (helper == null) return;
            cv.HelperNearby = !helper.Done && (k.PrepStep == PrepStep.TieLine || k.PrepStep == PrepStep.Toss);
            if (k.Flyer != FlyerState.Prep || k.PrepStep > PrepStep.Toss && k.PrepStep != PrepStep.Untangle) helper.Toss();
            helper.Tick(Time.time, cur.WindDirection, dt);
            if (helper.Done)
            {
                Destroy(helper.gameObject);
                _helpers.Remove(i);
            }
        }

        private static KiteSnap Lerp(in KiteSnap a, in KiteSnap b, float t)
        {
            if (a.Status != b.Status || !b.IsFlying) return b;
            KiteSnap r = b;
            t = Mathf.Clamp01(t);
            r.Azimuth = a.Azimuth + Mathf.DeltaAngle(a.Azimuth, b.Azimuth) * t;
            r.Elevation = Mathf.Lerp(a.Elevation, b.Elevation, t);
            r.Radius = Mathf.Lerp(a.Radius, b.Radius, t);
            r.Heading = a.Heading + Mathf.DeltaAngle(a.Heading, b.Heading) * t;
            r.Tension = Mathf.Lerp(a.Tension, b.Tension, t);
            return r;
        }

        private void UpdateCrates(MatchSnapshot cur)
        {
            var alive = new HashSet<int>();
            foreach (CrateSnap c in cur.Crates)
            {
                alive.Add(c.Id);
                // A view destroyed elsewhere is Unity "fake null": build it again instead of keeping a dead entry.
                if (_crates.TryGetValue(c.Id, out CrateView have) && have != null) continue;
                var cv = new GameObject("Crate_" + c.Id).AddComponent<CrateView>();
                cv.transform.SetParent(_world.transform, false);
                cv.Init(c);
                _crates[c.Id] = cv;
            }
            var remove = new List<int>();
            foreach (var kv in _crates) if (!alive.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (int id in remove)
            {
                CrateView v = _crates[id];
                _crates.Remove(id);
                if (v != null) Destroy(v.gameObject);
            }
        }

        // ---- Camera (upgrade W2) -----------------------------------------------------

        private void UpdateCamera(MatchSnapshot cur)
        {
            if (Camera == null) return;
            int local = Mathf.Clamp(Session.LocalIndex, 0, Kites.Length - 1);
            if (_replayLeft > 0f && _replayVictim >= 0)
            {
                Camera.Mode = CameraMode.Replay;
                Camera.Target = Kites[_replayVictim].transform.GetChild(0);
                return;
            }
            bool hasCharacters = Characters[local] != null;
            KiteSnap me = cur.Kites[local];
            switch (cur.Phase)
            {
                case MatchPhase.Warmup:
                    if (hasCharacters) { WalkCamera(local); return; }
                    Camera.Mode = CameraMode.FlyOver;
                    return;
                case MatchPhase.RoofSelect:
                    Camera.Mode = Session.Arena.Roofs != null ? CameraMode.Map : CameraMode.FlyOver;
                    return;
                case MatchPhase.Intro:
                    Camera.Mode = CameraMode.FlyOver;
                    return;
                case MatchPhase.Launch:
                case MatchPhase.LoadoutBreak:
                    Camera.Mode = CameraMode.Launch;
                    Camera.Anchor = ArenaBuilder.ToUnity(Session.Arena.Anchors[local]);
                    return;
            }

            if (hasCharacters && me.Flyer == FlyerState.Prep)
            {
                PrepCamera(local, me);
                return;
            }
            if (hasCharacters && (me.Flyer == FlyerState.Grounded || me.Flyer == FlyerState.Traversing))
            {
                WalkCamera(local);
                return;
            }

            int target = local;
            bool spectating = false;
            if (!me.IsFlying && (cur.Phase == MatchPhase.Round || cur.Phase == MatchPhase.Prep) && me.Flyer != FlyerState.PackingUp)
            {
                // Spectate (upgrade F5): the player who cut you first, ◀ ▶ cycles; teammates first in team modes.
                bool teamMode = Session.Setup.Rules.teamCount > 1;
                if (SpectateTarget < 0 || SpectateTarget >= cur.Kites.Length || cur.Kites[SpectateTarget].Eliminated)
                    SpectateTarget = SpectateRules.Default(cur, local, CutBy, Teams(), teamMode);
                if (SpectateTarget >= 0 && (me.Status != KiteStatus.Cut || me.Eliminated)) { target = SpectateTarget; spectating = true; }
            }
            else SpectateTarget = -1;

            Camera.Target = Kites[target].transform.GetChild(0);
            Camera.Anchor = ArenaBuilder.ToUnity(Session.Arena.Anchors[target]);
            BindCharacter(target);
            KiteSnap t = cur.Kites[target];
            bool wantKiteCam = WantsKiteCam(t, cur.Kites[target].Flyer);
            KiteCamActive = wantKiteCam || !hasCharacters;
            Camera.BlendTime = 0.35f;
            Camera.Mode = KiteCamActive ? (spectating ? CameraMode.Spectate : CameraMode.Follow) : CameraMode.Shoulder;
        }

        private bool WantsKiteCam(in KiteSnap k, FlyerState flyer)
        {
            CameraStyle style = Game.Settings != null ? Game.Settings.Data.cameraStyle : CameraStyle.Auto;
            if (Time.unscaledTime < _manualUntil) return _manualKiteCam;
            if (style == CameraStyle.KiteOnly) return true;
            if (style == CameraStyle.ShoulderOnly) return false;
            // Auto: kite cam on approach / contest, back to the shoulder 1.5 s after it ends.
            if (k.InContest || k.Approaching) _kiteCamUntil = Time.unscaledTime + 1.5f;
            return Time.unscaledTime < _kiteCamUntil;
        }

        private void PrepCamera(int local, in KiteSnap me)
        {
            BindCharacter(local);
            CharacterView cv = Characters[local];
            switch (me.PrepStep)
            {
                case PrepStep.PickKite:
                case PrepStep.TieKanni:
                case PrepStep.TieLine:
                    Camera.Target = Kites[local].transform.GetChild(0);
                    Camera.Mode = CameraMode.PrepClose;
                    Camera.BlendTime = 0.6f;
                    break;
                case PrepStep.Toss:
                    Camera.Mode = CameraMode.PrepWide;
                    Camera.BlendTime = 0.6f;
                    break;
                default:
                    Camera.Target = cv.PrepKite != null && cv.PrepKite.gameObject.activeSelf ? cv.PrepKite : Kites[local].transform.GetChild(0);
                    Camera.Mode = CameraMode.Shoulder;
                    Camera.BlendTime = 0.8f;
                    break;
            }
            // The close / wide shots point where the kite will fly: downwind of the character.
            if (Camera.Mode != CameraMode.Shoulder)
            {
                Vector3 c = cv.transform.position;
                float w = Session.Current.WindDirection * Mathf.Deg2Rad;
                _lookAhead.position = c + new Vector3(Mathf.Sin(w), 0f, Mathf.Cos(w)) * 12f + Vector3.up * 2f;
                Camera.Target = _lookAhead;
            }
        }

        private void WalkCamera(int local)
        {
            BindCharacter(local);
            CharacterView cv = Characters[local];
            _lookAhead.position = cv.transform.position + cv.transform.forward * 10f + Vector3.up * 1.4f;
            Camera.Target = _lookAhead;
            Camera.Mode = CameraMode.Shoulder;
            Camera.BlendTime = 0.5f;
        }

        private void UpdateAudio(MatchSnapshot cur, int local)
        {
            if (Game.Audio == null || local < 0 || local >= cur.Kites.Length) return;
            KiteSnap me = cur.Kites[local];
            float wind01 = Mathf.InverseLerp(2f, 14f, cur.WindSpeed);
            float hiss = me.IsFlying ? Mathf.InverseLerp(40f, 100f, me.Tension) : 0f;
            float buzz = me.InContest ? 1f : 0f;
            Game.Audio.SetAmbience(wind01, hiss, buzz);
            if (me.InContest && me.SweetCenter >= 0f && Mathf.Abs(me.Tension - me.SweetCenter) <= me.SweetWidth * 0.5f)
                Game.Haptics?.SweetPulse();
        }

        private void HandleEvent(MatchEvent e, MatchSnapshot cur)
        {
            int local = Session.LocalIndex;
            bool mine = e.A == local || e.B == local;
            Vector3 pos = ArenaBuilder.ToUnity(e.Position);
            switch (e.Type)
            {
                case MatchEventType.ContactStarted:
                    _effects.Sparks(pos, 25);
                    Game.Audio?.Play(Sfx.Spark);
                    if (mine) Game.Haptics?.Light();
                    break;
                case MatchEventType.KiteCut:
                    Kites[e.A].OnCut(pos);
                    bool confetti = e.B >= 0 && e.B == local;
                    _effects.Cut(pos, confetti);
                    Game.Audio?.Play(Sfx.Cut);
                    Characters[e.A]?.HitReact();
                    if (e.A == local) { CutBy = e.B; SpectateTarget = -1; }
                    if (mine)
                    {
                        Game.Haptics?.Strong();
                        Camera?.Shake(0.8f);
                        if (!Session.Setup.Rules.IsDuel) SlowMo(0.5f);
                    }
                    if (Session.Setup.Rules.IsDuel) _replayVictim = e.A;
                    break;
                case MatchEventType.SelfSnap:
                    _effects.Snap(pos);
                    Game.Audio?.Play(Sfx.Snap);
                    Characters[e.A]?.HitReact();
                    if (mine) { Game.Haptics?.Strong(); Camera?.Shake(0.6f); }
                    if (Session.Setup.Rules.IsDuel) _replayVictim = e.A;
                    break;
                case MatchEventType.Crash:
                    if (mine) { Game.Haptics?.Strong(); Camera?.Shake(0.4f); }
                    break;
                case MatchEventType.DiveStarted:
                    if (e.A == local) Game.Audio?.Play(Sfx.Whoosh);
                    break;
                case MatchEventType.GustStarted:
                    Game.Audio?.Play(Sfx.Gust, 0.8f);
                    break;
                case MatchEventType.RoundEnded:
                    bool autoEmotes = Game.Settings == null || Game.Settings.Data.autoEmotes;
                    if (e.A >= 0) { _flyers[e.A]?.React(true); if (autoEmotes) Characters[e.A]?.React(true); }
                    if (e.B >= 0) { _flyers[e.B]?.React(false); if (autoEmotes) Characters[e.B]?.React(false); }
                    if (e.Code == (int)RoundEndReason.Cut || e.Code == (int)RoundEndReason.SelfSnap) StartReplay();
                    if (e.A == local) Game.Audio?.Play(Sfx.Crowd, 0.6f);
                    break;
                case MatchEventType.MatchEnded:
                    Game.Audio?.Play(cur.Kites.Length > local && local >= 0 && e.A == local ? Sfx.Win : Sfx.Lose);
                    break;
                case MatchEventType.Launched:
                    if (e.A == local) Game.Audio?.Play(Sfx.Whoosh, 0.6f);
                    break;
                case MatchEventType.Emote:
                    Characters[e.A]?.PlayEmote(CharacterCatalog.EmoteId(e.Code));
                    break;
                case MatchEventType.QuickChat:
                    if (e.A >= 0 && e.A < Characters.Length && QuickChatVisible(e.A) && QuickChat.Valid(e.Code))
                        Characters[e.A]?.ShowChat(QuickChat.Messages[e.Code]);
                    break;
                case MatchEventType.Prep:
                    PrepFeedback(e, local);
                    break;
                case MatchEventType.PackedUp:
                case MatchEventType.ArrivedOnRoof:
                    if (e.A == local) Game.Haptics?.Light();
                    break;
            }
            EventRaised?.Invoke(e);
        }

        private bool QuickChatVisible(int sender)
        {
            int local = Session.LocalIndex;
            if (Game.Settings != null && !Game.Settings.Data.quickChat && sender != local) return false;
            if (local < 0) return true;
            return QuickChat.Visible(Session.Setup.Rules.mode, Session.Setup.Participants[sender].team, Session.Setup.Participants[local].team);
        }

        private void PrepFeedback(in MatchEvent e, int local)
        {
            if (e.A != local) return;
            switch ((PrepEventType)e.Code)
            {
                case PrepEventType.KanniTap:
                case PrepEventType.TugGood:
                    Game.Haptics?.Light();
                    Game.Audio?.Play(Sfx.Click, 0.7f);
                    break;
                case PrepEventType.TossPerfect:
                    Game.Audio?.Play(Sfx.Crowd, 0.7f);
                    Game.Haptics?.Strong();
                    break;
                case PrepEventType.TossMiss:
                case PrepEventType.Snagged:
                    Game.Haptics?.Strong();
                    break;
                case PrepEventType.Finished:
                    Game.Audio?.Play(Sfx.Whoosh, 0.7f);
                    break;
            }
        }

        private void SlowMo(float seconds)
        {
            Time.timeScale = 0.5f;
            _slowMoLeft = seconds;
        }

        private void StartReplay()
        {
            if (_replayVictim < 0 || _replay.Count < 10) return;
            _replayLeft = 3.4f;
            _replayCursor = 0f;
        }

        private void EndReplay()
        {
            _replayLeft = 0f;
            _replayVictim = -1;
            _replay.Clear();
        }
    }
}
