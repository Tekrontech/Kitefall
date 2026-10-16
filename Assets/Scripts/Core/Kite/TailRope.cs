using System;
using System.Numerics;

namespace Kitefall.Core
{
    [Serializable]
    public class TailRopeSettings
    {
        public float gravity = 9.81f;
        /// <summary>Velocity kept per step (0–1). Lower = heavier air drag.</summary>
        public float damping = 0.96f;
        /// <summary>How strongly the tail is pushed toward wind speed (1/s).</summary>
        public float windDrag = 2.5f;
        /// <summary>Constraint passes per step. More = stiffer rope.</summary>
        public int iterations = 6;
        /// <summary>Random flutter strength, m/s².</summary>
        public float flutter = 3f;
    }

    /// <summary>
    /// Milestone 2 – dum (tail) rope physics. Verlet rope: point 0 is pinned to the kite's tail,
    /// the rest falls with gravity, is pushed by the wind and keeps fixed segment lengths.
    /// It reacts to turns, dives and gusts automatically because the pinned point moves.
    /// Visual only: tail stats (stability etc.) come from <see cref="TailStats"/>.
    /// </summary>
    public sealed class TailRope
    {
        private readonly TailRopeSettings _s;
        private readonly Vector3[] _pos;
        private readonly Vector3[] _prev;
        private readonly float _segmentLength;
        private readonly SmoothNoise _noise;

        public int PointCount => _pos.Length;
        public float SegmentLength => _segmentLength;
        public Vector3 this[int i] => _pos[i];

        /// <param name="segments">Number of segments (spec: 6 / 10 / 14 for Low / Med / High).</param>
        /// <param name="totalLength">Tail length in metres.</param>
        public TailRope(TailRopeSettings settings, int segments, float totalLength, int seed = 0)
        {
            _s = settings ?? new TailRopeSettings();
            segments = Math.Max(1, segments);
            _pos = new Vector3[segments + 1];
            _prev = new Vector3[segments + 1];
            _segmentLength = Math.Max(0.01f, totalLength) / segments;
            _noise = new SmoothNoise(seed);
        }

        /// <summary>Lays the tail straight out from <paramref name="attach"/> along <paramref name="direction"/>.</summary>
        public void Reset(Vector3 attach, Vector3 direction)
        {
            Vector3 dir = direction.LengthSquared() > 1e-6f ? Vector3.Normalize(direction) : -Vector3.UnitY;
            for (int i = 0; i < _pos.Length; i++)
            {
                _pos[i] = attach + dir * (_segmentLength * i);
                _prev[i] = _pos[i];
            }
        }

        /// <param name="attach">World position of the kite's tail point this step.</param>
        /// <param name="wind">Wind velocity, m/s (world).</param>
        public void Step(Vector3 attach, Vector3 wind, float time, float dt)
        {
            if (dt <= 0f) return;
            float dt2 = dt * dt;
            var gravity = new Vector3(0f, -_s.gravity, 0f);

            _pos[0] = attach;
            _prev[0] = attach;

            for (int i = 1; i < _pos.Length; i++)
            {
                Vector3 velocity = (_pos[i] - _prev[i]) * _s.damping;
                Vector3 airVelocity = velocity / dt;
                Vector3 accel = gravity + (wind - airVelocity) * _s.windDrag;
                if (_s.flutter > 0f)
                {
                    float n = _noise.Sample(time + i * 0.37f, 0.6f);
                    accel += new Vector3(n, n * 0.3f, -n) * _s.flutter;
                }
                _prev[i] = _pos[i];
                _pos[i] += velocity + accel * dt2;
            }

            for (int k = 0; k < _s.iterations; k++)
            {
                _pos[0] = attach;
                for (int i = 0; i < _pos.Length - 1; i++)
                {
                    Vector3 delta = _pos[i + 1] - _pos[i];
                    float len = delta.Length();
                    if (len < 1e-6f) continue;
                    float diff = (len - _segmentLength) / len;
                    if (i == 0)
                    {
                        _pos[i + 1] -= delta * diff;           // first point pinned: move only the second
                    }
                    else
                    {
                        _pos[i] += delta * (0.5f * diff);
                        _pos[i + 1] -= delta * (0.5f * diff);
                    }
                }
            }
        }

        public void CopyPoints(Vector3[] target)
        {
            int n = Math.Min(target.Length, _pos.Length);
            Array.Copy(_pos, target, n);
        }
    }
}
