namespace Kitefall.Core
{
    /// <summary>Result of one tension step – tells the caller what changed this tick.</summary>
    public readonly struct TensionStepResult
    {
        public readonly bool ZoneChanged;
        public readonly bool SnappedThisStep;

        public TensionStepResult(bool zoneChanged, bool snappedThisStep)
        {
            ZoneChanged = zoneChanged;
            SnappedThisStep = snappedThisStep;
        }
    }

    /// <summary>
    /// Tension T (0–100) for one kite – spec 9.1 and 9.2.
    /// Pure C#: no UnityEngine, so the exact same code can run on the server later.
    /// </summary>
    public sealed class TensionModel
    {
        private readonly TensionSettings _settings;
        private readonly float _strength;
        private readonly float _riseMult;
        private readonly float _pullRiseMult;
        private readonly float _releaseDropMult;

        public float Tension { get; private set; }
        public TensionZone Zone { get; private set; }
        /// <summary>How long tension has been continuously above dor Strength (seconds).</summary>
        public float OverStrengthTime { get; private set; }
        public bool IsSnapped { get; private set; }
        public float Strength => _strength;
        public float SnapHoldTime => _settings.snapHoldTime;
        public bool IsAboveStrength => Tension > _strength;
        /// <summary>Tension never drops below this (Brake charkhi during a Dheel Cut = 55). 0 = off.</summary>
        public float Floor { get; set; }

        /// <param name="settings">Shared tension tunables.</param>
        /// <param name="strength">Dor Strength (snap limit).</param>
        /// <param name="riseMult">Dor multiplier on every rise (1 = none).</param>
        /// <param name="pullRiseMult">Charkhi multiplier on rises while pulling (1 = none).</param>
        /// <param name="releaseDropMult">Charkhi multiplier on falls while releasing (1 = none).</param>
        public TensionModel(TensionSettings settings, float strength, float riseMult = 1f, float pullRiseMult = 1f, float releaseDropMult = 1f)
        {
            _settings = settings ?? new TensionSettings();
            _strength = strength;
            _riseMult = riseMult;
            _pullRiseMult = pullRiseMult;
            _releaseDropMult = releaseDropMult;
            Reset(_settings.idleTarget);
        }

        public TensionModel(TensionSettings settings, in LoadoutStats loadout)
            : this(settings, loadout.Strength, loadout.TensionRiseMult, loadout.PullTensionRiseMult, loadout.ReleaseTensionDropMult)
        {
        }

        public void Reset(float tension)
        {
            Tension = KMath.Clamp(tension, 0f, 100f);
            Zone = ZoneFor(Tension);
            OverStrengthTime = 0f;
            IsSnapped = false;
            Floor = 0f;
        }

        public TensionZone ZoneFor(float tension)
        {
            if (tension >= _settings.strongZoneMax) return TensionZone.Danger;
            if (tension >= _settings.safeZoneMax) return TensionZone.Strong;
            return TensionZone.Safe;
        }

        /// <summary>Advances tension by <paramref name="dt"/> seconds.</summary>
        public TensionStepResult Step(LineAction action, bool strongWind, float dt)
        {
            if (IsSnapped || dt <= 0f) return new TensionStepResult(false, false);

            float windRise = strongWind ? _settings.strongWindRiseMult : 1f;
            float riseMult = _riseMult * windRise;

            switch (action)
            {
                case LineAction.Pull:
                    Tension += _settings.pullRate * riseMult * _pullRiseMult * dt;
                    break;
                case LineAction.Dheel:
                    Tension -= _settings.releaseRate * _releaseDropMult * dt;
                    break;
                default:
                    // Idle and Reel: drift toward the idle target. Rising drift also gets rise multipliers.
                    bool rising = Tension < _settings.idleTarget;
                    float rate = _settings.idleDriftRate * (rising ? riseMult : 1f);
                    Tension = KMath.MoveTowards(Tension, _settings.idleTarget, rate * dt);
                    break;
            }

            Tension = KMath.Clamp(Tension, Floor > 0f ? KMath.Min(Floor, 100f) : 0f, 100f);

            TensionZone previous = Zone;
            Zone = ZoneFor(Tension);

            bool snapped = false;
            if (Tension > _strength)
            {
                OverStrengthTime += dt;
                if (OverStrengthTime > _settings.snapHoldTime)
                {
                    IsSnapped = true;
                    snapped = true;
                }
            }
            else
            {
                OverStrengthTime = 0f;
            }

            return new TensionStepResult(previous != Zone, snapped);
        }
    }
}
