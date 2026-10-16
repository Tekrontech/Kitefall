using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>
    /// Upgrade v1.4 (spec F3, F6, F7, F8, F9, F10): Warmup plaza, rooftop selection, launch ritual, roof hopping,
    /// player commands (emotes, quick chat, help launch), roof traits and training wind.
    /// Every rule runs here on the server/host; clients only animate the replicated state.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        public RoofClaims Claims { get; private set; }
        /// <summary>Finished contests so far (training practice overlay, analytics).</summary>
        public IReadOnlyList<ContestSummary> ContestHistory => _contestHistory;
        public WaitingArea WaitingArea => Arena.Waiting;
        public bool UsesRoofs => Arena.Roofs != null;
        /// <summary>Roof hopping allowed in this match.</summary>
        public bool HoppingAllowed => UsesRoofs && Rules.roofHopping && !Setup.FixedRoofs;

        private bool _warmupEndRequested;
        private float _phaseElapsed;

        // =====================================================================
        // Setup
        // =====================================================================

        private void InitUpgrade()
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                p.CharacterPosition = Arena.Anchors[i] - new Vector3(0f, 1.35f, 0f);
                if (UsesRoofs) p.RoofId = Arena.Roofs.RoofAt(Arena.Anchors[i], 1f);
                p.BotRoofThink = 1f + _rng.Range(0f, 4f);
            }
            if (UsesRoofs)
            {
                var teams = new List<int>();
                foreach (MatchParticipant p in _participants) teams.Add(p.Team);
                RoofSelectMode mode = Setup.FixedRoofs ? RoofSelectMode.Assigned : Rules.roofSelect;
                Claims = new RoofClaims(Arena.Roofs, mode, teams, Setup.Seed);
                foreach (RoofNode r in Arena.Roofs.Roofs) r.Capacity = Claims.CapacityOf(r.Id);
            }
        }

        private void BeginPreMatch()
        {
            _phaseElapsed = 0f;
            if (UsesRoofs && Rules.warmup && Setup.WarmupTime > 0f)
            {
                WaitingArea area = Arena.BuildWaitingRoof(_participants.Count);
                for (int i = 0; i < _participants.Count; i++)
                {
                    _participants[i].CharacterPosition = area.SpawnPoints[i % area.SpawnPoints.Count];
                    _participants[i].Flyer = FlyerState.Walking;
                }
                EnterPhase(MatchPhase.Warmup, Setup.WarmupTime);
                return;
            }
            AfterWarmup();
        }

        private void AfterWarmup()
        {
            _phaseElapsed = 0f;
            if (UsesRoofs && !Setup.FixedRoofs && Rules.mode != GameMode.Tutorial)
            {
                foreach (MatchParticipant p in _participants) p.Flyer = FlyerState.OnRoof;
                EnterPhase(MatchPhase.RoofSelect, Rules.IsDuel ? Setup.Roofs.selectTimeDuel : Setup.Roofs.selectTime);
                return;
            }
            if (UsesRoofs) { Claims.AssignRemaining(); ApplyClaims(); }
            BeginIntro();
        }

        private void BeginIntro()
        {
            foreach (MatchParticipant p in _participants) if (!p.Eliminated) p.Flyer = FlyerState.OnRoof;
            if (Rules.introTime > 0f) EnterPhase(MatchPhase.Intro, Rules.introTime);
            else BeginLaunch();
        }

        private void BeginLaunch()
        {
            if (Setup.UseLaunchRitual)
            {
                bool full = RoundNumber == 0;
                PrepareLaunch();
                foreach (MatchParticipant p in _participants)
                    if (!p.Eliminated) StartPrep(p, full);
                EnterPhase(MatchPhase.Prep, full ? Setup.Prep.fullCap : Setup.Prep.shortCap);
                return;
            }
            EnterPhase(MatchPhase.Launch, Rules.launchTime);
            PrepareLaunch();
        }

        /// <summary>Host: the room is full – end Warmup early (never before the 10 s minimum).</summary>
        public void RequestWarmupEnd() => _warmupEndRequested = true;

        // =====================================================================
        // Warmup (spec F3)
        // =====================================================================

        private void StepWarmup(IReadOnlyList<KiteInput> inputs)
        {
            PhaseTimeLeft -= Dt;
            _phaseElapsed += Dt;
            WaitingArea area = Arena.Waiting;
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                float sx, sz;
                if (p.IsBot) BotWander(p, area, out sx, out sz);
                else { KiteInput input = i < inputs.Count ? inputs[i] : default; sx = input.SteerX; sz = input.SteerY; }
                Vector3 pos = p.CharacterPosition;
                p.CharacterSpeed = CharacterMotion.Step(ref pos, ref p.CharacterYaw, sx, sz, Dt, area.Surfaces);
                p.CharacterPosition = pos;
            }
            bool full = _warmupEndRequested && _phaseElapsed >= Setup.WarmupMin;
            if (PhaseTimeLeft <= 0f || full) AfterWarmup();
        }

        private void BotWander(MatchParticipant p, WaitingArea area, out float sx, out float sz)
        {
            // Walk toward a target spot, pause, pick a new one; an emote now and then.
            p.BotRoofThink -= Dt;
            if (p.BotRoofThink <= 0f || p.BotHopTarget < 0)
            {
                p.BotHopTarget = _rng.Range(0, area.Surfaces.Count);
                p.BotRoofThink = _rng.Range(3f, 7f);
                if (_rng.Chance(0.3f)) Execute(p.Index, PlayerCommand.Emote, _rng.Range(0, 4));
            }
            WalkRect r = area.Surfaces[p.BotHopTarget];
            Vector3 d = r.Center - p.CharacterPosition;
            d.Y = 0f;
            float len = d.Length();
            if (len < 1.5f || p.BotRoofThink > 5.5f) { sx = sz = 0f; return; }
            sx = d.X / len * 0.6f;
            sz = d.Z / len * 0.6f;
        }

        // =====================================================================
        // Rooftop selection (spec 10.3)
        // =====================================================================

        private void StepRoofSelect()
        {
            PhaseTimeLeft -= Dt;
            _phaseElapsed += Dt;
            foreach (MatchParticipant p in _participants)
            {
                if (!p.IsBot || Claims.ClaimOf(p.Index) >= 0 && Claims.Mode != RoofSelectMode.DuelPairs) continue;
                if (Claims.Mode == RoofSelectMode.DuelPairs && Claims.ClaimOf(p.Index) >= 0) continue;
                if (_phaseElapsed < p.BotRoofThink) continue;
                int pick = BotPickRoof(p);
                if (pick >= 0) Execute(p.Index, PlayerCommand.ClaimRoof, pick);
                p.BotRoofThink = 999f;
            }
            if (PhaseTimeLeft <= 0f)
            {
                Claims.AssignRemaining();
                ApplyClaims();
                BeginIntro();
            }
        }

        private int BotPickRoof(MatchParticipant p)
        {
            if (Claims.Mode == RoofSelectMode.DuelPairs) return Claims.OfferedPairs.Count > 0 ? Claims.OfferedPairs[_rng.Range(0, Claims.OfferedPairs.Count)] : -1;
            var options = new List<int>();
            for (int r = 0; r < Arena.Roofs.Roofs.Count; r++) if (Claims.CanClaim(p.Index, r)) options.Add(r);
            return options.Count == 0 ? -1 : options[_rng.Range(0, options.Count)];
        }

        private void SyncClaims()
        {
            foreach (MatchParticipant p in _participants) p.RoofId = Claims.ClaimOf(p.Index);
        }

        /// <summary>Moves every flyer to the claimed roof: anchors, kites and characters.</summary>
        private void ApplyClaims()
        {
            SyncClaims();
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                if (p.RoofId < 0) continue;
                p.RoofSlot = Claims.SlotOf(i);
                PlaceOnRoof(p, p.RoofId, p.RoofSlot, true);
                p.BotRoofThink = 6f + _rng.Range(0f, 6f);
                p.BotHopTarget = -1;
            }
        }

        private void PlaceOnRoof(MatchParticipant p, int roof, int slot, bool newKite)
        {
            RoofNode node = Arena.Roofs.Roof(roof);
            Vector3 spot = node.LaunchSpot(slot);
            Arena.Anchors[p.Index] = spot;
            p.CharacterPosition = spot - new Vector3(0f, 1.35f, 0f);
            p.CharacterYaw = 0f;
            if (newKite)
            {
                float durability = p.Kite.Durability;
                float stock = p.Kite.Spool.StockMeters;
                p.Current.dorStockMeters = stock > 0f ? stock : p.Current.dorStockMeters;
                p.Kite = NewKite(p, p.Index);
                p.Kite.Durability = durability;
                _lines[p.Index] = p.Kite.LinePoints;
            }
        }

        // =====================================================================
        // Launch ritual (spec F10)
        // =====================================================================

        private void StartPrep(MatchParticipant p, bool full)
        {
            p.Prep = new LaunchPrepController(Setup.Prep, full, Time, Setup.Seed + p.Index * 37 + RoundNumber * 1009 + Tick, p.LastKanni)
            {
                AutoPrep = p.Setup.autoPrep || !p.Connected,
                CablesNearby = RoofOf(p)?.Has(RoofTrait.Cables) ?? false,
                SafeLaunch = RoofOf(p)?.Has(RoofTrait.Sheltered) ?? false
            };
            if (p.IsBot) p.Prep.SetBot(p.Setup.botDifficulty);
            p.HelpedBy = -1;
            p.Flyer = FlyerState.Prep;
            p.Kite.SetWaiting();
            Raise(MatchEventType.PrepStarted, p.Index, -1, p.Prep.Cap, full ? 1 : 0);
        }

        /// <summary>Short prep on the current roof (respawn, after roof hopping, 25 s grounded).</summary>
        private void StartShortPrep(MatchParticipant p, bool newKite)
        {
            if (UsesRoofs && p.RoofId >= 0)
            {
                p.RoofSlot = FreeSlot(p, p.RoofId);
                PlaceOnRoof(p, p.RoofId, p.RoofSlot, newKite);
            }
            StartPrep(p, false);
        }

        private void StepPrepPhase(IReadOnlyList<KiteInput> inputs)
        {
            PhaseTimeLeft -= Dt;
            bool allDone = true;
            for (int i = 0; i < _participants.Count; i++)
            {
                MatchParticipant p = _participants[i];
                if (p.Eliminated) continue;
                KiteInput input = i < inputs.Count ? inputs[i] : default;
                if (p.Flyer == FlyerState.Prep) { UpdatePrep(p, input); allDone &= p.Flyer != FlyerState.Prep; }
                else if (p.Kite.IsFlying) StepFreeFlight(p, input); // finished early → flies earlier (no contacts yet)
            }
            if (PhaseTimeLeft <= 0f || allDone) StartRound();
        }

        private void UpdatePrep(MatchParticipant p, in KiteInput input)
        {
            if (!p.Connected) p.Prep.AutoPrep = true;
            p.Prep.Update(Time, input.PullHeld, input.SteerX, Dt);
            foreach (PrepEvent e in p.Prep.Events)
                if (e.Type != PrepEventType.StepStarted || e.Step != PrepStep.PickKite)
                    Raise(MatchEventType.Prep, p.Index, -1, (int)e.Grade, (int)e.Type);
            if (p.Prep.Done) LaunchFromPrep(p);
        }

        private void LaunchFromPrep(MatchParticipant p)
        {
            LaunchPrepController prep = p.Prep;
            if (prep.Full) p.LastKanni = prep.KanniGrade;
            LaunchWith(p, prep.HeightBonus + TraitStartBonus(p));
            p.Kite.Flight.SetDrift(prep.DriftDegPerSec, prep.DriftDuration, prep.DriftUntilSteer);
        }

        /// <summary>Flight without contacts, crates or scoring (kite launched before the round starts).</summary>
        private void StepFreeFlight(MatchParticipant p, in KiteInput input)
        {
            KiteSimulation kite = p.Kite;
            WindSample w = KiteWind(p, SampleWind(MatchTime, kite.Flight.Height));
            kite.Step(input, w, MatchTime, Dt);
            if (!kite.IsFlying)
            {
                // A crash before the round even started: back on the roof with a short prep, no damage.
                float durability = kite.Durability;
                p.Kite = NewKite(p, p.Index);
                p.Kite.Durability = durability;
                _lines[p.Index] = p.Kite.LinePoints;
                StartPrep(p, false);
            }
        }

        private void OnLaunchedFromRoof(MatchParticipant p)
        {
            RoofNode roof = RoofOf(p);
            if (roof != null && roof.Has(RoofTrait.Upwind) && MatchTime < Setup.Roofs.upwindBonusDuration)
                p.UpwindBonusUntil = Setup.Roofs.upwindBonusDuration;
            p.GroundedTime = 0f;
        }

        // =====================================================================
        // Roof hopping during the round (spec 10.4)
        // =====================================================================

        /// <summary>Handles prep / grounded / traversing flyers. Returns true when the kite loop should skip this player.</summary>
        private bool StepFlyerUpgrade(MatchParticipant p, int i, IReadOnlyList<KiteInput> inputs)
        {
            KiteInput input = i < inputs.Count ? inputs[i] : default;
            if (p.IsBot && HoppingAllowed) BotRoofBrain(p);

            switch (p.Flyer)
            {
                case FlyerState.Prep:
                    if (p.Prep == null) return false;
                    UpdatePrep(p, input);
                    return true;
                case FlyerState.Grounded:
                    p.GroundedTime += Dt;
                    StepGroundedWalk(p, input);
                    StormHurtsGrounded(p);
                    if (p.GroundedTime >= Setup.Roofs.groundedAutoPrep)
                    {
                        Raise(MatchEventType.GroundedTooLong, p.Index);
                        StartShortPrep(p, true);
                    }
                    return true;
                case FlyerState.Traversing:
                    p.GroundedTime += Dt;
                    p.TraverseLeft -= Dt;
                    RoofLink link = Arena.Roofs.Link(p.TraverseLink);
                    float t = 1f - KMath.Clamp01(p.TraverseLeft / KMath.Max(0.01f, p.TraverseTotal));
                    Vector3 a = p.RoofId == link.A ? link.PointA : link.PointB, b = p.RoofId == link.A ? link.PointB : link.PointA;
                    p.CharacterPosition = Vector3.Lerp(a, b, t);
                    p.CharacterSpeed = Vector3.Distance(a, b) / KMath.Max(0.1f, p.TraverseTotal);
                    StormHurtsGrounded(p);
                    if (p.TraverseLeft <= 0f) Arrive(p, link.Other(p.RoofId));
                    return true;
            }
            return false;
        }

        private void StepGroundedWalk(MatchParticipant p, in KiteInput input)
        {
            if (p.IsBot || p.RoofId < 0) { p.CharacterSpeed = 0f; return; }
            RoofNode roof = Arena.Roofs.Roof(p.RoofId);
            var rect = new[] { WalkRect.Around(roof.Center, roof.SizeX - 1f, roof.SizeZ - 1f) };
            Vector3 pos = p.CharacterPosition;
            p.CharacterSpeed = CharacterMotion.Step(ref pos, ref p.CharacterYaw, input.SteerX, input.SteerY, Dt, rect);
            p.CharacterPosition = pos;
        }

        private void StormHurtsGrounded(MatchParticipant p)
        {
            if (!Rules.stormZone) return;
            float dx = p.CharacterPosition.X - StormCenter.X, dz = p.CharacterPosition.Z - StormCenter.Z;
            if (dx * dx + dz * dz > StormRadius * StormRadius)
            {
                p.Kite.Durability = KMath.Max(0f, p.Kite.Durability - Setup.Phases.stormDamagePerSecond * Dt);
                if (p.Kite.Durability <= 0f && !p.Eliminated)
                {
                    p.Kite.EndFlight(KiteStatus.Crashed);
                    p.Flyer = FlyerState.OnRoof;
                }
            }
        }

        private KiteInput PackUpInput(MatchParticipant p) => new KiteInput { ReelHeld = true, SteerY = 0.25f };

        private void StepPackUp(MatchParticipant p)
        {
            p.PackUpLeft -= Dt;
            if (!p.Kite.IsFlying) { p.Flyer = FlyerState.OnRoof; return; }
            if (p.PackUpLeft > 0f || ContestOf(p.Index) != null) return; // never lands in the middle of a contest
            AccountDorUsed(p);
            p.Kite.SetWaiting();
            p.Flyer = FlyerState.Grounded;
            p.GroundedTime = 0f;
            p.BotLaunchAt = -1f;
            if (p.RoofId >= 0) p.CharacterPosition = Arena.Roofs.Roof(p.RoofId).LaunchSpot(p.RoofSlot) - new Vector3(0f, 1.35f, 0f);
            Raise(MatchEventType.PackedUp, p.Index);
        }

        private void Arrive(MatchParticipant p, int roof)
        {
            p.RoofId = roof;
            p.RoofSlot = FreeSlot(p, roof);
            p.TraverseLink = -1;
            p.Flyer = FlyerState.Grounded;
            p.CharacterSpeed = 0f;
            p.CharacterPosition = Arena.Roofs.Roof(roof).LaunchSpot(p.RoofSlot) - new Vector3(0f, 1.35f, 0f);
            Raise(MatchEventType.ArrivedOnRoof, p.Index, -1, 0f, roof);
            if (p.GroundedTime >= Setup.Roofs.groundedAutoPrep)
            {
                Raise(MatchEventType.GroundedTooLong, p.Index);
                StartShortPrep(p, true);
            }
        }

        private int FreeSlot(MatchParticipant p, int roof)
        {
            bool slot0Taken = false;
            foreach (MatchParticipant o in _participants)
                if (o != p && !o.Eliminated && o.RoofId == roof && o.RoofSlot == 0) slot0Taken = true;
            return slot0Taken ? 1 : 0;
        }

        /// <summary>FFA/LKF: one flyer per roof. Team: up to 2 teammates, never enemies (spec 10.3/10.4).</summary>
        public bool CanEnterRoof(int player, int roof)
        {
            if (!UsesRoofs || roof < 0) return false;
            MatchParticipant p = _participants[player];
            int others = 0;
            foreach (MatchParticipant o in _participants)
            {
                if (o == p || o.Eliminated || o.Flyer == FlyerState.Out) continue;
                bool there = o.RoofId == roof && o.Flyer != FlyerState.Traversing || o.Flyer == FlyerState.Traversing && o.TraverseLink >= 0 && Arena.Roofs.Link(o.TraverseLink).Other(o.RoofId) == roof;
                if (!there) continue;
                if (Rules.teamCount > 1 && o.Team != p.Team) return false;
                others++;
            }
            int capacity = Rules.teamCount > 1 ? 2 : 1;
            if (others >= capacity) return false;
            if (Rules.teamCount > 1 && MatchTime < Setup.Roofs.teamHalfLockTime && Claims != null && !Claims.InTeamHalf(roof, p.Team)) return false;
            return true;
        }

        // =====================================================================
        // Commands
        // =====================================================================

        private void ProcessCommands(IReadOnlyList<KiteInput> inputs)
        {
            if (inputs == null) return;
            for (int i = 0; i < _participants.Count && i < inputs.Count; i++)
            {
                KiteInput input = inputs[i];
                if (input.Command == PlayerCommand.None) continue;
                MatchParticipant p = _participants[i];
                if (p.CommandSeen && input.CommandSeq == p.LastCommandSeq) continue;
                p.CommandSeen = true;
                p.LastCommandSeq = input.CommandSeq;
                Execute(i, input.Command, input.CommandArg);
            }
        }

        /// <summary>Runs one player command with all server checks. Returns false when it is not allowed right now.</summary>
        public bool Execute(int index, PlayerCommand command, int arg)
        {
            if (index < 0 || index >= _participants.Count || IsOver) return false;
            MatchParticipant p = _participants[index];
            switch (command)
            {
                case PlayerCommand.ClaimRoof:
                    if (Phase != MatchPhase.RoofSelect || Claims == null) return false;
                    if (!Claims.TryClaim(index, arg)) return false;
                    SyncClaims();
                    Raise(MatchEventType.RoofClaimed, index, -1, 0f, arg);
                    return true;

                case PlayerCommand.PackUp:
                    if (Phase != MatchPhase.Round || !HoppingAllowed || p.Flyer != FlyerState.Flying || !p.Kite.IsFlying || ContestOf(index) != null) return false;
                    float reel = KMath.Max(1f, p.Kite.Loadout.ReelSpeed);
                    p.PackUpTotal = p.PackUpLeft = KMath.Clamp(p.Kite.Spool.LineOut / reel * 0.5f, Setup.Roofs.packUpMin, Setup.Roofs.packUpMax);
                    p.Flyer = FlyerState.PackingUp;
                    Raise(MatchEventType.PackUpStarted, index, -1, p.PackUpTotal);
                    return true;

                case PlayerCommand.TraverseLink:
                    if (Phase != MatchPhase.Round || p.Flyer != FlyerState.Grounded || arg < 0 || arg >= Arena.Roofs.Links.Count) return false;
                    RoofLink link = Arena.Roofs.Link(arg);
                    if (!link.Touches(p.RoofId) || !CanEnterRoof(index, link.Other(p.RoofId))) return false;
                    p.TraverseLink = arg;
                    p.TraverseTotal = p.TraverseLeft = link.TravelTime;
                    p.Flyer = FlyerState.Traversing;
                    p.CharacterYaw = KMath.Atan2(link.Other(p.RoofId) == link.B ? link.PointB.X - link.PointA.X : link.PointA.X - link.PointB.X,
                        link.Other(p.RoofId) == link.B ? link.PointB.Z - link.PointA.Z : link.PointA.Z - link.PointB.Z) * KMath.Rad2Deg;
                    Raise(MatchEventType.TraverseStarted, index, -1, link.TravelTime, arg);
                    return true;

                case PlayerCommand.LaunchHere:
                    if (Phase != MatchPhase.Round || p.Flyer != FlyerState.Grounded) return false;
                    StartShortPrep(p, true);
                    return true;

                case PlayerCommand.Emote:
                    if (arg < 0 || arg > 255) return false;
                    if (Phase == MatchPhase.Round && ContestOf(index) != null) return false; // never during an active contest
                    if (!p.Social.TryEmote(Time)) return false;
                    Raise(MatchEventType.Emote, index, -1, 0f, arg);
                    return true;

                case PlayerCommand.QuickChat:
                    if (!QuickChat.Valid(arg) || !p.Social.TryChat(Time)) return false;
                    Raise(MatchEventType.QuickChat, index, -1, 0f, arg);
                    return true;

                case PlayerCommand.HelpLaunch:
                    if (arg < 0 || arg >= _participants.Count || arg == index) return false;
                    MatchParticipant target = _participants[arg];
                    if (target.Flyer != FlyerState.Prep || target.Prep == null || target.Prep.HelperPresent) return false;
                    if (p.Flyer == FlyerState.Prep || p.Flyer == FlyerState.Traversing) return false;
                    bool friend = Rules.teamCount > 1 ? p.Team == target.Team : !string.IsNullOrEmpty(p.Setup.partyId) && p.Setup.partyId == target.Setup.partyId;
                    if (!friend || Vector3.Distance(p.CharacterPosition, target.CharacterPosition) > 15f) return false;
                    target.Prep.HelperPresent = true;
                    target.HelpedBy = index;
                    Raise(MatchEventType.HelpLaunch, index, arg);
                    return true;
            }
            return false;
        }

        // =====================================================================
        // Bots on roofs
        // =====================================================================

        private void BotRoofBrain(MatchParticipant p)
        {
            if (Phase != MatchPhase.Round) return;
            switch (p.Flyer)
            {
                case FlyerState.Flying:
                    p.BotRoofThink -= Dt;
                    if (p.BotRoofThink > 0f) return;
                    p.BotRoofThink = 4f;
                    if (ContestOf(p.Index) != null || !p.Kite.IsFlying) return;
                    int near = 0;
                    foreach (MatchParticipant o in _participants)
                        if (o != p && o.Kite.IsFlying && (Rules.teamCount <= 1 || o.Team != p.Team) && Vector3.Distance(o.Kite.WorldPosition, p.Kite.WorldPosition) < 100f) near++;
                    bool stormNear = false;
                    if (Rules.stormZone)
                    {
                        float dx = p.CharacterPosition.X - StormCenter.X, dz = p.CharacterPosition.Z - StormCenter.Z;
                        stormNear = dx * dx + dz * dz > StormRadius * StormRadius * 0.7f;
                    }
                    // Retreat when outnumbered nearby, the storm is coming, or the kite is badly damaged (spec 10.4 bots).
                    float chance = p.Setup.botDifficulty == BotDifficulty.Easy ? 0.15f : 0.35f;
                    bool hurt = p.Kite.Durability < 35f;
                    if ((near >= 2 || stormNear || hurt) && _rng.Chance(chance)) Execute(p.Index, PlayerCommand.PackUp, 0);
                    break;

                case FlyerState.Grounded:
                    if (p.BotHopTarget >= 0 && p.BotHopTarget == p.RoofId)
                    {
                        if (p.BotLaunchAt < 0f) p.BotLaunchAt = Time + 1f;
                        if (Time >= p.BotLaunchAt) { p.BotHopTarget = -1; p.BotLaunchAt = -1f; Execute(p.Index, PlayerCommand.LaunchHere, 0); }
                        return;
                    }
                    int bestLink = -1;
                    float bestScore = float.MaxValue;
                    foreach (int l in Arena.Roofs.Roof(p.RoofId).Links)
                    {
                        int dest = Arena.Roofs.Link(l).Other(p.RoofId);
                        if (!CanEnterRoof(p.Index, dest)) continue;
                        float score = 0f;
                        foreach (MatchParticipant o in _participants)
                            if (o != p && o.Kite.IsFlying) score += 1f / (1f + Vector3.Distance(o.CharacterPosition, Arena.Roofs.Roof(dest).Center) * 0.05f);
                        if (Rules.stormZone) score += Vector2.Distance(new Vector2(StormCenter.X, StormCenter.Z), new Vector2(Arena.Roofs.Roof(dest).Center.X, Arena.Roofs.Roof(dest).Center.Z)) * 0.01f;
                        if (score < bestScore) { bestScore = score; bestLink = l; }
                    }
                    if (bestLink >= 0)
                    {
                        p.BotHopTarget = Arena.Roofs.Link(bestLink).Other(p.RoofId);
                        Execute(p.Index, PlayerCommand.TraverseLink, bestLink);
                    }
                    else Execute(p.Index, PlayerCommand.LaunchHere, 0);
                    break;
            }
        }

        // =====================================================================
        // Traits, wind, crates
        // =====================================================================

        public RoofNode RoofOf(MatchParticipant p) => UsesRoofs && p.RoofId >= 0 ? Arena.Roofs.Roof(p.RoofId) : null;

        private float TraitStartBonus(MatchParticipant p)
        {
            RoofNode roof = RoofOf(p);
            if (roof == null) return 0f;
            float bonus = 0f;
            if (roof.Has(RoofTrait.High)) bonus += Setup.Roofs.highStartBonus;
            if (roof.Has(RoofTrait.Sheltered)) bonus -= Setup.Roofs.shelteredStartPenalty;
            if (roof.Has(RoofTrait.Upwind)) bonus += Setup.Roofs.upwindStartBonus;
            return bonus;
        }

        /// <summary>Per-kite gust strength from the launch roof (High ×1.25, Sheltered ×0.6).</summary>
        private WindSample KiteWind(MatchParticipant p, WindSample w)
        {
            if (!Phases.GustActive) return w;
            RoofNode roof = RoofOf(p);
            if (roof == null) return w;
            float mult = roof.Has(RoofTrait.High) ? Setup.Roofs.highGustMultiplier : roof.Has(RoofTrait.Sheltered) ? Setup.Roofs.shelteredGustMultiplier : 1f;
            return mult == 1f ? w : new WindSample(w.DirectionDeg, w.Speed * mult, w.IsStrong);
        }

        private WindSample SampleWind(float time, float height)
        {
            WindSample w = Wind.Sample(time, height);
            TrainingOptions t = Rules.mode == GameMode.Training ? Setup.Training : null;
            if (t == null || !t.overrideWind) return w;
            float dir = t.windDirectionDeg + KMath.Sin(time * 0.21f) * 4f;
            float speed = t.windSpeed * Wind.SpeedMultiplier;
            return new WindSample(dir, speed, speed >= Setup.Wind.strongWindSpeed);
        }

        private float TrainingWindMultiplier(float normal)
        {
            TrainingOptions t = Rules.mode == GameMode.Training ? Setup.Training : null;
            if (t == null) return normal;
            float m = normal;
            if (t.forcedPhase == (int)RoundPhase.Calm) m = 1f;
            else if (t.forcedPhase == (int)RoundPhase.Storm) m = Setup.Phases.stormWindMult * (Phases.GustActive ? Setup.Phases.gustWindMult : 1f);
            else if (t.forcedPhase == (int)RoundPhase.Gust) m = Phases.GustActive || Phases.Phase == RoundPhase.Calm && (int)(RoundTime / 17f) % 2 == 1 ? Setup.Phases.gustWindMult : 1f;
            if (!t.gusts && Phases.GustActive && m >= Setup.Phases.gustWindMult) m /= Setup.Phases.gustWindMult;
            return m;
        }

        private bool CrateSpotPosition(CrateSettings cs, out Vector3 pos)
        {
            pos = default;
            if (!UsesRoofs) return false;
            foreach (RoofNode r in Arena.Roofs.Roofs)
            {
                if (!r.Has(RoofTrait.CrateSpot)) continue;
                float radius = Setup.Roofs.crateSpotRadius * 0.7f;
                pos = new Vector3(r.Center.X + _crateRng.Range(-radius, radius), _crateRng.Range(cs.minHeight, cs.maxHeight) * 0.6f, r.Center.Z + _crateRng.Range(8f, radius));
                return true;
            }
            return false;
        }
    }
}
