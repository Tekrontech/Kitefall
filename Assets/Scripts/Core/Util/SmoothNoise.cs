namespace Kitefall.Core
{
    /// <summary>
    /// Deterministic, smooth 1D noise in the range [-1, 1] built from a sum of sines.
    /// The same seed and time always give the same value, so the server and every
    /// client can reproduce the wind exactly from a shared match seed.
    /// </summary>
    public readonly struct SmoothNoise
    {
        private readonly float _phaseA;
        private readonly float _phaseB;
        private readonly float _phaseC;

        public SmoothNoise(int seed)
        {
            // Simple integer hash -> three phases in [0, 2π).
            uint h = (uint)seed * 2654435761u;
            _phaseA = (h & 0x3FF) / 1024f * 6.2831853f;
            h = h * 1103515245u + 12345u;
            _phaseB = (h & 0x3FF) / 1024f * 6.2831853f;
            h = h * 1103515245u + 12345u;
            _phaseC = (h & 0x3FF) / 1024f * 6.2831853f;
        }

        /// <summary>Returns smooth noise in [-1, 1]. <paramref name="period"/> is the main cycle length in seconds.</summary>
        public float Sample(float time, float period)
        {
            if (period <= 0.0001f) return 0f;
            float t = time / period * 6.2831853f;
            // Incommensurate frequencies so the pattern does not visibly repeat.
            float v = KMath.Sin(t + _phaseA) * 0.5f
                    + KMath.Sin(t * 2.31f + _phaseB) * 0.3f
                    + KMath.Sin(t * 4.67f + _phaseC) * 0.2f;
            return KMath.Clamp(v, -1f, 1f);
        }
    }
}
