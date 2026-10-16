using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>Everything the flight sim needs for one step.</summary>
    public struct FlightInput
    {
        /// <summary>Steer joystick, x = right, y = up, magnitude 0–1.</summary>
        public float SteerX;
        public float SteerY;
        public LineAction Action;
        public WindSample Wind;
        /// <summary>Tension 0–1.</summary>
        public float Tension01;
        /// <summary>Line out on the charkhi, m.</summary>
        public float LineOut;
        /// <summary>Match time, s (drives deterministic wobble).</summary>
        public float Time;
    }

    /// <summary>
    /// Arcade kite flight on a "wind window" sphere around the flyer (the anchor).
    ///
    /// The kite lives at spherical coordinates (azimuth, elevation, radius) around the anchor:
    ///  • azimuth  – degrees clockwise from world +Z; the window is centred on the wind direction.
    ///  • elevation – degrees above the horizon.
    ///  • radius   – distance from the anchor; always ≤ line out.
    /// The kite moves along its <see cref="HeadingDeg"/> (0 = up, +90 = right as seen by the flyer).
    /// The joystick turns the heading at the kite's turn rate, like real fighter kites.
    /// </summary>
    public sealed class KiteFlightSim
    {
        private readonly FlightSettings _s;
        private LoadoutStats _loadout;
        private readonly SmoothNoise _wobbleNoise;

        public float AzimuthDeg { get; private set; }
        public float ElevationDeg { get; private set; }
        public float Radius { get; private set; }
        public float HeadingDeg { get; private set; }
        public float Speed { get; private set; }
        public float AnchorHeight { get; }
        public bool IsCrashed { get; private set; }
        public bool IsDiving => DiveTimeLeft > 0f;
        public float DiveTimeLeft { get; private set; }
        public float DiveCooldownLeft { get; private set; }
        public bool CanDive => !IsCrashed && DiveCooldownLeft <= 0f && !IsDiving;

        /// <summary>Height above the ground, m.</summary>
        public float Height => AnchorHeight + Radius * KMath.Sin(ElevationDeg * KMath.Deg2Rad);

        /// <summary>Kite position relative to the anchor (Unity axes: x right, y up, z forward).</summary>
        public Vector3 LocalPosition => Direction * Radius;

        /// <summary>Unit vector from the anchor to the kite.</summary>
        public Vector3 Direction => DirectionOf(AzimuthDeg, ElevationDeg);

        /// <summary>Unit vector the kite's nose points to (tangent to the sphere).</summary>
        public Vector3 NoseDirection => NoseOf(AzimuthDeg, ElevationDeg, HeadingDeg);

        public static Vector3 DirectionOf(float azimuthDeg, float elevationDeg)
        {
            float az = azimuthDeg * KMath.Deg2Rad, el = elevationDeg * KMath.Deg2Rad;
            return new Vector3(KMath.Cos(el) * KMath.Sin(az), KMath.Sin(el), KMath.Cos(el) * KMath.Cos(az));
        }

        public static Vector3 NoseOf(float azimuthDeg, float elevationDeg, float headingDeg)
        {
            float az = azimuthDeg * KMath.Deg2Rad, el = elevationDeg * KMath.Deg2Rad, h = headingDeg * KMath.Deg2Rad;
            var up = new Vector3(-KMath.Sin(el) * KMath.Sin(az), KMath.Cos(el), -KMath.Sin(el) * KMath.Cos(az));
            var right = new Vector3(KMath.Cos(az), 0f, -KMath.Sin(az));
            return Vector3.Normalize(up * KMath.Cos(h) + right * KMath.Sin(h));
        }

        public KiteFlightSim(FlightSettings settings, in LoadoutStats loadout, float anchorHeight, int seed = 0)
        {
            _s = settings ?? new FlightSettings();
            _loadout = loadout;
            AnchorHeight = anchorHeight;
            _wobbleNoise = new SmoothNoise(seed + 99);
        }

        /// <summary>Places the kite straight downwind at the start elevation.</summary>
        /// <param name="bonusHeight">Extra start height, m (perfect launch = +10 m, spec 6).</param>
        public void Reset(float windDirectionDeg, float lineOut, float bonusHeight = 0f, float azimuthOffsetDeg = 0f)
        {
            AzimuthDeg = KMath.WrapAngle(windDirectionDeg + azimuthOffsetDeg);
            Radius = KMath.Max(1f, lineOut);
            float bonusDeg = bonusHeight > 0f ? (float)System.Math.Asin(KMath.Clamp(bonusHeight / Radius, 0f, 0.9f)) * KMath.Rad2Deg : 0f;
            ElevationDeg = KMath.Min(_s.maxElevationDeg - 5f, _s.startElevationDeg + bonusDeg);
            HeadingDeg = 0f;
            Speed = 0f;
            IsCrashed = false;
            DiveTimeLeft = 0f;
            DiveCooldownLeft = 0f;
        }

        /// <summary>Overwrites the state (network correction / replays).</summary>
        public void SetState(float azimuthDeg, float elevationDeg, float radius, float headingDeg, float speed)
        {
            AzimuthDeg = azimuthDeg;
            ElevationDeg = elevationDeg;
            Radius = KMath.Max(1f, radius);
            HeadingDeg = headingDeg;
            Speed = speed;
        }

        /// <summary>Swaps the loadout mid-match (sky crate / loadout break).</summary>
        public void SetLoadout(in LoadoutStats loadout) => _loadout = loadout;

        /// <summary>Marks the kite as crashed (e.g. it hit a building).</summary>
        public void ForceCrash() => IsCrashed = true;

        /// <summary>Starts a Dive (Ghotta). Returns false while on cooldown.</summary>
        public bool StartDive()
        {
            if (!CanDive) return false;
            DiveTimeLeft = _s.diveDuration;
            DiveCooldownLeft = _s.diveDuration + _s.diveCooldown;
            return true;
        }

        public void Step(in FlightInput input, float dt)
        {
            if (IsCrashed || dt <= 0f) return;

            bool slack = input.Action == LineAction.Dheel;
            float windDir = input.Wind.DirectionDeg;
            bool diving = DiveTimeLeft > 0f;
            if (DiveTimeLeft > 0f) DiveTimeLeft = KMath.Max(0f, DiveTimeLeft - dt);
            if (DiveCooldownLeft > 0f) DiveCooldownLeft = KMath.Max(0f, DiveCooldownLeft - dt);

            // ---- 1. Steering: rotate heading toward the joystick direction.
            float steerMag = KMath.Sqrt(input.SteerX * input.SteerX + input.SteerY * input.SteerY);
            if (diving)
            {
                // Dive: nose swings straight down fast, steering can still bias left/right a little.
                float target = 180f - KMath.Clamp(input.SteerX, -1f, 1f) * 25f;
                HeadingDeg = KMath.MoveTowardsAngle(HeadingDeg, target, _loadout.TurnRate * _s.diveTurnMult * dt);
            }
            else if (steerMag > _s.steerDeadzone)
            {
                float target = KMath.Atan2(input.SteerX, input.SteerY) * KMath.Rad2Deg;
                float turn = _loadout.TurnRate * KMath.Clamp01(steerMag) * dt;
                HeadingDeg = KMath.MoveTowardsAngle(HeadingDeg, target, turn);
            }
            else
            {
                // Stable kites self-right toward "up" when the player lets go.
                HeadingDeg = KMath.MoveTowardsAngle(HeadingDeg, 0f, _s.stabilizeDegPerSecPerStat * _loadout.Stability * dt);
            }

            if (_s.wobbleDegPerSec > 0f)
            {
                float wobble = _wobbleNoise.Sample(input.Time, 1.7f) * _s.wobbleDegPerSec / _loadout.Stability;
                if (slack) wobble *= _s.slackWobbleMult;
                HeadingDeg += wobble * dt;
            }
            HeadingDeg = KMath.WrapAngle(HeadingDeg);

            // ---- 2. Speed: class speed + wind, scaled by line action and window position.
            float azRel = KMath.DeltaAngle(windDir, AzimuthDeg);
            float edge01 = KMath.Clamp01(KMath.Abs(azRel) / KMath.Max(1f, _s.windowHalfAngleDeg));
            float el01 = KMath.InverseLerp(0f, _s.maxElevationDeg, ElevationDeg);
            float power = KMath.Lerp(1f, _s.edgePowerMin, edge01 * edge01) * KMath.Lerp(1f, _s.zenithPowerMin, el01 * el01);

            float baseSpeed = _s.speedBase + _s.speedPerStat * _loadout.SpeedStat + input.Wind.Speed * _s.windSpeedFactor;
            float targetSpeed = baseSpeed * ActionSpeedMult(input.Action) * power;
            if (diving) targetSpeed = baseSpeed * _s.diveSpeedMult;
            Speed = KMath.MoveTowards(Speed, targetSpeed, _s.acceleration * (diving ? 3f : 1f) * dt);

            // ---- 3. Move along the heading on the sphere surface.
            float h = HeadingDeg * KMath.Deg2Rad;
            float radius = KMath.Max(1f, Radius);
            float vUp = Speed * KMath.Cos(h);
            float vRight = Speed * KMath.Sin(h);
            float cosEl = KMath.Max(0.15f, KMath.Cos(ElevationDeg * KMath.Deg2Rad));

            float dEl = vUp / radius * KMath.Rad2Deg;
            float dAz = vRight / (radius * cosEl) * KMath.Rad2Deg;

            // Lift from wind (more with tension) against gravity, dheel and stall sink.
            float lift = _s.windLiftDegPerSecPerMs * input.Wind.Speed * KMath.Clamp01(input.Tension01) * power;
            float sink = _s.gravitySinkDegPerSec;
            if (slack) sink += _s.dheelSinkDegPerSec;
            if (Speed < _s.stallSpeed && _s.stallSpeed > 0f) sink += _s.stallSinkDegPerSec * (1f - Speed / _s.stallSpeed);
            dEl += lift - sink;

            ElevationDeg += dEl * dt;
            azRel += dAz * dt;

            // Slack line: the wind pushes the kite back toward the window centre.
            if (slack) azRel = KMath.MoveTowards(azRel, 0f, _s.slackDriftDegPerSec * dt);

            // Outside the window (e.g. the wind just shifted) → pushed back to the edge.
            float half = _s.windowHalfAngleDeg;
            if (azRel > half) azRel = KMath.MoveTowards(azRel, half, _s.edgeReturnDegPerSec * dt);
            else if (azRel < -half) azRel = KMath.MoveTowards(azRel, -half, _s.edgeReturnDegPerSec * dt);
            // Hard limit so a big wind shift can never put the kite behind the flyer.
            azRel = KMath.Clamp(azRel, -half - 30f, half + 30f);

            AzimuthDeg = KMath.WrapAngle(windDir + azRel);
            ElevationDeg = KMath.Clamp(ElevationDeg, -60f, _s.maxElevationDeg);

            // ---- 4. Distance follows the line: tight line = full length, slack = sag.
            float lineOut = KMath.Max(1f, input.LineOut);
            float targetRadius = lineOut * KMath.Lerp(_s.sagMin, 1f, input.Tension01);
            Radius = KMath.MoveTowards(Radius, targetRadius, _s.radiusFollowSpeed * dt);
            Radius = KMath.Clamp(Radius, 1f, lineOut);

            // ---- 5. Crash into the ground.
            if (Height < _s.crashHeight) IsCrashed = true;
        }

        private float ActionSpeedMult(LineAction action)
        {
            switch (action)
            {
                case LineAction.Pull: return _s.pullSpeedMult;
                case LineAction.Dheel: return _s.dheelSpeedMult;
                case LineAction.Reel: return _s.reelSpeedMult;
                default: return _s.idleSpeedMult;
            }
        }
    }
}
