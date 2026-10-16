using System.Numerics;

namespace Kitefall.Core
{
    public enum BotState { Patrol, Hunt, Attack, Retreat }

    /// <summary>
    /// Milestone 8 – bot pilot (spec 14.3). States: Patrol → Hunt → Attack (chooses a counter) → Retreat.
    /// A bot only produces <see cref="KiteInput"/> – it flies with the same physics, stats and formula as players.
    /// Difficulty = how well it keeps tension in the sweet zone (Easy 50 %, Medium 70 %, Hard 85 %)
    /// and how often it picks the right counter.
    /// </summary>
    public sealed class BotBrain
    {
        public BotDifficulty Difficulty { get; }
        public int Self { get; }
        public BotState State { get; private set; } = BotState.Patrol;
        public int Target { get; private set; } = -1;

        private readonly DeterministicRandom _rng;
        private float _retargetTimer;
        private float _resampleTimer;
        private float _tensionOffset;
        private float _stateTimer;
        private AttackType _plannedAttack;
        private Contest _plannedFor;
        private float _patrolPhase;
        private float _launchTarget;
        private KiteInput _lastSteer;

        public BotBrain(int self, BotDifficulty difficulty, int seed)
        {
            Self = self;
            Difficulty = difficulty;
            _rng = new DeterministicRandom(seed * 131 + self * 7 + 1);
            _patrolPhase = _rng.Range(0f, 6.28f);
            _launchTarget = difficulty == BotDifficulty.Hard ? 1.4f : _rng.Range(0.9f, 1.9f);
        }

        /// <summary>Chance per decision to keep tension in the sweet zone (spec 14.3).</summary>
        public float Accuracy
        {
            get
            {
                switch (Difficulty)
                {
                    case BotDifficulty.Hard: return 0.85f;
                    case BotDifficulty.Medium: return 0.70f;
                    case BotDifficulty.Dummy: return 0.2f;
                    default: return 0.50f;
                }
            }
        }

        /// <summary>Chance to pick the correct counter attack.</summary>
        public float CounterSkill
        {
            get
            {
                switch (Difficulty)
                {
                    case BotDifficulty.Hard: return 0.85f;
                    case BotDifficulty.Medium: return 0.5f;
                    default: return 0.2f;
                }
            }
        }

        public KiteInput Think(MatchSimulation sim)
        {
            float dt = MatchSimulation.Dt;
            MatchParticipant me = sim.Participants[Self];
            KiteSimulation k = me.Kite;

            if (sim.Phase == MatchPhase.Launch)
                return new KiteInput { PullHeld = me.LaunchHold < _launchTarget && !me.LaunchDone };
            if (sim.Phase != MatchPhase.Round || !k.IsFlying) return default;

            if (Difficulty == BotDifficulty.Dummy) return DummyFlight(sim, me, k);

            Contest contest = sim.ContestOf(Self);
            if (contest != null)
            {
                State = BotState.Attack;
                return ContestInput(sim, contest, dt);
            }

            _retargetTimer -= dt;
            _stateTimer -= dt;
            if (State == BotState.Retreat && _stateTimer > 0f) return Retreat(k);
            if (k.Durability < 25f && Difficulty != BotDifficulty.Easy && State != BotState.Retreat)
            {
                State = BotState.Retreat;
                _stateTimer = 6f;
                return Retreat(k);
            }

            if (_retargetTimer <= 0f || Target < 0 || !sim.Participants[Target].Kite.IsFlying)
            {
                Target = PickTarget(sim);
                _retargetTimer = 3f;
            }

            if (Target < 0)
            {
                State = BotState.Patrol;
                return Patrol(sim, k);
            }

            State = BotState.Hunt;
            return Hunt(sim, k, sim.Participants[Target].Kite);
        }

        // ---- States ---------------------------------------------------------

        /// <summary>Training target dummies (upgrade F8): 3 heights, static / slow / fast side-to-side.</summary>
        private KiteInput DummyFlight(MatchSimulation sim, MatchParticipant me, KiteSimulation k)
        {
            int pattern = me.Setup.dummyPattern, height = me.Setup.dummyHeight;
            if (pattern <= 0 && height == 1) return Hover(k);
            float targetEl = height <= 0 ? 28f : height == 1 ? 45f : 62f;
            float swing = pattern <= 0 ? 0f : pattern == 1 ? 18f : 32f;
            float speed = pattern == 1 ? 0.35f : 1.1f;
            float targetAz = sim.CurrentWind.DirectionDeg + KMath.Sin(sim.MatchTime * speed + _patrolPhase) * swing;
            KiteInput input = SteerToAngles(k, targetAz, targetEl);
            TensionTo(k, 44f, ref input, false);
            ManageLine(k, 70f + height * 30f, ref input);
            Safety(k, ref input);
            return input;
        }

        private KiteInput Hover(KiteSimulation k)
        {
            var input = new KiteInput { SteerY = k.Flight.ElevationDeg < 45f ? 0.5f : 0f };
            TensionTo(k, 42f, ref input, false);
            return input;
        }

        private KiteInput Patrol(MatchSimulation sim, KiteSimulation k)
        {
            float t = sim.MatchTime * 0.25f + _patrolPhase;
            float targetAz = sim.CurrentWind.DirectionDeg + KMath.Sin(t) * 35f;
            float targetEl = 45f + KMath.Sin(t * 2f) * 12f;
            KiteInput input = SteerToAngles(k, targetAz, targetEl);
            TensionTo(k, 50f, ref input, false);
            ManageLine(k, 140f, ref input);
            Safety(k, ref input);
            return input;
        }

        private KiteInput Retreat(KiteSimulation k)
        {
            State = BotState.Retreat;
            var input = new KiteInput { SteerY = 1f };
            TensionTo(k, 45f, ref input, false);
            ManageLine(k, 90f, ref input);
            Safety(k, ref input);
            return input;
        }

        private KiteInput Hunt(MatchSimulation sim, KiteSimulation k, KiteSimulation enemy)
        {
            // Aim at the point of the enemy line that our own line can reach, slightly above it.
            Vector3[] line = enemy.LinePoints;
            float reach = k.Flight.Radius * 0.75f;
            Vector3 aim = line[line.Length - 1];
            float best = float.MaxValue;
            for (int i = 1; i < line.Length; i++)
            {
                float d = KMath.Abs(Vector3.Distance(line[i], k.Anchor) - reach);
                if (d < best) { best = d; aim = line[i]; }
            }
            aim.Y += 3f;

            Vector3 dir = aim - k.Anchor;
            float len = dir.Length();
            if (len < 1f) return Patrol(sim, k);
            dir /= len;
            float targetAz = KMath.Atan2(dir.X, dir.Z) * KMath.Rad2Deg;
            float targetEl = (float)System.Math.Asin(KMath.Clamp(dir.Y, -1f, 1f)) * KMath.Rad2Deg;

            KiteInput input = SteerToAngles(k, targetAz, targetEl);
            ManageLine(k, KMath.Clamp(len / 0.75f, 70f, 320f), ref input);

            bool near = sim.Contacts.NearestDistance(Self) < sim.Setup.Contact.approachDistance;
            if (near) ChooseStance(sim, k, enemy, ref input);
            else TensionTo(k, 52f, ref input, false);

            Safety(k, ref input);
            return input;
        }

        /// <summary>Getting ready just before contact: Dheel stance, Kheench stance or a dive from above.</summary>
        private void ChooseStance(MatchSimulation sim, KiteSimulation k, KiteSimulation enemy, ref KiteInput input)
        {
            AttackType predicted = Predict(enemy);
            AttackType want = _rng.Chance(CounterSkill) ? AttackRules.CounterOf(predicted) : (AttackType)_rng.Range(1, 4);

            switch (want)
            {
                case AttackType.Dive:
                    if (k.Flight.Height > enemy.Flight.Height + 2f && k.Flight.CanDive) input.DiveHeld = true;
                    else TensionTo(k, 55f, ref input, false);
                    break;
                case AttackType.Dheel:
                    // Be releasing line when the lines touch.
                    if (k.CurrentAction == LineAction.Pull) input.PullHeld = false;
                    else if (k.CurrentAction != LineAction.Dheel) input.PullHeld = true;
                    break;
                default:
                    TensionTo(k, 62f, ref input, false);
                    if (k.Tension.Tension < k.Tension.Strength - 15f) input.PullHeld = true;
                    break;
            }
        }

        private KiteInput ContestInput(MatchSimulation sim, Contest c, float dt)
        {
            MatchParticipant me = sim.Participants[Self];
            KiteSimulation k = me.Kite;
            bool locked = c.A == Self ? c.LockedA : c.LockedB;
            var input = new KiteInput { SteerY = 0.2f };

            if (!locked)
            {
                if (!ReferenceEquals(_plannedFor, c))
                {
                    _plannedFor = c;
                    int other = c.Other(Self);
                    bool otherLocked = c.A == other ? c.LockedA : c.LockedB;
                    AttackType theirs = otherLocked ? c.AttackOf(other) : Predict(sim.Participants[other].Kite);
                    _plannedAttack = _rng.Chance(CounterSkill) ? AttackRules.CounterOf(theirs) : (AttackType)_rng.Range(1, 4);
                    if (_plannedAttack == AttackType.Dheel && k.CurrentAction == LineAction.Idle) _plannedAttack = AttackType.Hold;
                }
                switch (_plannedAttack)
                {
                    case AttackType.Kheench: input.PullHeld = true; break;
                    case AttackType.Dive: input.SteerY = -1f; input.PullHeld = true; break;
                    case AttackType.Dheel: input.PullHeld = false; break;
                    default: break; // Hold: no input
                }
                return input;
            }

            // Locked: keep tension inside the moving sweet zone (with difficulty-based mistakes).
            SweetZoneBand? band = sim.SweetZoneFor(Self);
            _resampleTimer -= dt;
            if (_resampleTimer <= 0f)
            {
                _resampleTimer = 0.35f;
                float width = band?.Width ?? 15f;
                _tensionOffset = _rng.Chance(Accuracy) ? _rng.Range(-width * 0.2f, width * 0.2f)
                    : (_rng.Chance(0.5f) ? 1f : -1f) * (width * 0.5f + _rng.Range(4f, 16f));
            }
            float target = (band?.Center ?? 55f) + _tensionOffset;
            target = KMath.Min(target, k.Tension.Strength - 3f);
            TensionTo(k, target, ref input, true);
            return input;
        }

        // ---- Helpers --------------------------------------------------------

        private int PickTarget(MatchSimulation sim)
        {
            MatchParticipant me = sim.Participants[Self];
            int best = -1;
            float bestDist = float.MaxValue;
            foreach (MatchParticipant p in sim.Participants)
            {
                if (p.Index == Self || !p.Kite.IsFlying) continue;
                if (sim.Rules.teamCount > 1 && p.Team == me.Team) continue;
                float d = Vector3.Distance(p.Kite.WorldPosition, me.Kite.WorldPosition);
                if (d < bestDist) { bestDist = d; best = p.Index; }
            }
            return best;
        }

        private static AttackType Predict(KiteSimulation enemy)
        {
            if (enemy.Flight.IsDiving) return AttackType.Dive;
            switch (enemy.CurrentAction)
            {
                case LineAction.Pull: return AttackType.Kheench;
                case LineAction.Dheel: return AttackType.Dheel;
                default: return AttackType.Hold;
            }
        }

        private KiteInput SteerToAngles(KiteSimulation k, float targetAz, float targetEl)
        {
            float el = k.Flight.ElevationDeg;
            float dAz = KMath.DeltaAngle(k.Flight.AzimuthDeg, targetAz) * KMath.Cos(el * KMath.Deg2Rad);
            float dEl = targetEl - el;
            float err = KMath.Sqrt(dAz * dAz + dEl * dEl);
            if (err < 3f)
            {
                // Close: keep going the same way so the lines actually cross.
                return new KiteInput { SteerX = _lastSteer.SteerX, SteerY = _lastSteer.SteerY };
            }
            float h = KMath.Atan2(dAz, dEl);
            var input = new KiteInput { SteerX = KMath.Sin(h), SteerY = KMath.Cos(h) };
            _lastSteer = input;
            return input;
        }

        private static void TensionTo(KiteSimulation k, float target, ref KiteInput input, bool allowDheel)
        {
            float t = k.Tension.Tension;
            if (t < target - 2f && t < k.Tension.Strength - 4f)
            {
                input.PullHeld = true;
                input.ReelHeld = false;
            }
            else
            {
                input.PullHeld = false;
                // Going too high in a contest: let dheel bring it down; otherwise cancel dheel to save line.
                if (k.CurrentAction == LineAction.Dheel && (!allowDheel || t < target)) input.ReelHeld = true;
            }
        }

        private static void ManageLine(KiteSimulation k, float desired, ref KiteInput input)
        {
            float line = k.Spool.LineOut;
            if (line > desired + 25f && !input.PullHeld) input.ReelHeld = true;
            else if (line < desired - 15f && k.CurrentAction == LineAction.Dheel) input.ReelHeld = false;
        }

        private static void Safety(KiteSimulation k, ref KiteInput input)
        {
            if (k.Flight.Height < 14f || k.Flight.ElevationDeg < 10f)
            {
                input.SteerX = 0f;
                input.SteerY = 1f;
                input.DiveHeld = false;
                if (k.Tension.Tension < 50f) input.PullHeld = true;
            }
            if (k.Tension.Tension > k.Tension.Strength - 3f) input.PullHeld = false;
        }
    }
}
