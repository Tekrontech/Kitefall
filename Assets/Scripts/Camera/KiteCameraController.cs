using Kitefall.Data;
using Kitefall.Kite;
using UnityEngine;

namespace Kitefall.CameraRig
{
    /// <summary>
    /// Follow camera: sits on the line between the flyer and the kite, a little behind the kite,
    /// so the player sees the kite large and the line running back toward the rooftop.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class KiteCameraController : MonoBehaviour
    {
        [SerializeField] private KiteController kite;
        [SerializeField] private CameraConfig config;

        private UnityEngine.Camera _camera;
        private Vector3 _velocity;

        private float FollowDistance => config != null ? config.followDistance : 16f;
        private float HeightOffset => config != null ? config.heightOffset : 2f;
        private float SmoothTime => config != null ? config.positionSmoothTime : 0.25f;
        private float RotationSharpness => config != null ? config.rotationSharpness : 8f;
        private float LookTowardFlyer => config != null ? config.lookTowardFlyer : 0.05f;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            if (config != null) _camera.fieldOfView = config.fieldOfView;
        }

        private void Start() => SnapToTarget();

        /// <summary>Jumps straight to the follow position (no smoothing).</summary>
        public void SnapToTarget()
        {
            if (!TryGetTargets(out Vector3 kitePos, out Vector3 anchorPos)) return;
            transform.position = DesiredPosition(kitePos, anchorPos);
            transform.rotation = DesiredRotation(kitePos, anchorPos);
            _velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!TryGetTargets(out Vector3 kitePos, out Vector3 anchorPos)) return;

            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(kitePos, anchorPos), ref _velocity, SmoothTime);
            float t = 1f - Mathf.Exp(-RotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, DesiredRotation(kitePos, anchorPos), t);
        }

        private bool TryGetTargets(out Vector3 kitePos, out Vector3 anchorPos)
        {
            kitePos = anchorPos = Vector3.zero;
            if (kite == null || kite.KiteVisual == null || kite.Anchor == null) return false;
            kitePos = kite.KiteVisual.position;
            anchorPos = kite.Anchor.position;
            return true;
        }

        private Vector3 DesiredPosition(Vector3 kitePos, Vector3 anchorPos)
        {
            Vector3 toFlyer = anchorPos - kitePos;
            float lineLength = toFlyer.magnitude;
            if (lineLength < 0.01f) return kitePos - Vector3.forward * FollowDistance;
            // Never further back than the flyer.
            float distance = Mathf.Min(FollowDistance, lineLength * 0.9f);
            return kitePos + toFlyer / lineLength * distance + Vector3.up * HeightOffset;
        }

        private Quaternion DesiredRotation(Vector3 kitePos, Vector3 anchorPos)
        {
            Vector3 lookAt = Vector3.Lerp(kitePos, anchorPos, LookTowardFlyer);
            Vector3 forward = lookAt - transform.position;
            if (forward.sqrMagnitude < 0.0001f) forward = kitePos - anchorPos;
            return Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
