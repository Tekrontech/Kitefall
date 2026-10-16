using System;

namespace Kitefall.Core
{
    public enum RoundPhase { Calm, Gust, Storm }

    [Serializable]
    public class PhaseSettings
    {
        public float calmEnd = 60f;
        public float gustEnd = 120f;
        /// <summary>Long modes (FFA, Team…) repeat Calm → Gust → Storm every cycle.</summary>
        public float cycleLength = 180f;
        public float gustIntervalMin = 15f;
        public float gustIntervalMax = 20f;
        public float gustWarning = 1f;
        public float gustDuration = 3f;
        /// <summary>Wind speed multiplier during a gust.</summary>
        public float gustWindMult = 1.45f;
        /// <summary>Wind speed multiplier in the storm phase.</summary>
        public float stormWindMult = 1.5f;
        /// <summary>Arena event (birds / sea gust) – once per gust phase, lasts this long.</summary>
        public float arenaEventDuration = 2f;

        // ---- Launch (spec 6: "HOLD to launch; release in perfect window = start 10 m higher")
        public float launchPerfectMin = 1.2f;
        public float launchPerfectMax = 1.6f;
        public float launchBonusHeight = 10f;
        /// <summary>Holding longer than this overfills the launch (no bonus).</summary>
        public float launchOverfill = 2.4f;

        // ---- Storm zone (Last Kite Flying)
        public float stormStartRadius = 420f;
        public float stormShrinkFactor = 0.75f;
        public float stormMinRadius = 50f;
        public float stormDamagePerSecond = 5f;

        // ---- Penalties
        /// <summary>Crash durability loss (spec 6.2: −15 %).</summary>
        public float crashDurabilityLoss = 15f;
        /// <summary>Extra durability lost by a cut kite when the mode has no kite loss (Quick Duel).</summary>
        public float cutDurabilityLossNoLossMode = 10f;

        // ---- Disconnects (spec 14.2)
        public float disconnectContestFreeze = 3f;
        public float disconnectRoundForfeit = 30f;
        public float disconnectMatchForfeit = 60f;

        // ---- Dor consumption (spec 12.2: 60–150 m per match + cut line)
        public float dorUsedMin = 60f;
        public float dorUsedMax = 150f;
        public float dorUsedFractionOfMaxLine = 0.25f;
    }

    /// <summary>Round phases (spec 6.1): Calm 0–1 min, Gust 1–2 min (a gust every 15–20 s), Storm 2–3 min.</summary>
    public sealed class RoundPhaseController
    {
        private readonly PhaseSettings _s;
        private DeterministicRandom _rng;
        private float _gustEndsAt;
        private float _eventAt;
        private float _eventEndsAt;
        private int _eventCycle = -1;

        public RoundPhase Phase { get; private set; }
        public bool GustActive { get; private set; }
        public bool GustWarning { get; private set; }
        public bool ArenaEventActive { get; private set; }
        public float NextGustAt { get; private set; }
        public bool UseStorm { get; private set; }
        public PhaseSettings Settings => _s;

        public float WindMultiplier
        {
            get
            {
                float m = Phase == RoundPhase.Storm ? _s.stormWindMult : 1f;
                if (GustActive) m *= _s.gustWindMult;
                return m;
            }
        }

        public RoundPhaseController(PhaseSettings settings) => _s = settings ?? new PhaseSettings();

        public void Reset(int seed, bool useStorm)
        {
            _rng = new DeterministicRandom(seed);
            UseStorm = useStorm;
            Phase = RoundPhase.Calm;
            GustActive = GustWarning = ArenaEventActive = false;
            NextGustAt = _s.calmEnd + _rng.Range(0f, 3f);
            _eventCycle = -1;
        }

        public RoundPhase PhaseAt(float roundTime)
        {
            float t = _s.cycleLength > 0f ? roundTime % _s.cycleLength : roundTime;
            if (t < _s.calmEnd) return RoundPhase.Calm;
            if (t < _s.gustEnd || !UseStorm) return RoundPhase.Gust;
            return RoundPhase.Storm;
        }

        /// <summary>Advances to <paramref name="roundTime"/>. Returns flags for what started this step.</summary>
        public void Step(float roundTime, out bool phaseChanged, out bool gustWarningStarted, out bool gustStarted, out bool arenaEventStarted)
        {
            phaseChanged = gustWarningStarted = gustStarted = arenaEventStarted = false;
            RoundPhase p = PhaseAt(roundTime);
            if (p != Phase)
            {
                Phase = p;
                phaseChanged = true;
                if (p == RoundPhase.Calm)
                {
                    // New cycle in long modes.
                    float cycleStart = roundTime - (roundTime % _s.cycleLength);
                    NextGustAt = cycleStart + _s.calmEnd + _rng.Range(0f, 3f);
                }
            }

            if (GustActive && roundTime >= _gustEndsAt) GustActive = false;

            if (Phase == RoundPhase.Gust)
            {
                bool warning = roundTime >= NextGustAt - _s.gustWarning && roundTime < NextGustAt;
                if (warning && !GustWarning) gustWarningStarted = true;
                GustWarning = warning;
                if (roundTime >= NextGustAt)
                {
                    GustActive = true;
                    gustStarted = true;
                    _gustEndsAt = NextGustAt + _s.gustDuration;
                    NextGustAt += _rng.Range(_s.gustIntervalMin, _s.gustIntervalMax);
                }

                int cycle = _s.cycleLength > 0f ? (int)(roundTime / _s.cycleLength) : 0;
                if (cycle != _eventCycle)
                {
                    _eventCycle = cycle;
                    float gustPhaseStart = cycle * _s.cycleLength + _s.calmEnd;
                    _eventAt = gustPhaseStart + _rng.Range(10f, (_s.gustEnd - _s.calmEnd) - 5f);
                    _eventEndsAt = _eventAt + _s.arenaEventDuration;
                }
            }
            else
            {
                GustWarning = false;
            }

            bool eventNow = roundTime >= _eventAt && roundTime < _eventEndsAt;
            if (eventNow && !ArenaEventActive) arenaEventStarted = true;
            ArenaEventActive = eventNow;
        }
    }
}
