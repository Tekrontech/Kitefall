using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>Line shape and 3D distance helpers (milestone 3).</summary>
    public static class LineGeometry
    {
        /// <summary>
        /// Fills <paramref name="output"/> with points along a sagging line from anchor to kite.
        /// Quadratic Bézier: the control point drops more when tension is low.
        /// Both the server (contact checks) and the client (drawing) use this, so they always agree.
        /// </summary>
        public static void BuildLine(Vector3 anchor, Vector3 kite, float tension01, float maxSagFraction, Vector3[] output)
        {
            int n = output.Length;
            if (n == 0) return;
            float length = Vector3.Distance(anchor, kite);
            float sag = length * maxSagFraction * (1f - KMath.Clamp01(tension01));
            Vector3 control = (anchor + kite) * 0.5f - Vector3.UnitY * (sag * 2f);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 1f : i / (float)(n - 1);
                float u = 1f - t;
                output[i] = u * u * anchor + 2f * u * t * control + t * t * kite;
            }
        }

        /// <summary>
        /// Closest distance between segments p1–q1 and p2–q2 (Ericson, Real-Time Collision Detection 5.1.9).
        /// <paramref name="s"/> and <paramref name="t"/> are 0–1 positions along each segment.
        /// </summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
            out Vector3 c1, out Vector3 c2, out float s, out float t)
        {
            const float eps = 1e-8f;
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);

            if (a <= eps && e <= eps)
            {
                s = t = 0f;
                c1 = p1; c2 = p2;
                return Vector3.Distance(c1, c2);
            }
            if (a <= eps)
            {
                s = 0f;
                t = KMath.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= eps)
                {
                    t = 0f;
                    s = KMath.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > eps ? KMath.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = KMath.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = KMath.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return Vector3.Distance(c1, c2);
        }

        /// <summary>Result of the closest approach between two polylines.</summary>
        public struct Closest
        {
            public float Distance;
            public Vector3 PointA;
            public Vector3 PointB;
            public int SegmentA;
            public int SegmentB;
            /// <summary>Distance from the start (anchor) of line A to the closest point, m.</summary>
            public float AlongA;
            public float AlongB;
            public Vector3 DirectionA;
            public Vector3 DirectionB;
        }

        public static Closest PolylineClosest(Vector3[] a, Vector3[] b)
        {
            var best = new Closest { Distance = float.MaxValue };
            float alongA = 0f;
            for (int i = 0; i < a.Length - 1; i++)
            {
                float segLenA = Vector3.Distance(a[i], a[i + 1]);
                float alongB = 0f;
                for (int j = 0; j < b.Length - 1; j++)
                {
                    float segLenB = Vector3.Distance(b[j], b[j + 1]);
                    float d = SegmentDistance(a[i], a[i + 1], b[j], b[j + 1], out Vector3 ca, out Vector3 cb, out float s, out float t);
                    if (d < best.Distance)
                    {
                        best.Distance = d;
                        best.PointA = ca;
                        best.PointB = cb;
                        best.SegmentA = i;
                        best.SegmentB = j;
                        best.AlongA = alongA + segLenA * s;
                        best.AlongB = alongB + segLenB * t;
                        best.DirectionA = a[i + 1] - a[i];
                        best.DirectionB = b[j + 1] - b[j];
                    }
                    alongB += segLenB;
                }
                alongA += segLenA;
            }
            return best;
        }

        /// <summary>Crossing angle between two directions, 0–180°.</summary>
        public static float CrossingAngle(Vector3 dirA, Vector3 dirB)
        {
            float la = dirA.Length(), lb = dirB.Length();
            if (la < 1e-6f || lb < 1e-6f) return 90f;
            float cos = KMath.Clamp(Vector3.Dot(dirA, dirB) / (la * lb), -1f, 1f);
            return (float)System.Math.Acos(cos) * KMath.Rad2Deg;
        }
    }
}
