using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Kite follow camera tunables (Settings > Camera will edit these later).</summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "Kitefall/Config/Camera Config", order = 23)]
    public sealed class CameraConfig : ScriptableObject
    {
        [Tooltip("Distance from the kite back along the line toward the flyer, m.")]
        [Min(1)] public float followDistance = 16f;
        [Tooltip("Extra height above that point, m.")]
        public float heightOffset = 2f;
        [Tooltip("Lower = snappier camera.")]
        [Min(0)] public float positionSmoothTime = 0.25f;
        [Tooltip("Higher = camera turns to the kite faster.")]
        [Min(0.1f)] public float rotationSharpness = 8f;
        [Range(20, 100)] public float fieldOfView = 55f;
        [Tooltip("0 = look at the kite, 0.2 = look 20% of the way back toward the flyer.")]
        [Range(0, 1)] public float lookTowardFlyer = 0.05f;
    }
}
