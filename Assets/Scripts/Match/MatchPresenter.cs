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
    /// Draws a match from an <see cref="IMatchSession"/>: arena, kites, flyers, crates, storm, effects,
    /// camera, audio ambience, haptics, slow motion and the round-end replay. Pure view – never changes rules.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public IMatchSession Session { get; private set; }
        public MatchCamera Camera { get; private set; }
        public KiteView[] Kites { get; private set; }
        public event Action<MatchEvent> EventRaised;
        public bool Replaying => _replayLeft > 0f;
        public int SpectateTarget { get; private set; } = -1;

        private GameObject _world;
        private FlyerView[] _flyers;
        private readonly Dictionary<int, CrateView> _crates = new Dictionary<int, CrateView>();
        private StormZoneView _storm;
        private Effects _effects;
        private ReplayBuffer _replay;
        private float _replayLeft;
        private int _replayVictim = -1;
        private float _replayCursor;
        private float _slowMoLeft;
        private readonly KiteSnap[] _interp = new KiteSnap[32];

        public void Begin(IMatchSession session, ArenaData arena, Camera cam)
        {
            Session = session;
            MatchSetup setup = session.Setup;
            _world = new GameObject("MatchWorld");
            ArenaBuilder.Build(session.Arena, arena, setup.TimeOfDay01, _world.transform);

            int n = setup.Participants.Count;
            Kites = new KiteView[n];
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
                var fv = new GameObject("Flyer_" + i).AddComponent<FlyerView>();
                fv.transform.SetParent(_world.transform, false);
                Color shirt = i == session.LocalIndex ? new Color(0.95f, 0.55f, 0.15f)
                    : setup.Rules.teamCount > 1 ? (p.team == 0 ? new Color(0.2f, 0.45f, 0.9f) : new Color(0.85f, 0.25f, 0.25f))
                    : Color.HSVToRGB(i * 0.13f % 1f, 0.5f, 0.75f);
                fv.Init(ArenaBuilder.ToUnity(session.Arena.Anchors[i]), shirt);
                _flyers[i] = fv;
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

            Camera = cam.GetComponent<MatchCamera>() ?? cam.gameObject.AddComponent<MatchCamera>();
            ApplyCameraSettings();
            int local = Mathf.Clamp(session.LocalIndex, 0, n - 1);
            Camera.Anchor = ArenaBuilder.ToUnity(session.Arena.Anchors[local]);
            Camera.OrbitCenter = ArenaBuilder.ToUnity(session.Arena.SkyCenter);
            Camera.Target = Kites[local].transform.GetChild(0);
            Camera.Mode = CameraMode.FlyOver;
            Camera.Snap();

            _replay = new ReplayBuffer(80, n);
            ArenaData data = arena;
            Game.Audio?.PlayMusic(data != null ? (int)data.music : 0);
        }

        public void ApplyCameraSettings()
        {
            if (Camera == null || Game.Settings == null) return;
            Camera.Distance = Game.Settings.Data.cameraDistance;
            Camera.Follow = Game.Settings.Data.cameraFollow;
            Camera.ShakeEnabled = Game.Settings.Data.cameraShake;
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

            Vector3 wind = new Vector3(Mathf.Sin(cur.WindDirection * Mathf.Deg2Rad), 0f, Mathf.Cos(cur.WindDirection * Mathf.Deg2Rad)) * cur.WindSpeed;
            int local = Session.LocalIndex;
            int nearestEnemy = local >= 0 && local < cur.Kites.Length && cur.Kites[local].Approaching ? cur.Kites[local].NearestEnemy : -1;
            for (int i = 0; i < Kites.Length && i < cur.Kites.Length; i++)
            {
                bool inContest = cur.Kites[i].InContest;
                Kites[i].Apply(frame[i], wind, inContest, i == nearestEnemy, Time.time, dt);
                _flyers[i].Apply(frame[i], Kites[i].KitePosition, dt);
            }

            foreach (ContestSnap c in cur.Contests)
                if (UnityEngine.Random.value < 0.5f) _effects.Sparks(ArenaBuilder.ToUnity(c.Point), 3);

            UpdateCrates(cur);
            if (_storm != null) _storm.Apply(ArenaBuilder.ToUnity(cur.StormCenter), cur.StormRadius);
            UpdateCamera(cur);
            UpdateAudio(cur, local);
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
                if (_crates.ContainsKey(c.Id)) continue;
                var cv = new GameObject("Crate_" + c.Id).AddComponent<CrateView>();
                cv.transform.SetParent(_world.transform, false);
                cv.Init(c);
                _crates[c.Id] = cv;
            }
            var remove = new List<int>();
            foreach (var kv in _crates) if (!alive.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (int id in remove) { Destroy(_crates[id].gameObject); _crates.Remove(id); }
        }

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
            switch (cur.Phase)
            {
                case MatchPhase.Intro:
                    Camera.Mode = CameraMode.FlyOver;
                    return;
                case MatchPhase.Launch:
                case MatchPhase.LoadoutBreak:
                    Camera.Mode = CameraMode.Launch;
                    Camera.Anchor = ArenaBuilder.ToUnity(Session.Arena.Anchors[local]);
                    return;
            }

            KiteSnap me = cur.Kites[local];
            int target = local;
            if (!me.IsFlying && cur.Phase == MatchPhase.Round)
            {
                // Spectate: watch your falling kite briefly, then a flying kite (spec 9.6: loser spectates 5 s).
                if (SpectateTarget < 0 || !cur.Kites[SpectateTarget].IsFlying)
                {
                    SpectateTarget = -1;
                    for (int i = 0; i < cur.Kites.Length; i++)
                        if (i != local && cur.Kites[i].IsFlying) { SpectateTarget = i; break; }
                }
                if (SpectateTarget >= 0 && me.Status != KiteStatus.Cut) target = SpectateTarget;
            }
            else SpectateTarget = -1;

            Camera.Mode = target == local ? CameraMode.Follow : CameraMode.Spectate;
            Camera.Target = Kites[target].transform.GetChild(0);
            Camera.Anchor = ArenaBuilder.ToUnity(Session.Arena.Anchors[target]);
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
                    if (e.A >= 0) _flyers[e.A].React(true);
                    if (e.B >= 0) _flyers[e.B].React(false);
                    if (e.Code == (int)RoundEndReason.Cut || e.Code == (int)RoundEndReason.SelfSnap) StartReplay();
                    if (e.A == local) Game.Audio?.Play(Sfx.Crowd, 0.6f);
                    break;
                case MatchEventType.MatchEnded:
                    Game.Audio?.Play(cur.Kites.Length > local && local >= 0 && e.A == local ? Sfx.Win : Sfx.Lose);
                    break;
                case MatchEventType.Launched:
                    if (e.A == local) Game.Audio?.Play(Sfx.Whoosh, 0.6f);
                    break;
            }
            EventRaised?.Invoke(e);
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
