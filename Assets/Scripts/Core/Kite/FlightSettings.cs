using System;

namespace Kitefall.Core
{
    /// <summary>
    /// Arcade flight tunables for KiteFlightSim. None of these are fixed by the spec –
    /// they are starting values for playtesting (spec: "every number is a starting value").
    /// </summary>
    [Serializable]
    public class FlightSettings
    {
        // ---- Speed -------------------------------------------------------
        /// <summary>Speed with a speed stat of 0, m/s.</summary>
        public float speedBase = 4f;
        /// <summary>m/s added per kite speed stat point (Guddi 3 → 4 + 4.5 = 8.5 m/s).</summary>
        public float speedPerStat = 1.5f;
        /// <summary>m/s of kite speed added per m/s of wind.</summary>
        public float windSpeedFactor = 0.3f;
        /// <summary>How fast speed changes toward its target, m/s².</summary>
        public float acceleration = 12f;
        public float pullSpeedMult = 1.4f;
        public float dheelSpeedMult = 0.35f;
        public float reelSpeedMult = 1.1f;
        public float idleSpeedMult = 1f;

        // ---- Wind window -------------------------------------------------
        /// <summary>Kite can fly this many degrees left/right of straight downwind.</summary>
        public float windowHalfAngleDeg = 75f;
        /// <summary>Power multiplier at the side edge of the window.</summary>
        public float edgePowerMin = 0.35f;
        /// <summary>Highest elevation the kite can reach (zenith), degrees.</summary>
        public float maxElevationDeg = 82f;
        /// <summary>Power multiplier at the zenith.</summary>
        public float zenithPowerMin = 0.4f;
        /// <summary>Degrees/second the kite is pushed back when outside the window (e.g. after a wind shift).</summary>
        public float edgeReturnDegPerSec = 30f;

        // ---- Steering ----------------------------------------------------
        /// <summary>Joystick magnitude below this is ignored.</summary>
        public float steerDeadzone = 0.2f;
        /// <summary>With no steering input, heading returns to "up" at this rate × stability.</summary>
        public float stabilizeDegPerSecPerStat = 12f;
        /// <summary>Random wobble rate (deg/s) divided by stability. 0 disables wobble.</summary>
        public float wobbleDegPerSec = 25f;
        /// <summary>Extra wobble factor while the line is slack (dheel).</summary>
        public float slackWobbleMult = 2.5f;

        // ---- Height ------------------------------------------------------
        /// <summary>Lift in deg/s per m/s of wind at full tension.</summary>
        public float windLiftDegPerSecPerMs = 0.3f;
        /// <summary>Constant gravity sink, deg/s.</summary>
        public float gravitySinkDegPerSec = 1f;
        /// <summary>Extra sink while releasing line, deg/s.</summary>
        public float dheelSinkDegPerSec = 2.5f;
        /// <summary>Below this speed the kite stalls and sinks faster, m/s.</summary>
        public float stallSpeed = 3f;
        public float stallSinkDegPerSec = 10f;
        /// <summary>While slack the kite drifts back to the window centre at this rate.</summary>
        public float slackDriftDegPerSec = 8f;

        // ---- Line --------------------------------------------------------
        /// <summary>Kite distance = lineOut × lerp(sagMin, 1, tension/100). Models line sag.</summary>
        public float sagMin = 0.9f;
        /// <summary>How fast the kite distance follows its target, m/s.</summary>
        public float radiusFollowSpeed = 40f;
        /// <summary>Shortest line the charkhi can reel to, m.</summary>
        public float minLineOut = 10f;

        // ---- Dive (Ghotta) – milestone 5 ----------------------------------
        /// <summary>How long a dive lasts, s.</summary>
        public float diveDuration = 1.0f;
        /// <summary>Speed multiplier while diving.</summary>
        public float diveSpeedMult = 1.9f;
        /// <summary>Turn-rate multiplier used to swing the nose down at dive start.</summary>
        public float diveTurnMult = 3f;
        /// <summary>Seconds before the next dive is possible.</summary>
        public float diveCooldown = 1.5f;

        // ---- Start / crash -------------------------------------------------
        public float startElevationDeg = 35f;
        public float startLineOut = 60f;
        /// <summary>Kite crashes when its height above ground drops below this, m.</summary>
        public float crashHeight = 0.5f;
    }
}
