using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Draws one kite from snapshots: sail, dum (rope physics), line with sag and contact vibration,
    /// cut fall (spins, drifts, lands), snapped line pieces, crash pose.
    /// </summary>
    public sealed class KiteView : MonoBehaviour
    {
        public int Index { get; private set; }
        public Vector3 KitePosition => _kite != null ? _kite.transform.position : transform.position;
        public bool Visible => _kite != null && _kite.activeSelf;

        private GameObject _kite;
        private Transform _tailPoint;
        private Renderer _sailRenderer;
        private LineRenderer _line;
        private LineRenderer _dangle;
        private TailRope[] _ropes;
        private LineRenderer[] _ropeLines;
        private System.Numerics.Vector3[] _ropePts;
        private readonly System.Numerics.Vector3[] _linePts = new System.Numerics.Vector3[16];
        private Color _lineColor = new Color(0.95f, 0.95f, 0.92f, 0.95f);
        private Color _tailColor;
        private MaterialPropertyBlock _mpb;

        private KiteStatus _status = KiteStatus.Waiting;
        private bool _falling;
        private Vector3 _fallVelocity;
        private float _fallSpin;
        private float _endTime;
        private Vector3 _cutPoint;
        private Vector3 _anchor;
        private float _lastHeading;
        private float _bank;
        private float _distanceScale = 1f;

        // Readability (audit E1/E2, visual only – physics never sees it): a far kite is drawn bigger so it stays at least
        // ~40 px tall at 720p (×3 at most), and the line is never thinner than ~2 px on screen.
        private const float KiteWorldHeight = 1.15f * KiteVisualFactory.VisualScale;
        private const float MinKiteScreenFraction = 40f / 720f;
        private const float MinLineScreenFraction = 2f / 720f;
        private const float MaxDistanceScale = 3f;

        /// <summary>The extra visual scale this frame for distance (1 … 3). Visual only.</summary>
        public float DistanceScale => _distanceScale;
        /// <summary>How tall the kite is drawn right now, in metres (base size × distance scale). The HUD sizes its markers from it.</summary>
        public float VisualHeight => KiteWorldHeight * _distanceScale;

        public void Init(int index, string kiteId, string skinId, string tailId, Color lineColor, int tailSegments, TailRopeSettings ropeSettings)
        {
            Index = index;
            _lineColor = lineColor;
            _kite = KiteVisualFactory.Build(kiteId, skinId, transform);
            _tailPoint = _kite.transform.Find("TailPoint");
            _sailRenderer = _kite.GetComponentInChildren<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _tailColor = KiteVisualFactory.LookFor(kiteId, skinId).Secondary;

            _line = NewLine("Line", 0.05f);
            _dangle = NewLine("Dangle", 0.04f);
            _dangle.enabled = false;

            // Dum: how many ribbons and how long (visual only; stats come from TailStats).
            int ropes; float length;
            switch (tailId)
            {
                case "none": ropes = 0; length = 0f; break;
                case "double_ribbon": ropes = 2; length = 2.2f; break;
                case "long_tail": ropes = 1; length = 4.2f; break;
                case "tassel": ropes = 3; length = 1.1f; break;
                default: ropes = 1; length = 1.8f; break;
            }
            _ropes = new TailRope[ropes];
            _ropeLines = new LineRenderer[ropes];
            _ropePts = new System.Numerics.Vector3[tailSegments + 1];
            for (int i = 0; i < ropes; i++)
            {
                _ropes[i] = new TailRope(ropeSettings, tailSegments, length * KiteVisualFactory.VisualScale * 0.8f, index * 10 + i);
                _ropes[i].Reset(ToN(TailAttach(i)), System.Numerics.Vector3.UnitY * -1f);
                LineRenderer lr = NewLine("Dum" + i, 0.16f);
                lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.35f);
                lr.startColor = lr.endColor = _tailColor;
                lr.positionCount = _ropePts.Length;
                _ropeLines[i] = lr;
            }
        }

        private LineRenderer NewLine(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Materials.Unlit();
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace = true;
            lr.startColor = lr.endColor = _lineColor;
            return lr;
        }

        private Vector3 TailAttach(int rope)
        {
            Vector3 p = _tailPoint != null ? _tailPoint.position : _kite.transform.position;
            if (_ropes == null || _ropes.Length <= 1) return p;
            return p + _kite.transform.right * ((rope - (_ropes.Length - 1) * 0.5f) * 0.25f);
        }

        /// <summary>The character's left hand (upgrade W1): the drawn line starts here. Visual offset only, max 1.5 m.</summary>
        public Transform LineStart;
        /// <summary>The character holds the kite while waiting – don't draw the old "kite on the roof" pose.</summary>
        public bool HideWhenWaiting;

        private System.Numerics.Vector3 LineOrigin(in KiteSnap s)
        {
            if (LineStart == null) return s.Anchor;
            Vector3 a = ArenaBuilder.ToUnity(s.Anchor);
            Vector3 h = LineStart.position;
            Vector3 d = h - a;
            if (d.magnitude > 1.5f) h = a + d.normalized * 1.5f;
            return new System.Numerics.Vector3(h.x, h.y, h.z);
        }

        /// <summary>Called by the presenter when this kite's line is cut at <paramref name="point"/>.</summary>
        public void OnCut(Vector3 point) => _cutPoint = point;

        public void Apply(in KiteSnap s, Vector3 wind, bool inContest, bool highlighted, float time, float dt)
        {
            _anchor = ArenaBuilder.ToUnity(s.Anchor);
            if (s.Status != _status) OnStatusChanged(s);
            _status = s.Status;

            switch (s.Status)
            {
                case KiteStatus.Flying:
                    FlyPose(s, time, dt);
                    DrawLine(s, inContest, highlighted, time);
                    break;
                case KiteStatus.Waiting:
                    if (HideWhenWaiting) { _kite.SetActive(false); _line.enabled = false; if (_dangle != null) _dangle.enabled = false; break; }
                    _kite.SetActive(true);
                    _kite.transform.SetPositionAndRotation(_anchor + new Vector3(0.6f, 0.2f, 0.6f), Quaternion.Euler(-60f, 0f, 0f));
                    _line.enabled = false;
                    break;
                case KiteStatus.Crashed:
                    _line.enabled = true;
                    DrawSlack(_anchor, _kite.transform.position, 0.25f);
                    break;
                default: // Cut / SelfSnapped
                    Fall(wind, dt);
                    break;
            }

            // Damage tint (durability < 50 % looks worn).
            if (_sailRenderer != null)
            {
                float worn = Mathf.InverseLerp(50f, 0f, s.Durability);
                _sailRenderer.GetPropertyBlock(_mpb);
                Color tint = Color.Lerp(Color.white, new Color(0.55f, 0.5f, 0.45f), worn);
                _mpb.SetColor("_BaseColor", tint);
                _mpb.SetColor("_Color", tint);
                _sailRenderer.SetPropertyBlock(_mpb);
            }

            ApplyDistanceScale(dt);
            UpdateTail(wind, time, dt);
        }

        /// <summary>World size of one screen-height fraction at <paramref name="distance"/> (perspective camera).</summary>
        private static float WorldPerScreen(Camera cam, float distance)
        {
            if (cam.orthographic) return cam.orthographicSize * 2f;
            return 2f * Mathf.Max(0.01f, distance) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        private void ApplyDistanceScale(float dt)
        {
            Camera cam = Camera.main;
            float target = 1f;
            if (cam != null && _kite.activeSelf)
            {
                float d = Vector3.Distance(cam.transform.position, _kite.transform.position);
                target = Mathf.Clamp(MinKiteScreenFraction * WorldPerScreen(cam, d) / KiteWorldHeight, 1f, MaxDistanceScale);
            }
            // Smooth so camera cuts never make the kite pop.
            _distanceScale = dt > 0f ? Mathf.Lerp(_distanceScale, target, 1f - Mathf.Exp(-8f * dt)) : target;
            _kite.transform.localScale = Vector3.one * _distanceScale;
        }

        /// <summary>Sets the line width so it is at least ~2 px on screen along its whole length (thicker far away).</summary>
        private void FitLineWidth(LineRenderer lr, float baseWidth)
        {
            Camera cam = Camera.main;
            int n = lr.positionCount;
            if (cam == null || n < 2) { lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f); lr.widthMultiplier = baseWidth; return; }
            Vector3 c = cam.transform.position;
            float w0 = Mathf.Max(baseWidth, MinLineScreenFraction * WorldPerScreen(cam, Vector3.Distance(c, lr.GetPosition(0))));
            float w1 = Mathf.Max(baseWidth, MinLineScreenFraction * WorldPerScreen(cam, Vector3.Distance(c, lr.GetPosition(n / 2))));
            float w2 = Mathf.Max(baseWidth, MinLineScreenFraction * WorldPerScreen(cam, Vector3.Distance(c, lr.GetPosition(n - 1))));
            float max = Mathf.Max(w0, Mathf.Max(w1, w2));
            lr.widthMultiplier = max;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, w0 / max), new Keyframe(0.5f, w1 / max), new Keyframe(1f, w2 / max));
        }

        private void OnStatusChanged(in KiteSnap s)
        {
            _endTime = Time.time;
            if (s.Status == KiteStatus.Flying)
            {
                _falling = false;
                _dangle.enabled = false;
                _kite.SetActive(true);
                Vector3 pos = ArenaBuilder.ToUnity(s.WorldPosition);
                for (int i = 0; i < _ropes.Length; i++) _ropes[i].Reset(ToN(pos), System.Numerics.Vector3.UnitY * -1f);
            }
            else if (s.Status == KiteStatus.Cut || s.Status == KiteStatus.SelfSnapped)
            {
                _falling = true;
                _fallVelocity = Vector3.down * 1.5f;
                _fallSpin = Random.Range(140f, 260f) * (Random.value > 0.5f ? 1f : -1f);
                if (s.Status == KiteStatus.SelfSnapped || _cutPoint == Vector3.zero)
                    _cutPoint = Vector3.Lerp(_anchor, _kite.transform.position, 0.35f);
                _dangle.enabled = true;
            }
        }

        private void FlyPose(in KiteSnap s, float time, float dt)
        {
            Vector3 pos = ArenaBuilder.ToUnity(s.WorldPosition);
            Vector3 dir = ArenaBuilder.ToUnity(s.Direction);
            Vector3 nose = ArenaBuilder.ToUnity(s.Nose);
            // Banking: roll into turns, settle back. Idle flutter is stronger when the line is slack.
            float turnRate = dt > 0f ? Mathf.DeltaAngle(_lastHeading, s.Heading) / dt : 0f;
            _lastHeading = s.Heading;
            _bank = Mathf.Lerp(_bank, Mathf.Clamp(-turnRate * 0.12f, -35f, 35f), 1f - Mathf.Exp(-6f * dt));
            float flutter = Mathf.Sin(time * 11f + Index) * (s.Action == LineAction.Dheel ? 6f : 2f);
            Quaternion rot = Quaternion.LookRotation(dir, nose) * Quaternion.Euler(0f, flutter, _bank);
            _kite.SetActive(true);
            _kite.transform.SetPositionAndRotation(pos, rot);
        }

        private void DrawLine(in KiteSnap s, bool inContest, bool highlighted, float time)
        {
            _line.enabled = true;
            _dangle.enabled = false;
            LineGeometry.BuildLine(LineOrigin(s), ToN(_kite.transform.position), s.Tension / 100f, 0.06f, _linePts);
            _line.positionCount = _linePts.Length;
            Vector3 side = Vector3.Cross(ArenaBuilder.ToUnity(s.Direction), Vector3.up).normalized;
            for (int i = 0; i < _linePts.Length; i++)
            {
                Vector3 p = ArenaBuilder.ToUnity(_linePts[i]);
                if (inContest)
                {
                    float t = i / (_linePts.Length - 1f);
                    p += side * Mathf.Sin(time * 70f + i) * 0.08f * Mathf.Sin(t * Mathf.PI);
                }
                _line.SetPosition(i, p);
            }
            Color c = highlighted ? new Color(1f, 0.85f, 0.2f, 1f) : _lineColor;
            if (s.Zone == TensionZone.Danger) c = Color.Lerp(c, new Color(1f, 0.3f, 0.25f), 0.6f);
            _line.startColor = _line.endColor = c;
            FitLineWidth(_line, highlighted ? 0.09f : 0.05f);
        }

        private void DrawSlack(Vector3 from, Vector3 to, float sag)
        {
            LineGeometry.BuildLine(ToN(from), ToN(to), 0f, sag, _linePts);
            _line.positionCount = _linePts.Length;
            for (int i = 0; i < _linePts.Length; i++) _line.SetPosition(i, ArenaBuilder.ToUnity(_linePts[i]));
            _line.startColor = _line.endColor = new Color(_lineColor.r, _lineColor.g, _lineColor.b, 0.6f);
            FitLineWidth(_line, 0.05f);
        }

        private void Fall(Vector3 wind, float dt)
        {
            if (!_falling) return;
            Transform k = _kite.transform;
            if (k.position.y > 0.3f)
            {
                _fallVelocity += (wind * 0.35f - _fallVelocity) * Mathf.Clamp01(dt * 0.8f);
                _fallVelocity.y = Mathf.Max(_fallVelocity.y - 2.2f * dt, -4.5f);
                k.position += _fallVelocity * dt;
                k.Rotate(Vector3.forward, _fallSpin * dt, Space.Self);
                k.Rotate(Vector3.right, _fallSpin * 0.3f * dt, Space.Self);
            }
            else if (k.position.y < 0.3f)
            {
                k.position = new Vector3(k.position.x, 0.3f, k.position.z);
                k.rotation = Quaternion.Slerp(k.rotation, Quaternion.Euler(90f, k.eulerAngles.y, 0f), dt * 3f);
            }

            float age = Time.time - _endTime;
            bool showLines = age < 8f;
            _line.enabled = showLines;
            _dangle.enabled = showLines;
            if (!showLines) return;
            // Flyer's piece hangs from the cut point; the kite drags its own piece.
            DrawSlack(_anchor, _cutPoint + Vector3.down * Mathf.Min(age * 3f, 15f), 0.3f);
            _dangle.positionCount = 6;
            for (int i = 0; i < 6; i++) _dangle.SetPosition(i, k.position + Vector3.down * (i * 1.2f) - _fallVelocity * (i * 0.15f));
        }

        private void UpdateTail(Vector3 wind, float time, float dt)
        {
            if (_ropes == null || dt <= 0f) return;
            bool visible = _kite.activeSelf;
            float step = Mathf.Min(dt, 1f / 30f);
            for (int i = 0; i < _ropes.Length; i++)
            {
                _ropeLines[i].enabled = visible;
                if (!visible) continue;
                _ropeLines[i].widthMultiplier = 0.16f * _distanceScale;
                _ropes[i].Step(ToN(TailAttach(i)), ToN(wind), time, step);
                _ropes[i].CopyPoints(_ropePts);
                for (int p = 0; p < _ropePts.Length; p++) _ropeLines[i].SetPosition(p, ArenaBuilder.ToUnity(_ropePts[p]));
            }
        }

        private static System.Numerics.Vector3 ToN(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
    }
}
