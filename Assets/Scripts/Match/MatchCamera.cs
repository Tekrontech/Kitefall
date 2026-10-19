using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>Follow = "KiteCam" (kept for compatibility). Shoulder = PUBG-style third person (upgrade W2).</summary>
    public enum CameraMode { FlyOver, Launch, Follow, Spectate, Replay, Showroom, Shoulder, PrepClose, PrepWide, Map, HomeShot }

    /// <summary>
    /// Match camera: arena fly-over (intro), behind the flyer at launch, follow the kite along the line,
    /// spectate, replay focus, and a slow orbit for the home showroom. Shake on cuts (Settings ▸ Camera).
    /// </summary>
    public sealed class MatchCamera : MonoBehaviour
    {
        public CameraMode Mode = CameraMode.Follow;
        public Transform Target;
        public Vector3 Anchor;
        public Vector3 OrbitCenter;
        public float Distance = 16f;
        /// <summary>0 = loose, 1 = tight.</summary>
        public float Follow = 0.5f;
        public bool ShakeEnabled = true;
        /// <summary>The character for Shoulder / Prep modes (upgrade W2).</summary>
        public Transform Character;
        public Transform CharacterHead;
        /// <summary>Blend time for mode changes (KiteCam ↔ Shoulder = 0.35 s).</summary>
        public float BlendTime = 0.35f;
        public float BaseFov = 55f;
        public float MaxFov = 70f;
        public Vector3 MapCenter;
        public float MapHeight = 120f;
        /// <summary>Showroom orbit (home: 22 m around the kite; wardrobe turntable: ~3 m around the character).</summary>
        public float OrbitRadius = 22f;
        public float OrbitHeight = 4f;
        public float OrbitSpeed = 6f;

        private CameraMode _lastMode;
        private float _blend = 1f;

        private Vector3 _vel;
        private float _shake;
        private float _orbit;
        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _cam.nearClipPlane = 0.3f;
                _cam.farClipPlane = 2500f;
                _cam.fieldOfView = 55f;
            }
        }

        public void Shake(float amount)
        {
            if (ShakeEnabled) _shake = Mathf.Max(_shake, amount);
        }

        public void Snap()
        {
            Vector3 pos, look;
            Desired(out pos, out look);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
            _vel = Vector3.zero;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (Mode != _lastMode) { _blend = 0f; _lastMode = Mode; }
            _blend = Mathf.MoveTowards(_blend, 1f, dt / Mathf.Max(0.05f, BlendTime));
            Desired(out Vector3 pos, out Vector3 look);
            float smooth = Mathf.Lerp(0.45f, 0.08f, Follow);
            if (Mode == CameraMode.FlyOver || Mode == CameraMode.Showroom || Mode == CameraMode.HomeShot) smooth = 0.6f;
            if (Mode == CameraMode.Shoulder || Mode == CameraMode.PrepClose || Mode == CameraMode.PrepWide || Mode == CameraMode.Map)
                smooth = Mathf.Lerp(BlendTime * 0.5f, 0.06f, _blend);
            UpdateFov(dt);
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref _vel, smooth, Mathf.Infinity, dt);
            Quaternion rot = Quaternion.LookRotation((look - transform.position).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-Mathf.Lerp(4f, 12f, Follow) * dt));

            if (_shake > 0f)
            {
                transform.position += Random.insideUnitSphere * _shake * 0.4f;
                _shake = Mathf.Max(0f, _shake - dt * 2.5f);
            }
        }

        /// <summary>Shoulder mode: widen the FOV (up to 70°) before the kite can leave the frame.</summary>
        private void UpdateFov(float dt)
        {
            if (_cam == null) return;
            float want = BaseFov;
            if (Mode == CameraMode.Shoulder && Target != null)
            {
                Vector3 v = _cam.WorldToViewportPoint(Target.position);
                bool outside = v.z < 0f || v.x < 0.06f || v.x > 0.94f || v.y < 0.06f || v.y > 0.94f;
                bool edge = v.x < 0.15f || v.x > 0.85f || v.y < 0.12f || v.y > 0.9f;
                want = outside ? MaxFov : edge ? Mathf.Lerp(BaseFov, MaxFov, 0.6f) : BaseFov;
            }
            _cam.fieldOfView = Mathf.MoveTowards(_cam.fieldOfView, want, dt * 40f);
        }

        private void Desired(out Vector3 pos, out Vector3 look)
        {
            Vector3 target = Target != null ? Target.position : OrbitCenter;
            switch (Mode)
            {
                case CameraMode.Shoulder:
                case CameraMode.PrepClose:
                case CameraMode.PrepWide:
                {
                    Vector3 c = Character != null ? Character.position : Anchor - Vector3.up * 1.35f;
                    Vector3 head = CharacterHead != null ? CharacterHead.position : c + Vector3.up * 1.65f;
                    Vector3 fwd = target - c;
                    fwd.y = 0f;
                    fwd = fwd.sqrMagnitude < 0.01f ? (Character != null ? Character.forward : Vector3.forward) : fwd.normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd);
                    if (Mode == CameraMode.Shoulder)
                    {
                        // Behind and right of the character; character lower-left, kite upper-right (upgrade W2).
                        pos = c - fwd * 4.5f + right * 1.2f + Vector3.up * 2.0f;
                        look = Vector3.Lerp(head, target, 0.85f);
                        float maxLook = 60f;
                        if ((look - head).magnitude > maxLook) look = head + (look - head).normalized * maxLook;
                    }
                    else if (Mode == CameraMode.PrepClose)
                    {
                        pos = c - fwd * 1.7f + right * 0.75f + Vector3.up * 1.55f;
                        look = c + fwd * 1.1f + Vector3.up * 0.7f;
                    }
                    else
                    {
                        pos = c + right * 7.5f - fwd * 3f + Vector3.up * 3.2f;
                        look = c + fwd * 6f + Vector3.up * 1.8f;
                    }
                    return;
                }
                case CameraMode.Map:
                    pos = MapCenter + new Vector3(0f, MapHeight, -MapHeight * 0.35f);
                    look = MapCenter;
                    return;
                case CameraMode.FlyOver:
                case CameraMode.Showroom:
                {
                    _orbit += Time.unscaledDeltaTime * (Mode == CameraMode.FlyOver ? 14f : OrbitSpeed);
                    float r = Mode == CameraMode.FlyOver ? 130f : OrbitRadius;
                    Vector3 c = Mode == CameraMode.FlyOver ? OrbitCenter : target;
                    pos = c + Quaternion.Euler(0f, _orbit, 0f) * new Vector3(0f, Mode == CameraMode.FlyOver ? 40f : OrbitHeight, -r);
                    look = c;
                    return;
                }
                case CameraMode.HomeShot:
                    // Home design: behind and right of the flyer, looking past them at the kite and the sky (flyer sits left of centre).
                    pos = Anchor + new Vector3(3.2f, 2.4f, -7.5f);
                    look = Vector3.Lerp(Anchor + new Vector3(0f, 6f, 30f), target, 0.25f);
                    return;
                case CameraMode.Launch:
                    pos = Anchor + new Vector3(0f, 3.5f, -9f);
                    look = Anchor + new Vector3(0f, 20f, 50f);
                    return;
                case CameraMode.Replay:
                    pos = target + new Vector3(8f, 4f, -10f);
                    look = target;
                    return;
                default:
                {
                    Vector3 toFlyer = Anchor - target;
                    float len = toFlyer.magnitude;
                    if (len < 0.1f) toFlyer = Vector3.back; else toFlyer /= len;
                    float d = Mathf.Min(Distance, Mathf.Max(4f, len * 0.9f));
                    pos = target + toFlyer * d + Vector3.up * 2.2f;
                    look = Vector3.Lerp(target, Anchor, 0.05f);
                    if (pos.y < 2f) pos.y = 2f;
                    return;
                }
            }
        }
    }
}
