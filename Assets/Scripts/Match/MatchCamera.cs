using UnityEngine;

namespace Kitefall.Match
{
    public enum CameraMode { FlyOver, Launch, Follow, Spectate, Replay, Showroom }

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
            Desired(out Vector3 pos, out Vector3 look);
            float smooth = Mathf.Lerp(0.45f, 0.08f, Follow);
            if (Mode == CameraMode.FlyOver || Mode == CameraMode.Showroom) smooth = 0.6f;
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref _vel, smooth, Mathf.Infinity, dt);
            Quaternion rot = Quaternion.LookRotation((look - transform.position).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-Mathf.Lerp(4f, 12f, Follow) * dt));

            if (_shake > 0f)
            {
                transform.position += Random.insideUnitSphere * _shake * 0.4f;
                _shake = Mathf.Max(0f, _shake - dt * 2.5f);
            }
        }

        private void Desired(out Vector3 pos, out Vector3 look)
        {
            Vector3 target = Target != null ? Target.position : OrbitCenter;
            switch (Mode)
            {
                case CameraMode.FlyOver:
                case CameraMode.Showroom:
                {
                    _orbit += Time.unscaledDeltaTime * (Mode == CameraMode.FlyOver ? 14f : 6f);
                    float r = Mode == CameraMode.FlyOver ? 130f : 22f;
                    Vector3 c = Mode == CameraMode.FlyOver ? OrbitCenter : target;
                    pos = c + Quaternion.Euler(0f, _orbit, 0f) * new Vector3(0f, Mode == CameraMode.FlyOver ? 40f : 4f, -r);
                    look = c;
                    return;
                }
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
