namespace Kitefall.Core
{
    /// <summary>One wind reading.</summary>
    public readonly struct WindSample
    {
        /// <summary>Direction the wind blows toward, degrees clockwise from +Z.</summary>
        public readonly float DirectionDeg;
        /// <summary>Speed in m/s.</summary>
        public readonly float Speed;
        public readonly bool IsStrong;

        public WindSample(float directionDeg, float speed, bool isStrong)
        {
            DirectionDeg = directionDeg;
            Speed = speed;
            IsStrong = isStrong;
        }

        /// <summary>Unit vector the wind blows toward, (x, z) on the ground plane.</summary>
        public float DirX => KMath.Sin(DirectionDeg * KMath.Deg2Rad);
        public float DirZ => KMath.Cos(DirectionDeg * KMath.Deg2Rad);
    }

    /// <summary>
    /// Deterministic wind field: same settings + seed + time = same wind on every machine.
    /// Gusts and round phases (Calm / Gust / Storm) arrive in milestone 7 through <see cref="SpeedMultiplier"/>.
    /// </summary>
    public sealed class WindModel
    {
        private readonly WindSettings _settings;
        private readonly SmoothNoise _directionNoise;
        private readonly SmoothNoise _speedNoise;

        /// <summary>Extra multiplier on speed (gusts / storm phase later). Default 1.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public WindSettings Settings => _settings;

        public WindModel(WindSettings settings)
        {
            _settings = settings ?? new WindSettings();
            _directionNoise = new SmoothNoise(_settings.seed);
            _speedNoise = new SmoothNoise(_settings.seed * 31 + 7);
        }

        /// <param name="time">Match time in seconds.</param>
        /// <param name="heightAboveGround">Height of the sample point in metres.</param>
        public WindSample Sample(float time, float heightAboveGround)
        {
            float dir = _settings.baseDirectionDeg
                      + _directionNoise.Sample(time, _settings.variationPeriod) * _settings.directionVariationDeg;

            float speed = _settings.baseSpeed * (1f + _speedNoise.Sample(time, _settings.variationPeriod * 0.6f) * _settings.speedVariation);
            float heightFactor = 1f + KMath.Max(0f, heightAboveGround) / 100f * _settings.heightGainPer100m;
            speed *= heightFactor * SpeedMultiplier;
            speed = KMath.Clamp(speed, _settings.minSpeed, _settings.maxSpeed);

            return new WindSample(KMath.WrapAngle(dir), speed, speed >= _settings.strongWindSpeed);
        }
    }
}
