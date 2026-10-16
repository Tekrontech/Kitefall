using System;
using System.Numerics;

namespace Kitefall.Core
{
    [Serializable]
    public class LineSettings
    {
        /// <summary>Segments used for contact checks and drawing.</summary>
        public int segments = 8;
        /// <summary>Sag at zero tension as a fraction of line length.</summary>
        public float maxSagFraction = 0.06f;
    }

    /// <summary>
    /// One complete kite: input → line action → tension → line out → flight → line shape.
    /// The server runs this for every kite; Unity only draws it.
    /// </summary>
    public sealed class KiteSimulation
    {
        public LoadoutStats Loadout { get; private set; }
        public TensionModel Tension { get; private set; }
        public LineSpool Spool { get; private set; }
        public KiteFlightSim Flight { get; }
        public LineActionResolver ActionResolver { get; }

        public KiteStatus Status { get; private set; }
        public LineAction CurrentAction { get; private set; }
        /// <summary>World position of the flyer's hands.</summary>
        public Vector3 Anchor { get; set; }
        public float GroundHeight { get; }
        /// <summary>Kite durability 0–100 %.</summary>
        public float Durability { get; set; } = 100f;
        /// <summary>Seconds since the last dive started (large if never).</summary>
        public float TimeSinceDive { get; private set; } = 999f;
        /// <summary>Points of the line from the anchor (index 0) to the kite (last).</summary>
        public Vector3[] LinePoints { get; }
        /// <summary>Longest line out this flight (used for dor consumption).</summary>
        public float MaxLineOutReached { get; private set; }
        /// <summary>Optional world collision (buildings). Returns true if the point is inside something solid.</summary>
        public Func<Vector3, bool> IsSolid { get; set; }
        /// <summary>Last input received (bots and HUD read it).</summary>
        public KiteInput LastInput { get; private set; }

        public Vector3 WorldPosition => Anchor + Flight.LocalPosition;
        public bool IsFlying => Status == KiteStatus.Flying;

        private readonly TensionSettings _tensionSettings;
        private readonly FlightSettings _flightSettings;
        private readonly LineSettings _lineSettings;
        private readonly float _dorStockMeters;
        private bool _wasDiveHeld;

        /// <summary>Milestone-1 constructor: anchor at (0, anchorHeight, 0), ground at 0.</summary>
        public KiteSimulation(
            in LoadoutStats loadout,
            TensionSettings tensionSettings,
            FlightSettings flightSettings,
            float anchorHeight,
            float dorStockMeters,
            bool dheelOnPullRelease = true,
            int seed = 0)
            : this(loadout, tensionSettings, flightSettings, null, new Vector3(0f, anchorHeight, 0f), 0f, dorStockMeters, dheelOnPullRelease, seed)
        {
        }

        public KiteSimulation(
            in LoadoutStats loadout,
            TensionSettings tensionSettings,
            FlightSettings flightSettings,
            LineSettings lineSettings,
            Vector3 anchor,
            float groundHeight,
            float dorStockMeters,
            bool dheelOnPullRelease = true,
            int seed = 0)
        {
            Loadout = loadout;
            _tensionSettings = tensionSettings ?? new TensionSettings();
            _flightSettings = flightSettings ?? new FlightSettings();
            _lineSettings = lineSettings ?? new LineSettings();
            _dorStockMeters = dorStockMeters;
            Anchor = anchor;
            GroundHeight = groundHeight;

            Tension = new TensionModel(_tensionSettings, loadout);
            Spool = NewSpool(loadout, _flightSettings.startLineOut);
            Flight = new KiteFlightSim(_flightSettings, loadout, anchor.Y - groundHeight, seed);
            ActionResolver = new LineActionResolver(dheelOnPullRelease);
            LinePoints = new Vector3[Math.Max(2, _lineSettings.segments + 1)];
            Status = KiteStatus.Waiting;
        }

        private LineSpool NewSpool(in LoadoutStats loadout, float lineOut) =>
            new LineSpool(loadout.CapacityMeters, loadout.ReelSpeed, loadout.ReleaseSpeed, _dorStockMeters, _flightSettings.minLineOut, lineOut);

        /// <summary>Puts the kite in the air, straight downwind.</summary>
        public void Reset(float windDirectionDeg, float bonusHeight = 0f, float azimuthOffsetDeg = 0f)
        {
            Tension.Reset(_tensionSettings.idleTarget);
            Spool.Reset(_flightSettings.startLineOut);
            Flight.Reset(windDirectionDeg, Spool.LineOut, bonusHeight, azimuthOffsetDeg);
            ActionResolver.Reset();
            CurrentAction = LineAction.Idle;
            Status = KiteStatus.Flying;
            TimeSinceDive = 999f;
            MaxLineOutReached = Spool.LineOut;
            _wasDiveHeld = false;
            UpdateLine();
        }

        /// <summary>Kite back on the roof, not flying.</summary>
        public void SetWaiting() => Status = KiteStatus.Waiting;

        /// <summary>Ends the flight for a reason decided outside (cut by an opponent, round over…).</summary>
        public void EndFlight(KiteStatus status)
        {
            if (status == KiteStatus.Flying) return;
            Status = status;
        }

        /// <summary>Swaps dum / charkhi / dor stats (loadout break or sky crate). Tension and line out are kept.</summary>
        public void ChangeLoadout(in LoadoutStats loadout)
        {
            float t = Tension.Tension;
            float line = Spool.LineOut;
            Loadout = loadout;
            Tension = new TensionModel(_tensionSettings, loadout);
            Tension.Reset(t);
            Spool = NewSpool(loadout, line);
            Flight.SetLoadout(loadout);
        }

        /// <summary>Adds dor stock for this match (sky crate "Dor refill").</summary>
        public void AddStock(float meters)
        {
            float line = Spool.LineOut;
            Spool = new LineSpool(Loadout.CapacityMeters, Loadout.ReelSpeed, Loadout.ReleaseSpeed, Spool.StockMeters + meters, _flightSettings.minLineOut, line);
        }

        public KiteStepResult Step(in KiteInput input, in WindSample wind, float time, float dt)
        {
            LastInput = input;
            if (Status != KiteStatus.Flying) return default;

            TimeSinceDive += dt;
            bool diveStarted = false;
            bool diveEdge = input.DiveHeld && !_wasDiveHeld;
            _wasDiveHeld = input.DiveHeld;
            if (diveEdge && Flight.StartDive())
            {
                TimeSinceDive = 0f;
                diveStarted = true;
            }

            LineAction action = ActionResolver.Resolve(input.PullHeld, input.ReelHeld, Spool.IsAtMax);
            bool actionChanged = action != CurrentAction;
            CurrentAction = action;

            TensionStepResult t = Tension.Step(action, wind.IsStrong, dt);
            Spool.Step(action, dt);
            if (Spool.LineOut > MaxLineOutReached) MaxLineOutReached = Spool.LineOut;

            var flightInput = new FlightInput
            {
                SteerX = input.SteerX,
                SteerY = input.SteerY,
                Action = action,
                Wind = wind,
                Tension01 = Tension.Tension / 100f,
                LineOut = Spool.LineOut,
                Time = time
            };
            Flight.Step(flightInput, dt);

            if (!Flight.IsCrashed && IsSolid != null && IsSolid(WorldPosition)) Flight.ForceCrash();
            if (Durability <= 0f) Flight.ForceCrash();

            bool snapped = t.SnappedThisStep;
            bool crashed = !snapped && Flight.IsCrashed;
            if (snapped) Status = KiteStatus.SelfSnapped;
            else if (crashed) Status = KiteStatus.Crashed;

            UpdateLine();
            return new KiteStepResult(t.ZoneChanged, snapped, crashed, actionChanged, diveStarted);
        }

        /// <summary>Recomputes <see cref="LinePoints"/> from the current state.</summary>
        public void UpdateLine()
        {
            LineGeometry.BuildLine(Anchor, WorldPosition, Tension.Tension / 100f, _lineSettings.maxSagFraction, LinePoints);
        }
    }
}
