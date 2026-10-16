using System;

namespace Kitefall.Core
{
    /// <summary>
    /// Small math helpers so the Core assembly never needs UnityEngine.
    /// Everything here is deterministic and allocation-free (server friendly).
    /// </summary>
    public static class KMath
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float InverseLerp(float a, float b, float value)
        {
            if (Math.Abs(b - a) < 1e-6f) return 0f;
            return Clamp01((value - a) / (b - a));
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float diff = target - current;
            if (Math.Abs(diff) <= maxDelta) return target;
            return current + Math.Sign(diff) * maxDelta;
        }

        /// <summary>Wraps an angle into (-180, 180].</summary>
        public static float WrapAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>Shortest signed difference from <paramref name="from"/> to <paramref name="to"/> in degrees.</summary>
        public static float DeltaAngle(float from, float to) => WrapAngle(to - from);

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + Math.Sign(delta) * maxDelta;
        }

        public static float Sin(float x) => (float)Math.Sin(x);
        public static float Cos(float x) => (float)Math.Cos(x);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float x) => (float)Math.Sqrt(x);
        public static float Abs(float x) => Math.Abs(x);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
    }
}
