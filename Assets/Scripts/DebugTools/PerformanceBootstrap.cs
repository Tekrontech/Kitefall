using UnityEngine;

namespace Kitefall.DebugTools
{
    /// <summary>
    /// Sets the frame-rate target (spec 0: 60 FPS mid-range, 30 FPS low-end).
    /// Settings > Graphics will drive this later; for now it is one inspector value.
    /// </summary>
    public sealed class PerformanceBootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool neverSleep = true;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
            if (neverSleep) Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
