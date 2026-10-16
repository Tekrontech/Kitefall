using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>
    /// The whole match, in pure C# (milestones 3–7): kites, wind, round phases, contacts, contests,
    /// cuts, scoring, crates, storm zone, disconnects and results – for every mode.
    ///
    /// Server authority (spec rule 2): the server (or the offline game) creates this from a
    /// <see cref="MatchSetup"/> and calls <see cref="Step"/> 20 times per second with every player's
    /// <see cref="KiteInput"/>. Clients only send inputs and draw snapshots.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        public const float TickRate = 20f;
        public const float Dt = 1f / TickRate;

        public MatchSetup Setup { get; }
        public ModeRules Rules { get; }
        public ArenaLayout Arena { get; }
        public WindModel Wind { get; }
        public RoundPhaseController Phases { get; }
        public ContactDetector Contacts { get; }
        public ContestResolver Resolver { get; }

        public IReadOnlyList<MatchParticipant> Participants => _participants;
        public IReadOnlyList<Contest> ActiveContests => _contests;
        public IReadOnlyList<SkyCrate> Crates => _crates;
        /// <summary>Events raised by the last <see cref="Step"/>.</summary>
        public IReadOnlyList<MatchEvent> Events => _events;

        public int Tick { get; private set; }
        /// <summary>Seconds since the match started (all phases).</summary>
        public float Time { get; private set; }
        /// <summary>Seconds of actual flying (round phases only).</summary>
        public float MatchTime { get; private set; }
        public MatchPhase Phase { get; private set; }
        public float PhaseTimeLeft { get; private set; }
        public int RoundNumber { get; private set; }
        public float RoundTime { get; private set; }
        public WindSample CurrentWind { get; private set; }
        public bool BirdsActive { get; private set; }
        public Vector3 StormCenter { get; private set; }
        public float StormRadius { get; private set; }
        public int[] TeamScores { get; }
        public MatchResult Result { get; private set; }
        public bool IsOver => Phase == MatchPhase.Results;
        public List<RoundRecord> Rounds { get; } = new List<RoundRecord>();

        private readonly List<MatchParticipant> _participants = new List<MatchParticipant>();
        private readonly List<Contest> _contests = new List<Contest>();
        private readonly List<SkyCrate> _crates = new List<SkyCrate>();
        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        private readonly List<ContactEvent> _contactBuffer = new List<ContactEvent>();
        private readonly List<ContestSummary> _contestHistory = new List<ContestSummary>();
        private readonly List<int> _eliminationOrder = new List<int>();
        private readonly Vector3[][] _lines;
        private readonly bool[] _active;
        private readonly DeterministicRandom _rng;
        private readonly DeterministicRandom _crateRng;
        private float _nextCrateAt;
        private float _nextStormShrinkAt;
        private int _crateIds;
        private ContestSummary _lastDecidingContest;

        public MatchSimulation(MatchSetup setup)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Rules = setup.Rules ?? ModeRules.For(GameMode.QuickDuel);
            if (setup.Participants.Count == 0) throw new ArgumentException("A match needs at least one participant.");

            _rng = new DeterministicRandom(setup.Seed);
            _crateRng = new DeterministicRandom(setup.Seed * 31 + 5);
            Arena = ArenaLayout.Generate(setup.Arena, setup.Seed, setup.Participants.Count, setup.UseRoofGraph, Rules.IsDuel, setup.Roofs);

            // Wind pattern randomised per match around the arena's base direction (spec 11).
            var wind = Clone(setup.Wind);
            wind.seed = setup.Seed;
            wind.baseDirectionDeg = Arena.BaseWindDirection + _rng.Range(-20f, 20f);
            wind.baseSpeed *= Arena.WindStrength;
            wind.directionVariationDeg *= Arena.WindVariation;
            Wind = new WindModel(wind);

            Phases = new RoundPhaseController(setup.Phases);
            Contacts = new ContactDetector(setup.Contact, setup.Participants.Count);
            Resolver = new ContestResolver(setup.Contest, new DeterministicRandom(setup.Seed * 17 + 3));
            TeamScores = new int[Math.Max(1, Rules.teamCount)];

            _lines = new Vector3[setup.Participants.Count][];
            _active = new bool[setup.Participants.Count];

            for (int i = 0; i < setup.Participants.Count; i++)
            {
                ParticipantSetup ps = setup.Participants[i];
                if (ps.stack == null || ps.stack.Count == 0) ps.stack = new List<ParticipantLoadout> { setup.FreeGuddi };
                var p = new MatchParticipant
                {
                    Index = i,
                    Setup = ps,
                    LastContestWith = new float[setup.Participants.Count],
                    Result = new ParticipantResult { Index = i, PlayerId = ps.playerId, DisplayName = ps.displayName, IsBot = ps.isBot, Team = ps.team, RankPoints = ps.rankPoints }
                };
                for (int k = 0; k < p.LastContestWith.Length; k++) p.LastContestWith[k] = -999f;
                p.Current = PrepareLoadout(ps.stack[0]);
                p.Kite = NewKite(p, i);
                _participants.Add(p);
                _lines[i] = p.Kite.LinePoints;
            }

            StormCenter = new Vector3(Arena.SkyCenter.X, 0f, Arena.SkyCenter.Z);
            StormRadius = setup.Phases.stormStartRadius;
            CurrentWind = Wind.Sample(0f, Arena.Anchors[0].Y);
            InitUpgrade();
            BeginPreMatch();
        }

        private static WindSettings Clone(WindSettings w) => new WindSettings
        {
            baseDirectionDeg = w.baseDirectionDeg, baseSpeed = w.baseSpeed, directionVariationDeg = w.directionVariationDeg,
            speedVariation = w.speedVariation, variationPeriod = w.variationPeriod, heightGainPer100m = w.heightGainPer100m,
            minSpeed = w.minSpeed, maxSpeed = w.maxSpeed, strongWindSpeed = w.strongWindSpeed, seed = w.seed
        };

        private ParticipantLoadout PrepareLoadout(ParticipantLoadout l)
        {
            if (Rules.startWithBasicGear)
            {
                if (!string.IsNullOrEmpty(Setup.BasicDor.id)) { l.dor = Setup.BasicDor; l.dorId = Setup.BasicDor.id; }
                if (!string.IsNullOrEmpty(Setup.BasicCharkhi.id)) { l.charkhi = Setup.BasicCharkhi; l.charkhiId = Setup.BasicCharkhi.id; }
                l.tunings = null;
            }
            if (l.durability <= 0f) l.durability = 100f;
            if (l.dorStockMeters <= 0f) l.dorStockMeters = 600f;
            return l;
        }

        private KiteSimulation NewKite(MatchParticipant p, int index)
        {
            var kite = new KiteSimulation(p.Current.Compose(), Setup.Tension, Setup.Flight, Setup.Line,
                Arena.Anchors[index], Arena.GroundHeight, p.Current.dorStockMeters, Setup.DheelOnPullRelease, Setup.Seed + index * 101)
            {
                Durability = p.Current.durability,
                IsSolid = Arena.IsSolid
            };
            kite.UpdateLine();
            return kite;
        }

        // =====================================================================
        // Public control
        // =====================================================================

        /// <summary>Advances the match by one tick (1/20 s). <paramref name="inputs"/> has one entry per participant.</summary>
        public void Step(IReadOnlyList<KiteInput> inputs)
        {
            _events.Clear();
            if (Phase == MatchPhase.Results) return;
            Tick++;
            Time += Dt;
            ProcessCommands(inputs);

            switch (Phase)
            {
                case MatchPhase.Warmup:
                    StepWarmup(inputs);
                    break;
                case MatchPhase.RoofSelect:
                    StepRoofSelect();
                    break;
                case MatchPhase.Prep:
                    StepPrepPhase(inputs);
                    break;
                case MatchPhase.Intro:
                    PhaseTimeLeft -= Dt;
                    if (PhaseTimeLeft <= 0f) BeginLaunch();
                    break;
                case MatchPhase.Launch:
                    StepLaunch(inputs);
                    PhaseTimeLeft -= Dt;
                    if (PhaseTimeLeft <= 0f) StartRound();
                    break;
                case MatchPhase.Round:
                    StepRound(inputs);
                    break;
                case MatchPhase.RoundEnd:
                    PhaseTimeLeft -= Dt;
                    if (PhaseTimeLeft <= 0f) AfterRoundEnd();
                    break;
                case MatchPhase.LoadoutBreak:
                    PhaseTimeLeft -= Dt;
                    if (PhaseTimeLeft <= 0f) BeginLaunch();
                    break;
            }
        }

        /// <summary>Loadout break (spec 5.1): may change dum and charkhi, not kite class or dor.</summary>
        public bool TryChangeLoadout(int index, TailStats tail, string tailId, CharkhiStats charkhi, string charkhiId)
        {
            if (Phase != MatchPhase.LoadoutBreak || index < 0 || index >= _participants.Count) return false;
            MatchParticipant p = _participants[index];
            p.Current.tail = tail;
            p.Current.tailId = tailId;
            p.Current.charkhi = charkhi;
            p.Current.charkhiId = charkhiId;
            p.Kite.ChangeLoadout(p.Current.Compose());
            if (!p.Result.CharkhisUsed.Contains(charkhiId)) p.Result.CharkhisUsed.Add(charkhiId);
            return true;
        }

        public void SetConnected(int index, bool connected)
        {
            MatchParticipant p = _participants[index];
            if (p.Connected == connected) return;
            p.Connected = connected;
            if (connected) { p.DisconnectedFor = 0f; p.RoundForfeitApplied = false; }
            Raise(connected ? MatchEventType.Reconnected : MatchEventType.Disconnected, index);
        }

        /// <summary>Player quits (spec 12.3: Ranked quit = loss).</summary>
        public void Forfeit(int index)
        {
            if (IsOver) return;
            MatchParticipant p = _participants[index];
            if (p.Eliminated && !Rules.IsDuel && !Rules.respawn)
            {
                // Already out of Last Kite Flying: leaving keeps the placement and rewards (upgrade F5).
                p.Connected = false;
                return;
            }
            p.Quit = true;
            p.Result.Quit = true;
            p.Eliminated = true;
            p.Flyer = FlyerState.Out;
            if (p.Kite.IsFlying) p.Kite.EndFlight(KiteStatus.Crashed);
            CancelContestsOf(index);
            if (!_eliminationOrder.Contains(index)) _eliminationOrder.Add(index);

            int remaining = 0, last = -1;
            foreach (MatchParticipant q in _participants)
                if (!q.Quit) { remaining++; last = q.Index; }
            if (Rules.IsDuel || remaining <= 1) EndMatch(remaining == 1 ? last : -1);
        }

        /// <summary>Ends the match now (tutorial / private room host).</summary>
        public void EndMatch() => EndMatch(-2);

        // =====================================================================
        // Phases
        // =====================================================================

        private void EnterPhase(MatchPhase phase, float duration)
        {
            Phase = phase;
            PhaseTimeLeft = duration;
            Raise(MatchEventType.PhaseChanged, -1, -1, duration, (int)phase);
        }

        private void PrepareLaunch()
        {
            foreach (MatchParticipant p in _participants)
            {
                p.LaunchHold = 0f;
                p.LaunchHeldPrev = false;
                p.LaunchDone = false;
                p.LaunchBonus = 0f;
                if (p.Eliminated) continue;
                p.Flyer = FlyerState.OnRoof;

                if (Rules.IsDuel)
                {
                    // Each duel round flies a fresh kite of the same loadout; a survivor keeps its damage.
                    float durability = p.NextRoundDurability >= 0f ? p.NextRoundDurability : p.Current.durability;
                    p.Kite = NewKite(p, p.Index);
                    p.Kite.Durability = durability;
                    _lines[p.Index] = p.Kite.LinePoints;
                }
                p.Kite.SetWaiting();
            }
        }

        private void StepLaunch(IReadOnlyList<KiteInput> inputs)
        {
            PhaseSettings ps = Setup.Phases;
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                if (p.LaunchDone || p.Eliminated) continue;
                bool held = i < inputs.Count && inputs[i].PullHeld;
                if (held) p.LaunchHold += Dt;
                if (p.LaunchHeldPrev && !held)
                {
                    p.LaunchDone = true;
                    if (p.LaunchHold >= ps.launchPerfectMin && p.LaunchHold <= ps.launchPerfectMax)
                    {
                        p.LaunchBonus = ps.launchBonusHeight;
                        Raise(MatchEventType.PerfectLaunch, i);
                    }
                }
                if (p.LaunchHold > ps.launchOverfill) p.LaunchDone = true;
                p.LaunchHeldPrev = held;
            }
        }

        private void StartRound()
        {
            RoundNumber++;
            RoundTime = 0f;
            EnterPhase(MatchPhase.Round, Rules.IsDuel ? Rules.roundMaxTime : Rules.matchTime);
            Phases.Reset(Setup.Seed + RoundNumber * 977, Rules.useStorm);
            _contests.Clear();
            Contacts.ResetAll();
            if (Rules.crateInterval > 0f) _nextCrateAt = Rules.crateInterval;
            if (Rules.stormZone) _nextStormShrinkAt = Rules.stormShrinkInterval;

            foreach (MatchParticipant p in _participants)
            {
                if (p.Eliminated || p.Kite.IsFlying) continue;
                if (p.Prep != null && p.Flyer == FlyerState.Prep)
                {
                    // Ritual not finished at the cap: auto-complete with "Good" results (spec 11.2).
                    p.Prep.AutoComplete(Time);
                    LaunchFromPrep(p);
                    continue;
                }
                p.LaunchDelayLeft = p.Kite.Loadout.LaunchDelay;
                if (p.LaunchDelayLeft <= 0f) LaunchKite(p);
            }
        }

        private void LaunchKite(MatchParticipant p)
        {
            LaunchWith(p, p.LaunchBonus + TraitStartBonus(p));
            p.LaunchBonus = 0f;
        }

        private void LaunchWith(MatchParticipant p, float bonusHeight)
        {
            WindSample w = SampleWind(MatchTime, p.Kite.Anchor.Y);
            // Spread kites a little so duel kites do not start on top of each other.
            float spread = (p.Index - (_participants.Count - 1) * 0.5f) * (_participants.Count <= 2 ? 6f : 3f);
            if (Arena.Roofs != null) spread = 0f; // separate roofs already spread the kites
            p.Kite.Reset(w.DirectionDeg, bonusHeight, spread);
            p.Flyer = FlyerState.Flying;
            OnLaunchedFromRoof(p);
            if (!p.Result.DorsUsed.Contains(p.Current.dorId)) p.Result.DorsUsed.Add(p.Current.dorId);
            if (!p.Result.CharkhisUsed.Contains(p.Current.charkhiId)) p.Result.CharkhisUsed.Add(p.Current.charkhiId);
            Raise(MatchEventType.Launched, p.Index);
        }

        private void AfterRoundEnd()
        {
            RoundRecord last = Rounds.Count > 0 ? Rounds[Rounds.Count - 1] : null;
            foreach (MatchParticipant p in _participants)
            {
                if (p.RoundsWon >= Rules.roundsToWin) { EndMatch(p.Index); return; }
            }
            if (RoundNumber >= Rules.maxRounds)
            {
                int best = -1, bestRounds = -1; bool tie = false;
                foreach (MatchParticipant p in _participants)
                {
                    if (p.RoundsWon > bestRounds) { best = p.Index; bestRounds = p.RoundsWon; tie = false; }
                    else if (p.RoundsWon == bestRounds) tie = true;
                }
                EndMatch(tie ? -1 : best);
                return;
            }
            if (last != null && Rules.loadoutBreakTime > 0f) EnterPhase(MatchPhase.LoadoutBreak, Rules.loadoutBreakTime);
            else BeginLaunch();
        }

        // =====================================================================
        // Round
        // =====================================================================

        private void StepRound(IReadOnlyList<KiteInput> inputs)
        {
            RoundTime += Dt;
            MatchTime += Dt;
            PhaseTimeLeft -= Dt;

            Phases.Step(RoundTime, out bool phaseChanged, out bool gustWarn, out bool gustStart, out bool arenaEvent);
            if (phaseChanged) Raise(MatchEventType.RoundPhaseChanged, -1, -1, 0f, (int)Phases.Phase);
            if (gustWarn) Raise(MatchEventType.GustWarning);
            if (gustStart) Raise(MatchEventType.GustStarted);
            if (arenaEvent) Raise(MatchEventType.ArenaEvent, -1, -1, 0f, (int)Arena.Type);
            Wind.SpeedMultiplier = TrainingWindMultiplier(Phases.WindMultiplier) * (Phases.ArenaEventActive && Arena.Type == ArenaType.CoastalCliffs ? 1.3f : 1f);
            BirdsActive = Phases.ArenaEventActive && Arena.Type == ArenaType.OldCityRooftops;
            CurrentWind = SampleWind(MatchTime, Arena.Anchors[0].Y);

            // ---- Kites
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                if (p.Eliminated) continue;
                if (StepFlyerUpgrade(p, i, inputs)) continue;
                KiteSimulation kite = p.Kite;

                if (kite.Status == KiteStatus.Waiting)
                {
                    if (p.LaunchDelayLeft > 0f)
                    {
                        p.LaunchDelayLeft -= Dt;
                        if (p.LaunchDelayLeft <= 0f) LaunchKite(p);
                    }
                    else if (Rules.respawn && p.RespawnTimer > 0f)
                    {
                        p.RespawnTimer -= Dt;
                        if (p.RespawnTimer <= 0f) Respawn(p);
                    }
                    continue;
                }
                if (!kite.IsFlying)
                {
                    if (Rules.respawn && p.RespawnTimer > 0f)
                    {
                        p.RespawnTimer -= Dt;
                        if (p.RespawnTimer <= 0f) Respawn(p);
                    }
                    continue;
                }

                KiteInput input = !p.Connected ? HoldInput(p) : (i < inputs.Count ? inputs[i] : default);
                if (p.Flyer == FlyerState.PackingUp) input = PackUpInput(p);
                if (BirdsActive)
                {
                    // Birds shake the lines for 2 s (spec 6.1).
                    input.SteerX += KMath.Sin(Time * 23f + i) * 0.6f;
                    input.SteerY += KMath.Cos(Time * 19f + i * 2f) * 0.6f;
                }

                kite.Tension.Floor = DheelFloorFor(i);
                WindSample w = KiteWind(p, SampleWind(MatchTime, kite.Flight.Height));
                KiteStepResult r = kite.Step(input, w, MatchTime, Dt);
                if (r.DiveStarted) Raise(MatchEventType.DiveStarted, i);
                if (p.Flyer == FlyerState.PackingUp) StepPackUp(p);

                if (Rules.stormZone && kite.IsFlying)
                {
                    Vector3 pos = kite.WorldPosition;
                    float dx = pos.X - StormCenter.X, dz = pos.Z - StormCenter.Z;
                    if (dx * dx + dz * dz > StormRadius * StormRadius)
                        kite.Durability = KMath.Max(0f, kite.Durability - Setup.Phases.stormDamagePerSecond * Dt);
                }
            }

            UpdateContests();
            HandleFlightEnds();
            if (Phase != MatchPhase.Round) return;

            DetectContacts();
            UpdateCrates();
            UpdateStorm();
            UpdateDisconnects();
            CheckRoundOrMatchEnd();
        }

        private KiteInput HoldInput(MatchParticipant p)
        {
            // "AI hold" while disconnected (spec 14.2): keep the kite steady, nose up, tension near idle.
            KiteSimulation k = p.Kite;
            return new KiteInput
            {
                SteerX = 0f,
                SteerY = k.Flight.ElevationDeg < 40f ? 0.6f : 0f,
                PullHeld = k.Tension.Tension < 38f,
                ReelHeld = k.CurrentAction == LineAction.Dheel
            };
        }

        private float DheelFloorFor(int kite)
        {
            foreach (Contest c in _contests)
            {
                if (!c.Involves(kite) || c.IsOver) continue;
                if (c.AttackOf(kite) == AttackType.Dheel)
                {
                    int floor = _participants[kite].Kite.Loadout.DheelTensionFloor;
                    if (floor > 0) return floor;
                }
            }
            return 0f;
        }

        private void Respawn(MatchParticipant p)
        {
            p.StackIndex++;
            ParticipantLoadout next = p.StackIndex < p.Setup.stack.Count ? p.Setup.stack[p.StackIndex] : Setup.FreeGuddi;
            p.Current = PrepareLoadout(next);
            p.Kite = NewKite(p, p.Index);
            _lines[p.Index] = p.Kite.LinePoints;
            Raise(MatchEventType.Respawned, p.Index);
            if (Setup.UseLaunchRitual) StartShortPrep(p, false); // short prep replaces the 5 s respawn launch (spec 11.5)
            else LaunchKite(p);
        }

        // =====================================================================
        // Contacts & contests
        // =====================================================================

        private bool CanContact(int a, int b)
        {
            if (Rules.teamCount > 1 && _participants[a].Team == _participants[b].Team) return false;
            return true;
        }

        private void DetectContacts()
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                _active[i] = _participants[i].Kite.IsFlying;
                _lines[i] = _participants[i].Kite.LinePoints;
            }
            _contactBuffer.Clear();
            Contacts.Step(Time, _lines, _active, CanContact, _contactBuffer);
            foreach (ContactEvent e in _contactBuffer) StartContest(e);
        }

        private void StartContest(in ContactEvent e)
        {
            var c = new Contest
            {
                A = e.A,
                B = e.B,
                StartTime = Time,
                AngleDeg = e.AngleDeg,
                Point = e.Point,
                AlongA = e.AlongA,
                AlongB = e.AlongB,
                SweetPhaseA = _rng.NextFloat(),
                SweetPhaseB = _rng.NextFloat()
            };
            _contests.Add(c);
            TryLock(c, true);
            TryLock(c, false);
            Raise(MatchEventType.ContactStarted, e.A, e.B, e.AngleDeg, 0, e.Point);
        }

        /// <summary>Attack type locks from the first input within 0.3 s of contact; none = Hold (spec 9.3).</summary>
        private void TryLock(Contest c, bool sideA)
        {
            if (sideA ? c.LockedA : c.LockedB) return;
            int k = sideA ? c.A : c.B;
            KiteSimulation kite = _participants[k].Kite;
            ContestSettings s = Setup.Contest;

            AttackType? attack = null;
            KiteInput last = kite.LastInput;
            bool recentDive = kite.TimeSinceDive - c.Elapsed <= s.diveWindowBeforeContact;
            bool steerDownPull = last.SteerY < -0.7f && last.PullHeld;
            if (recentDive || steerDownPull) attack = AttackType.Dive;
            else if (kite.CurrentAction == LineAction.Pull) attack = AttackType.Kheench;
            else if (kite.CurrentAction == LineAction.Dheel) attack = AttackType.Dheel;
            else if (c.Elapsed >= s.attackLockWindow - 1e-4f) attack = AttackType.Hold;

            if (attack == null) return;
            if (sideA) { c.AttackA = attack.Value; c.LockedA = true; }
            else { c.AttackB = attack.Value; c.LockedB = true; }
            Raise(MatchEventType.AttackLocked, k, c.Other(k), 0f, (int)attack.Value);
        }

        private int ContestCount(int kite)
        {
            int n = 0;
            foreach (Contest c in _contests) if (!c.IsOver && c.Involves(kite)) n++;
            return n;
        }

        private ContestantView ViewOf(Contest c, int kite)
        {
            MatchParticipant p = _participants[kite];
            KiteSimulation k = p.Kite;
            int width = k.Loadout.SweetZone - (Phases.Phase == RoundPhase.Storm ? Setup.SweetZone.stormWidthPenalty : 0);
            float phase = kite == c.A ? c.SweetPhaseA : c.SweetPhaseB;
            return new ContestantView
            {
                ClassBase = k.Loadout.ClassBase,
                DorCut = k.Loadout.DorCut,
                DorSpeed = k.Loadout.DorSpeed,
                DheelCounterBonus = k.Loadout.DheelCounterBonus,
                LongRangeAngleBonus = k.Loadout.LongRangeAngleBonus,
                KheenchPowerMult = k.Loadout.KheenchPowerMult,
                Tension = k.Tension.Tension,
                Sweet = SweetZoneBand.At(Setup.SweetZone, c.Elapsed, phase, width),
                Height = k.Flight.Height,
                LineOut = k.Spool.LineOut,
                Durability = k.Durability,
                KitePosition = k.WorldPosition,
                ContestCount = ContestCount(kite),
                WindBonus = MatchTime < p.UpwindBonusUntil ? 1f + Setup.Roofs.upwindWindBonus : 0f
            };
        }

        /// <summary>The sweet zone band a kite sees right now (HUD), or null when not in a contest.</summary>
        public SweetZoneBand? SweetZoneFor(int kite)
        {
            foreach (Contest c in _contests)
                if (!c.IsOver && c.Involves(kite)) return ViewOf(c, kite).Sweet;
            return null;
        }

        /// <summary>The contest a kite is in (first one), or null.</summary>
        public Contest ContestOf(int kite)
        {
            foreach (Contest c in _contests)
                if (!c.IsOver && c.Involves(kite)) return c;
            return null;
        }

        private void UpdateContests()
        {
            var ctx = new ContestContext
            {
                Storm = Phases.Phase == RoundPhase.Storm,
                Gust = Phases.GustActive,
                WindDirection = new Vector3(CurrentWind.DirX, 0f, CurrentWind.DirZ)
            };

            for (int i = 0; i < _contests.Count; i++)
            {
                Contest c = _contests[i];
                if (c.IsOver) continue;
                MatchParticipant pa = _participants[c.A], pb = _participants[c.B];

                // Self-snap overrides the meter.
                if (pa.Kite.Status == KiteStatus.SelfSnapped) { c.Outcome = ContestOutcome.SnapA; continue; }
                if (pb.Kite.Status == KiteStatus.SelfSnapped) { c.Outcome = ContestOutcome.SnapB; continue; }
                if (!pa.Kite.IsFlying || !pb.Kite.IsFlying) { c.Outcome = ContestOutcome.Cancelled; continue; }

                // Disconnect during a contest: freeze up to 3 s, then the connected player wins (spec 14.2).
                if (!pa.Connected || !pb.Connected)
                {
                    MatchParticipant gone = !pa.Connected ? pa : pb;
                    if (gone.DisconnectedFor >= Setup.Phases.disconnectContestFreeze)
                        c.Outcome = gone == pa ? ContestOutcome.BWins : ContestOutcome.AWins;
                    continue;
                }

                TryLock(c, true);
                TryLock(c, false);
                ContestantView va = ViewOf(c, c.A), vb = ViewOf(c, c.B);
                Resolver.Tick(c, va, vb, ctx, Dt);
                pa.ContestTicks++; pb.ContestTicks++;
                if (va.Sweet.Contains(va.Tension)) pa.SweetTicks++;
                if (vb.Sweet.Contains(vb.Tension)) pb.SweetTicks++;
            }

            // Resolve finished contests (cuts first, so a cut kite cannot also win elsewhere this tick).
            for (int i = 0; i < _contests.Count; i++)
            {
                Contest c = _contests[i];
                if (c.IsOver) FinishContest(c);
            }
            _contests.RemoveAll(c => c.IsOver);
        }

        private void FinishContest(Contest c)
        {
            MatchParticipant pa = _participants[c.A], pb = _participants[c.B];
            pa.Kite.Tension.Floor = 0f;
            pb.Kite.Tension.Floor = 0f;
            pa.LastContestWith[c.B] = MatchTime;
            pb.LastContestWith[c.A] = MatchTime;

            if (Rules.durabilityLoss && c.Outcome != ContestOutcome.Cancelled)
            {
                pa.Kite.Durability = KMath.Max(0f, pa.Kite.Durability - Resolver.DurabilityLoss(c.StatsA, c.AttackA, c.Outcome));
                pb.Kite.Durability = KMath.Max(0f, pb.Kite.Durability - Resolver.DurabilityLoss(c.StatsB, c.AttackB, c.Outcome));
            }

            ContestSummary summary = ContestSummary.From(c, MatchTime);
            if (c.Outcome != ContestOutcome.Cancelled) _contestHistory.Add(summary);
            Contacts.Release(c.A, c.B, Time, c.Outcome == ContestOutcome.Neutral);
            Raise(MatchEventType.ContestEnded, c.A, c.B, c.M, (int)c.Outcome, c.Point);

            if (c.Outcome == ContestOutcome.AWins || c.Outcome == ContestOutcome.BWins)
            {
                int winner = c.Winner, loser = c.Loser;
                _participants[winner].ContestsWon++;
                _participants[loser].ContestsLost++;
                _lastDecidingContest = summary;
                float along = loser == c.A ? c.AlongA : c.AlongB;
                CutKite(loser, winner, along, c.Point);
            }
            else if (c.Outcome == ContestOutcome.SnapA || c.Outcome == ContestOutcome.SnapB)
            {
                _lastDecidingContest = summary;
            }
        }

        private void CutKite(int victim, int by, float alongLine, Vector3 point)
        {
            MatchParticipant v = _participants[victim];
            if (!v.Kite.IsFlying) return;
            float lost = KMath.Max(0f, v.Kite.Spool.LineOut - alongLine);
            v.Kite.EndFlight(KiteStatus.Cut);
            CancelContestsOf(victim);
            Raise(MatchEventType.KiteCut, victim, by, lost, 0, point);

            if (Rules.lossMode)
            {
                v.Result.KitesLost.Add(v.Current.kiteId);
                Add(v.Result.DorLost, v.Current.dorId, lost);
            }
            else if (Rules.durabilityLoss)
            {
                v.Kite.Durability = KMath.Max(0f, v.Kite.Durability - Setup.Phases.cutDurabilityLossNoLossMode);
            }

            MatchParticipant k = by >= 0 ? _participants[by] : null;
            if (k != null)
            {
                k.Cuts++;
                if (!Rules.IsDuel)
                {
                    k.Score += Rules.scoreCut;
                    Raise(MatchEventType.ScoreChanged, by, victim, Rules.scoreCut);
                    if (Rules.teamCount > 1)
                    {
                        int value = v.Setup.captain ? Rules.captainCutValue : 1;
                        TeamScores[k.Team] += value;
                    }
                    // Assists: anyone else who contested the victim in the last 3 s.
                    foreach (MatchParticipant o in _participants)
                    {
                        if (o.Index == by || o.Index == victim) continue;
                        if (MatchTime - o.LastContestWith[victim] <= Rules.assistWindow)
                        {
                            o.Assists++;
                            o.Score += Rules.scoreAssist;
                            Raise(MatchEventType.Assist, o.Index, victim, Rules.scoreAssist);
                        }
                    }
                }
            }
        }

        private void CancelContestsOf(int kite)
        {
            foreach (Contest c in _contests)
            {
                if (c.IsOver || !c.Involves(kite)) continue;
                c.Outcome = ContestOutcome.Cancelled;
                Contacts.Release(c.A, c.B, Time, false);
            }
        }

        /// <summary>Kites that stopped flying this tick (snap, crash, cut) – scoring and respawn.</summary>
        private void HandleFlightEnds()
        {
            int ended = 0, cutBy = -1, endedIndex = -1, endedCount = 0;
            RoundEndReason reason = RoundEndReason.Crash;
            foreach (MatchParticipant p in _participants)
            {
                KiteSimulation k = p.Kite;
                if (p.Eliminated || k.Status == KiteStatus.Flying || k.Status == KiteStatus.Waiting) continue;
                if (ReferenceEquals(p.EndHandledFor, k)) continue;
                p.EndHandledFor = k;

                endedCount++;
                endedIndex = p.Index;
                AccountDorUsed(p);

                if (k.Status == KiteStatus.SelfSnapped)
                {
                    p.SelfSnaps++;
                    reason = RoundEndReason.SelfSnap;
                    Raise(MatchEventType.SelfSnap, p.Index, -1, k.Tension.Strength, 0, k.WorldPosition);
                    if (!Rules.IsDuel)
                    {
                        p.Score += Rules.scoreSelfSnap;
                        Raise(MatchEventType.ScoreChanged, p.Index, -1, Rules.scoreSelfSnap);
                    }
                    if (Rules.lossMode) p.Result.KitesLost.Add(p.Current.kiteId);
                }
                else if (k.Status == KiteStatus.Crashed)
                {
                    p.Crashes++;
                    reason = RoundEndReason.Crash;
                    k.Durability = KMath.Max(0f, k.Durability - Setup.Phases.crashDurabilityLoss);
                    Raise(MatchEventType.Crash, p.Index, -1, k.Durability, 0, k.WorldPosition);
                    if (k.Durability <= 0f && Rules.lossMode) p.Result.KitesLost.Add(p.Current.kiteId);
                }
                else if (k.Status == KiteStatus.Cut)
                {
                    reason = RoundEndReason.Cut;
                    cutBy = FindCutter(p.Index);
                }
                ended++;
                p.Result.DurabilityEnd[p.Current.kiteId] = k.Durability;
                p.Flyer = FlyerState.OnRoof;

                if (!Rules.IsDuel)
                {
                    if (Rules.respawn) p.RespawnTimer = Rules.respawnDelay;
                    else
                    {
                        p.Eliminated = true;
                        p.Flyer = FlyerState.Out;
                        _eliminationOrder.Add(p.Index);
                        Raise(MatchEventType.Eliminated, p.Index);
                    }
                }
            }

            if (Rules.IsDuel && ended > 0)
            {
                if (endedCount >= 2)
                {
                    EndRound(-1, -1, reason);
                }
                else
                {
                    int winner = reason == RoundEndReason.Cut && cutBy >= 0 ? cutBy : OtherDuelist(endedIndex);
                    EndRound(winner, endedIndex, reason);
                }
            }
        }

        private int FindCutter(int victim)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
                if (_events[i].Type == MatchEventType.KiteCut && _events[i].A == victim) return _events[i].B;
            return -1;
        }

        private int OtherDuelist(int index)
        {
            foreach (MatchParticipant p in _participants)
                if (p.Index != index && !p.Quit) return p.Index;
            return -1;
        }

        private void AccountDorUsed(MatchParticipant p)
        {
            float used = p.Kite.MaxLineOutReached * Setup.Phases.dorUsedFractionOfMaxLine;
            Add(p.Result.DorUsed, p.Current.dorId, used);
        }

        private static void Add(Dictionary<string, float> map, string key, float value)
        {
            if (string.IsNullOrEmpty(key)) key = "unknown";
            map.TryGetValue(key, out float v);
            map[key] = v + value;
        }

        private void EndRound(int winner, int loser, RoundEndReason reason)
        {
            var record = new RoundRecord
            {
                Number = RoundNumber,
                Winner = winner,
                Loser = loser,
                Reason = reason,
                Duration = RoundTime,
                Deciding = reason == RoundEndReason.Cut || reason == RoundEndReason.SelfSnap ? _lastDecidingContest : null
            };
            Rounds.Add(record);
            _lastDecidingContest = null;
            if (winner >= 0) _participants[winner].RoundsWon++;

            foreach (MatchParticipant p in _participants)
            {
                if (p.Kite.IsFlying)
                {
                    AccountDorUsed(p);
                    p.Result.DurabilityEnd[p.Current.kiteId] = p.Kite.Durability;
                }
                // Survivors keep their damaged kite for the next round; lost kites are replaced by a fresh one.
                p.NextRoundDurability = p.Kite.IsFlying ? p.Kite.Durability : (Rules.lossMode ? p.Current.durability : p.Kite.Durability);
            }
            foreach (Contest c in _contests) c.Outcome = ContestOutcome.Cancelled;
            _contests.Clear();
            Raise(MatchEventType.RoundEnded, winner, loser, RoundNumber, (int)reason);
            EnterPhase(MatchPhase.RoundEnd, Rules.roundEndTime);
        }

        private void CheckRoundOrMatchEnd()
        {
            if (Rules.IsDuel)
            {
                if (RoundTime >= Rules.roundMaxTime - 1e-4f) EndRoundOnTimer();
                return;
            }

            if (Rules.teamCount > 1)
            {
                for (int t = 0; t < TeamScores.Length; t++)
                    if (Rules.teamCutsToWin > 0 && TeamScores[t] >= Rules.teamCutsToWin) { EndMatch(-1); return; }
            }

            if (Rules.stormZone || !Rules.respawn)
            {
                int alive = 0;
                foreach (MatchParticipant p in _participants) if (!p.Eliminated) alive++;
                if (alive <= 1 && _participants.Count > 1) { EndMatch(-1); return; }
            }

            if (Rules.matchTime > 0f && MatchTime >= Rules.matchTime - 1e-4f) EndMatch(-1);
        }

        /// <summary>Timer tiebreak (spec 6.2): higher kite → more durability → draw.</summary>
        private void EndRoundOnTimer()
        {
            MatchParticipant a = null, b = null;
            foreach (MatchParticipant p in _participants)
            {
                if (p.Quit) continue;
                if (a == null) a = p; else if (b == null) b = p;
            }
            if (a == null || b == null) { EndRound(a?.Index ?? -1, -1, RoundEndReason.Timer); return; }

            float ha = a.Kite.IsFlying ? a.Kite.Flight.Height : -1f;
            float hb = b.Kite.IsFlying ? b.Kite.Flight.Height : -1f;
            if (KMath.Abs(ha - hb) > 0.5f) { EndRound(ha > hb ? a.Index : b.Index, ha > hb ? b.Index : a.Index, RoundEndReason.Timer); return; }
            float da = a.Kite.Durability, db = b.Kite.Durability;
            if (KMath.Abs(da - db) > 0.5f) { EndRound(da > db ? a.Index : b.Index, da > db ? b.Index : a.Index, RoundEndReason.Timer); return; }
            EndRound(-1, -1, RoundEndReason.Timer);
        }

        // =====================================================================
        // Crates, storm zone, disconnects
        // =====================================================================

        private void UpdateCrates()
        {
            if (Rules.crateInterval <= 0f) return;
            CrateSettings cs = Setup.Crates;

            if (MatchTime >= _nextCrateAt)
            {
                _nextCrateAt += Rules.crateInterval;
                Vector3 c = Arena.SkyCenter;
                Vector3 pos = new Vector3(c.X + _crateRng.Range(-70f, 70f), _crateRng.Range(cs.minHeight, cs.maxHeight), c.Z + _crateRng.Range(-60f, 60f));
                if (_crateIds == 0 && CrateSpotPosition(cs, out Vector3 spot)) pos = spot;
                var crate = new SkyCrate
                {
                    Id = ++_crateIds,
                    Position = pos,
                    Content = (CrateContent)_crateRng.Range(0, 4),
                    SpawnTime = MatchTime,
                    Progress = new float[_participants.Count]
                };
                _crates.Add(crate);
                Raise(MatchEventType.CrateSpawned, -1, -1, crate.Id, (int)crate.Content, crate.Position);
            }

            float r2 = cs.collectRadius * cs.collectRadius;
            foreach (SkyCrate crate in _crates)
            {
                if (crate.Taken) continue;
                for (int i = 0; i < _participants.Count; i++)
                {
                    MatchParticipant p = _participants[i];
                    bool inRange = p.Kite.IsFlying && Vector3.DistanceSquared(p.Kite.WorldPosition, crate.Position) <= r2;
                    crate.Progress[i] = inRange ? crate.Progress[i] + Dt : KMath.Max(0f, crate.Progress[i] - Dt);
                    if (crate.Progress[i] >= cs.collectTime)
                    {
                        crate.Taken = true;
                        crate.TakenBy = i;
                        ApplyCrate(p, crate.Content);
                        Raise(MatchEventType.CrateCollected, i, -1, crate.Id, (int)crate.Content, crate.Position);
                        break;
                    }
                }
            }
            _crates.RemoveAll(c => c.Taken || MatchTime - c.SpawnTime > cs.lifetime);
        }

        private void ApplyCrate(MatchParticipant p, CrateContent content)
        {
            switch (content)
            {
                case CrateContent.BetterDor:
                    if (Setup.CrateDors.Count > 0)
                    {
                        DorStats d = Setup.CrateDors[_crateRng.Range(0, Setup.CrateDors.Count)];
                        p.Current.dor = d;
                        p.Current.dorId = d.id;
                        p.Kite.ChangeLoadout(p.Current.Compose());
                    }
                    break;
                case CrateContent.BetterCharkhi:
                    if (Setup.CrateCharkhis.Count > 0)
                    {
                        CharkhiStats ch = Setup.CrateCharkhis[_crateRng.Range(0, Setup.CrateCharkhis.Count)];
                        p.Current.charkhi = ch;
                        p.Current.charkhiId = ch.id;
                        p.Kite.ChangeLoadout(p.Current.Compose());
                    }
                    break;
                case CrateContent.PatchKit:
                    p.Kite.Durability = KMath.Min(100f, p.Kite.Durability + Setup.Crates.patchKitDurability);
                    break;
                case CrateContent.DorRefill:
                    p.Kite.AddStock(Setup.Crates.dorRefillMeters);
                    break;
            }
        }

        private void UpdateStorm()
        {
            if (!Rules.stormZone) return;
            if (MatchTime >= _nextStormShrinkAt)
            {
                _nextStormShrinkAt += Rules.stormShrinkInterval;
                StormRadius = KMath.Max(Setup.Phases.stormMinRadius, StormRadius * Setup.Phases.stormShrinkFactor);
                Raise(MatchEventType.StormShrink, -1, -1, StormRadius);
            }
        }

        private void UpdateDisconnects()
        {
            foreach (MatchParticipant p in _participants)
            {
                if (p.Connected || p.Quit) continue;
                p.DisconnectedFor += Dt;
                if (p.DisconnectedFor >= Setup.Phases.disconnectMatchForfeit) { Forfeit(p.Index); return; }
                if (Rules.IsDuel && !p.RoundForfeitApplied && p.DisconnectedFor >= Setup.Phases.disconnectRoundForfeit && ContestOf(p.Index) == null)
                {
                    p.RoundForfeitApplied = true;
                    if (p.Kite.IsFlying) p.Kite.EndFlight(KiteStatus.Crashed);
                    EndRound(OtherDuelist(p.Index), p.Index, RoundEndReason.Forfeit);
                    return;
                }
            }
        }

        // =====================================================================
        // End
        // =====================================================================

        private void EndMatch(int winner)
        {
            if (Phase == MatchPhase.Results) return;
            foreach (MatchParticipant p in _participants)
            {
                if (p.Kite.IsFlying) AccountDorUsed(p);
                if (!p.Result.DurabilityEnd.ContainsKey(p.Current.kiteId) || p.Kite.IsFlying)
                    p.Result.DurabilityEnd[p.Current.kiteId] = p.Kite.Durability;
            }
            Result = BuildResult(winner);
            EnterPhase(MatchPhase.Results, 0f);
            Raise(MatchEventType.MatchEnded, Result.WinnerIndex, -1, 0f, Result.WinningTeam);
        }

        private MatchResult BuildResult(int winnerHint)
        {
            var result = new MatchResult
            {
                Mode = Rules.mode,
                Seed = Setup.Seed,
                Duration = Time,
                Rounds = new List<RoundRecord>(Rounds),
                Contests = new List<ContestSummary>(_contestHistory),
                TeamScores = (int[])TeamScores.Clone()
            };

            var order = new List<MatchParticipant>(_participants);
            if (Rules.IsDuel)
            {
                order.Sort((x, y) =>
                {
                    if (x.Quit != y.Quit) return x.Quit ? 1 : -1;
                    int r = y.RoundsWon.CompareTo(x.RoundsWon);
                    return r != 0 ? r : x.Index.CompareTo(y.Index);
                });
                if (winnerHint >= 0) result.WinnerIndex = winnerHint;
                else if (winnerHint == -1 && order.Count > 1 && order[0].RoundsWon == order[1].RoundsWon && !order[1].Quit) result.Draw = true;
                else result.WinnerIndex = order[0].Index;
            }
            else if (Rules.teamCount > 1)
            {
                int bestTeam = 0; bool tie = false;
                for (int t = 1; t < TeamScores.Length; t++)
                {
                    if (TeamScores[t] > TeamScores[bestTeam]) { bestTeam = t; tie = false; }
                    else if (TeamScores[t] == TeamScores[bestTeam]) tie = true;
                }
                result.WinningTeam = tie ? -1 : bestTeam;
                result.Draw = tie;
                order.Sort((x, y) =>
                {
                    int t = (y.Team == bestTeam).CompareTo(x.Team == bestTeam);
                    if (t != 0) return t;
                    int s = y.Score.CompareTo(x.Score);
                    return s != 0 ? s : y.Cuts.CompareTo(x.Cuts);
                });
                if (!tie) result.WinnerIndex = order[0].Index;
            }
            else if (!Rules.respawn && _participants.Count > 1)
            {
                // Last Kite Flying: survivors first, then reverse elimination order.
                order.Sort((x, y) =>
                {
                    int ex = _eliminationOrder.IndexOf(x.Index), ey = _eliminationOrder.IndexOf(y.Index);
                    if (ex < 0 && ey < 0) return y.Kite.Durability.CompareTo(x.Kite.Durability);
                    if (ex < 0) return -1;
                    if (ey < 0) return 1;
                    return ey.CompareTo(ex);
                });
                result.WinnerIndex = order[0].Index;
            }
            else
            {
                order.Sort((x, y) =>
                {
                    if (x.Quit != y.Quit) return x.Quit ? 1 : -1;
                    int s = y.Score.CompareTo(x.Score);
                    if (s != 0) return s;
                    int c = y.Cuts.CompareTo(x.Cuts);
                    return c != 0 ? c : x.SelfSnaps.CompareTo(y.SelfSnaps);
                });
                if (order.Count > 0) result.WinnerIndex = order[0].Index;
            }

            for (int rank = 0; rank < order.Count; rank++)
            {
                MatchParticipant p = order[rank];
                ParticipantResult r = p.Result;
                r.Placement = rank + 1;
                r.Won = !result.Draw && (Rules.teamCount > 1 ? p.Team == result.WinningTeam : p.Index == result.WinnerIndex);
                r.RoundsWon = p.RoundsWon;
                r.Score = p.Score;
                r.Cuts = p.Cuts;
                r.Assists = p.Assists;
                r.SelfSnaps = p.SelfSnaps;
                r.Crashes = p.Crashes;
                r.ContestsWon = p.ContestsWon;
                r.ContestsLost = p.ContestsLost;
                r.SweetFraction = p.ContestTicks == 0 ? 0f : p.SweetTicks / (float)p.ContestTicks;

                // Clamp dor usage to 60–150 m per dor (spec 12.2).
                var dorKeys = new List<string>(r.DorUsed.Keys);
                foreach (string k in dorKeys) r.DorUsed[k] = KMath.Clamp(r.DorUsed[k], Setup.Phases.dorUsedMin, Setup.Phases.dorUsedMax);
            }
            result.Participants.Clear();
            foreach (MatchParticipant p in _participants) result.Participants.Add(p.Result);
            return result;
        }

        private void Raise(MatchEventType type, int a = -1, int b = -1, float value = 0f, int code = 0, Vector3 position = default)
        {
            _events.Add(new MatchEvent { Type = type, A = a, B = b, Value = value, Code = code, Position = position });
        }
    }
}
