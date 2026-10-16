namespace Kitefall.Core
{
    public enum KiteStatus
    {
        Flying,
        /// <summary>Own line broke from over-tension (spec 9.2).</summary>
        SelfSnapped,
        /// <summary>Hit the ground (spec 6.2).</summary>
        Crashed
    }

    /// <summary>Raw player input for one tick (what the client would send to the server).</summary>
    public struct KiteInput
    {
        public float SteerX;
        public float SteerY;
        public bool PullHeld;
        public bool ReelHeld;
    }

    /// <summary>What happened during one simulation step.</summary>
    public readonly struct KiteStepResult
    {
        public readonly bool ZoneChanged;
        public readonly bool SelfSnapped;
        public readonly bool Crashed;
        public readonly bool ActionChanged;

        public KiteStepResult(bool zoneChanged, bool selfSnapped, bool crashed, bool actionChanged)
        {
            ZoneChanged = zoneChanged;
            SelfSnapped = selfSnapped;
            Crashed = crashed;
            ActionChanged = actionChanged;
        }
    }
}
