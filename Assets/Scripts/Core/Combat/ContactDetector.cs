using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    [Serializable]
    public class ContactSettings
    {
        /// <summary>Lines closer than this = CONTACT (spec 9.3: 0.3 m).</summary>
        public float contactDistance = 0.3f;
        /// <summary>Lines closer than this = APPROACH (spec 9.3: 15 m).</summary>
        public float approachDistance = 15f;
        /// <summary>
        /// Lines move a lot between two 20 Hz ticks, so they can pass through each other without ever
        /// being 0.3 m apart on a tick. If the closest-approach direction flips while both ticks are
        /// within this range, the lines crossed = contact. Assumption, see docs/DECISIONS.md.
        /// </summary>
        public float sweepRange = 4f;
        /// <summary>No re-contact between the same pair for this long after a NEUTRAL (spec 9.3: 1 s).</summary>
        public float neutralCooldown = 1f;
    }

    public struct ContactEvent
    {
        public int A;
        public int B;
        public Vector3 Point;
        public float AngleDeg;
        /// <summary>Distance from A's anchor to the contact point along A's line.</summary>
        public float AlongA;
        public float AlongB;
    }

    /// <summary>
    /// Milestone 3 – checks every pair of lines each tick (spec 9.3: 3D segment-to-segment distance at 20 ticks/s).
    /// </summary>
    public sealed class ContactDetector
    {
        private readonly ContactSettings _s;
        private readonly int _n;
        private readonly Vector3[] _prevDelta;
        private readonly bool[] _hasPrev;
        private readonly float[] _cooldownUntil;
        private readonly bool[] _blocked;
        private readonly float[] _nearestDistance;
        private readonly int[] _nearestOther;

        public ContactSettings Settings => _s;

        public ContactDetector(ContactSettings settings, int kiteCount)
        {
            _s = settings ?? new ContactSettings();
            _n = Math.Max(1, kiteCount);
            _prevDelta = new Vector3[_n * _n];
            _hasPrev = new bool[_n * _n];
            _cooldownUntil = new float[_n * _n];
            _blocked = new bool[_n * _n];
            _nearestDistance = new float[_n];
            _nearestOther = new int[_n];
        }

        private int Key(int a, int b) => a < b ? a * _n + b : b * _n + a;

        /// <summary>Distance from kite <paramref name="i"/>'s line to the nearest enemy line (∞ if none).</summary>
        public float NearestDistance(int i) => _nearestDistance[i];
        /// <summary>Index of the nearest enemy line, or -1.</summary>
        public int NearestOther(int i) => _nearestOther[i];
        public bool IsApproaching(int i) => _nearestDistance[i] < _s.approachDistance;

        /// <summary>Pairs in a contest are skipped until <see cref="Release"/>.</summary>
        public void Block(int a, int b) => _blocked[Key(a, b)] = true;

        /// <summary>Ends the block; optional cooldown (after NEUTRAL).</summary>
        public void Release(int a, int b, float time, bool cooldown)
        {
            int k = Key(a, b);
            _blocked[k] = false;
            _hasPrev[k] = false;
            if (cooldown) _cooldownUntil[k] = time + _s.neutralCooldown;
        }

        public void ResetAll()
        {
            Array.Clear(_hasPrev, 0, _hasPrev.Length);
            Array.Clear(_blocked, 0, _blocked.Length);
            Array.Clear(_cooldownUntil, 0, _cooldownUntil.Length);
        }

        /// <param name="lines">Line points per kite (anchor first). Null = no line.</param>
        /// <param name="active">Kite can make contact (flying).</param>
        /// <param name="canContact">Optional filter, e.g. no contests between team mates.</param>
        public void Step(float time, IReadOnlyList<Vector3[]> lines, IReadOnlyList<bool> active,
            Func<int, int, bool> canContact, List<ContactEvent> output)
        {
            for (int i = 0; i < _n; i++)
            {
                _nearestDistance[i] = float.PositiveInfinity;
                _nearestOther[i] = -1;
            }

            int count = Math.Min(_n, lines.Count);
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    int k = Key(a, b);
                    if (!active[a] || !active[b] || lines[a] == null || lines[b] == null || (canContact != null && !canContact(a, b)))
                    {
                        _hasPrev[k] = false;
                        continue;
                    }

                    LineGeometry.Closest c = LineGeometry.PolylineClosest(lines[a], lines[b]);
                    if (c.Distance < _nearestDistance[a]) { _nearestDistance[a] = c.Distance; _nearestOther[a] = b; }
                    if (c.Distance < _nearestDistance[b]) { _nearestDistance[b] = c.Distance; _nearestOther[b] = a; }

                    Vector3 delta = c.PointB - c.PointA;
                    bool crossed = _hasPrev[k]
                                   && c.Distance < _s.sweepRange
                                   && _prevDelta[k].Length() < _s.sweepRange
                                   && Vector3.Dot(delta, _prevDelta[k]) < 0f;
                    _prevDelta[k] = delta;
                    _hasPrev[k] = true;

                    if (_blocked[k] || time < _cooldownUntil[k]) continue;

                    if (c.Distance < _s.contactDistance || crossed)
                    {
                        _blocked[k] = true;
                        output.Add(new ContactEvent
                        {
                            A = a,
                            B = b,
                            Point = (c.PointA + c.PointB) * 0.5f,
                            AngleDeg = LineGeometry.CrossingAngle(c.DirectionA, c.DirectionB),
                            AlongA = c.AlongA,
                            AlongB = c.AlongB
                        });
                    }
                }
            }
        }
    }
}
