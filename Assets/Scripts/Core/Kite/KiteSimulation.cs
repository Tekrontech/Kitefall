namespace Kitefall.Core
{
    /// <summary>
    /// One complete kite: input → line action → tension → line out → flight.
    /// This is the class the server will run later (milestone 10); the Unity
    /// KiteController only feeds it input and draws the result.
    /// </summary>
    public sealed class KiteSimulation
    {
        public LoadoutStats Loadout { get; }
        public TensionModel Tension { get; }
        public LineSpool Spool { get; }
        public KiteFlightSim Flight { get; }
        public LineActionResolver ActionResolver { get; }

        public KiteStatus Status { get; private set; }
        public LineAction CurrentAction { get; private set; }

        private readonly TensionSettings _tensionSettings;
        private readonly FlightSettings _flightSettings;

        public KiteSimulation(
            in LoadoutStats loadout,
            TensionSettings tensionSettings,
            FlightSettings flightSettings,
            float anchorHeight,
            float dorStockMeters,
            bool dheelOnPullRelease = true,
            int seed = 0)
        {
            Loadout = loadout;
            _tensionSettings = tensionSettings ?? new TensionSettings();
            _flightSettings = flightSettings ?? new FlightSettings();

            Tension = new TensionModel(_tensionSettings, loadout);
            Spool = new LineSpool(loadout.CapacityMeters, loadout.ReelSpeed, loadout.ReleaseSpeed,
                dorStockMeters, _flightSettings.minLineOut, _flightSettings.startLineOut);
            Flight = new KiteFlightSim(_flightSettings, loadout, anchorHeight, seed);
            ActionResolver = new LineActionResolver(dheelOnPullRelease);
        }

        /// <summary>Puts the kite back in the air, straight downwind.</summary>
        public void Reset(float windDirectionDeg)
        {
            Tension.Reset(_tensionSettings.idleTarget);
            Spool.Reset(_flightSettings.startLineOut);
            Flight.Reset(windDirectionDeg, Spool.LineOut);
            ActionResolver.Reset();
            CurrentAction = LineAction.Idle;
            Status = KiteStatus.Flying;
        }

        public KiteStepResult Step(in KiteInput input, in WindSample wind, float time, float dt)
        {
            if (Status != KiteStatus.Flying) return default;

            LineAction action = ActionResolver.Resolve(input.PullHeld, input.ReelHeld, Spool.IsAtMax);
            bool actionChanged = action != CurrentAction;
            CurrentAction = action;

            TensionStepResult t = Tension.Step(action, wind.IsStrong, dt);
            Spool.Step(action, dt);

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

            bool snapped = t.SnappedThisStep;
            bool crashed = !snapped && Flight.IsCrashed;
            if (snapped) Status = KiteStatus.SelfSnapped;
            else if (crashed) Status = KiteStatus.Crashed;

            return new KiteStepResult(t.ZoneChanged, snapped, crashed, actionChanged);
        }
    }
}
