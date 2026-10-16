using System;
using Kitefall.Core;
using Kitefall.Data;
using Kitefall.Input;
using Kitefall.Wind;
using UnityEngine;

namespace Kitefall.Kite
{
    /// <summary>
    /// Milestone 1 – one kite flown by the local player.
    ///
    /// All the rules live in the pure C# <see cref="KiteSimulation"/> (Kitefall.Core).
    /// This component only:
    ///   1. reads input (KiteInputReader) and wind (WindField),
    ///   2. steps the simulation in FixedUpdate,
    ///   3. moves the kite visual smoothly in Update (interpolation),
    ///   4. raises C# events for the HUD / audio / effects.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class KiteController : MonoBehaviour
    {
        [Header("Loadout")]
        [SerializeField] private KiteClassData kiteClass;
        [SerializeField] private TailData tail;
        [SerializeField] private DorData dor;
        [SerializeField] private CharkhiData charkhi;

        [Header("Config")]
        [SerializeField] private CombatConfig combatConfig;
        [SerializeField] private FlightConfig flightConfig;

        [Header("Scene")]
        [SerializeField] private WindField windField;
        [SerializeField] private KiteInputReader inputReader;
        [Tooltip("Where the line leaves the flyer's hands. Ground is world y = 0.")]
        [SerializeField] private Transform anchor;
        [Tooltip("The kite model that gets moved. Defaults to this transform.")]
        [SerializeField] private Transform kiteVisual;

        [Header("Temporary (until Economy / MatchManager)")]
        [Tooltip("Metres of this dor the player owns. Server inventory replaces this in milestone 11.")]
        [SerializeField, Min(10)] private float dorStockMeters = 600f;
        [SerializeField] private bool autoRespawn = true;
        [SerializeField, Min(0)] private float respawnDelay = 2f;
        [SerializeField] private bool logEvents = true;

        // ---- Events ---------------------------------------------------------
        public event Action<TensionZone> ZoneChanged;
        public event Action<LineAction> ActionChanged;
        public event Action SelfSnapped;
        public event Action Crashed;
        public event Action Respawned;

        // ---- Read-only state for HUD ----------------------------------------
        public KiteSimulation Simulation { get; private set; }
        public KiteClassData KiteClass => kiteClass;
        public TailData Tail => tail;
        public DorData Dor => dor;
        public CharkhiData Charkhi => charkhi;
        public CombatConfig CombatConfig => combatConfig;
        public Transform Anchor => anchor;
        public Transform KiteVisual => kiteVisual;
        public WindSample LastWind { get; private set; }
        public float RespawnCountdown => _respawnTimer;

        private Vector3 _previousPosition;
        private Vector3 _currentPosition;
        private Quaternion _previousRotation;
        private Quaternion _currentRotation;
        private float _respawnTimer;
        private float _groundHeight;

        private void Awake()
        {
            if (kiteVisual == null) kiteVisual = transform;
            if (!ValidateReferences()) { enabled = false; return; }
            BuildSimulation();
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            if (kiteClass == null) { Debug.LogError("[KiteController] Kite Class is not assigned.", this); ok = false; }
            if (dor == null) { Debug.LogError("[KiteController] Dor is not assigned.", this); ok = false; }
            if (charkhi == null) { Debug.LogError("[KiteController] Charkhi is not assigned.", this); ok = false; }
            if (windField == null) { Debug.LogError("[KiteController] WindField is not assigned.", this); ok = false; }
            if (inputReader == null) { Debug.LogError("[KiteController] KiteInputReader is not assigned.", this); ok = false; }
            if (anchor == null) { Debug.LogError("[KiteController] Anchor is not assigned.", this); ok = false; }
            if (combatConfig == null) Debug.LogWarning("[KiteController] No CombatConfig – using spec defaults.", this);
            if (flightConfig == null) Debug.LogWarning("[KiteController] No FlightConfig – using defaults.", this);
            return ok;
        }

        /// <summary>Re-reads every data asset. Use after changing the loadout in the inspector.</summary>
        [ContextMenu("Rebuild Simulation")]
        public void BuildSimulation()
        {
            TailStats tailStats = tail != null ? tail.ToStats() : TailStats.None;
            var loadout = new LoadoutStats(kiteClass.ToStats(), tailStats, dor.ToStats(), charkhi.ToStats());

            TensionSettings tension = combatConfig != null ? combatConfig.ToTensionSettings() : new TensionSettings();
            FlightSettings flight = flightConfig != null ? flightConfig.settings : new FlightSettings();
            bool dheelOnRelease = combatConfig == null || combatConfig.dheelOnPullRelease;

            _groundHeight = 0f;
            float anchorHeight = anchor.position.y - _groundHeight;
            Simulation = new KiteSimulation(loadout, tension, flight, anchorHeight, dorStockMeters, dheelOnRelease);
            Respawn();
        }

        /// <summary>Puts the kite back in the air straight downwind (debug / round reset).</summary>
        public void Respawn()
        {
            if (Simulation == null) return;
            WindSample wind = windField.Sample(Time.fixedTime, anchor.position.y - _groundHeight);
            Simulation.Reset(wind.DirectionDeg);
            LastWind = wind;
            _respawnTimer = 0f;

            ComputePose(out _currentPosition, out _currentRotation);
            _previousPosition = _currentPosition;
            _previousRotation = _currentRotation;
            kiteVisual.SetPositionAndRotation(_currentPosition, _currentRotation);

            if (logEvents) Debug.Log($"[Kite] Launched: {kiteClass.displayName} + {(tail != null ? tail.displayName : "no dum")} + {dor.displayName} + {charkhi.displayName}", this);
            Respawned?.Invoke();
        }

        private void FixedUpdate()
        {
            if (Simulation == null) return;
            float dt = Time.fixedDeltaTime;

            if (Simulation.Status != KiteStatus.Flying)
            {
                if (autoRespawn)
                {
                    _respawnTimer -= dt;
                    if (_respawnTimer <= 0f) Respawn();
                }
                return;
            }

            KiteInput input = inputReader.ReadInput();
            WindSample wind = windField.Sample(Time.fixedTime, Simulation.Flight.Height);
            LastWind = wind;

            KiteStepResult result = Simulation.Step(input, wind, Time.fixedTime, dt);

            _previousPosition = _currentPosition;
            _previousRotation = _currentRotation;
            ComputePose(out _currentPosition, out _currentRotation);

            RaiseEvents(result);
        }

        private void Update()
        {
            if (Simulation == null) return;

            if (inputReader.ResetPressedThisFrame) Respawn();

            // Interpolate between the last two physics ticks so the kite looks smooth at any FPS.
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            kiteVisual.SetPositionAndRotation(
                Vector3.LerpUnclamped(_previousPosition, _currentPosition, alpha),
                Quaternion.SlerpUnclamped(_previousRotation, _currentRotation, alpha));
        }

        private void ComputePose(out Vector3 position, out Quaternion rotation)
        {
            KiteFlightSim flight = Simulation.Flight;
            position = anchor.position + flight.LocalPosition.ToUnity();
            // Kite +Z points away from the flyer (sail faces the flyer), +Y is the nose.
            rotation = Quaternion.LookRotation(flight.Direction.ToUnity(), flight.NoseDirection.ToUnity());
        }

        private void RaiseEvents(in KiteStepResult result)
        {
            if (result.ActionChanged) ActionChanged?.Invoke(Simulation.CurrentAction);

            if (result.ZoneChanged)
            {
                if (logEvents && Simulation.Tension.Zone == TensionZone.Danger)
                    Debug.Log($"[Kite] DANGER zone – tension {Simulation.Tension.Tension:0}", this);
                ZoneChanged?.Invoke(Simulation.Tension.Zone);
            }

            if (result.SelfSnapped)
            {
                if (logEvents) Debug.Log($"[Kite] SELF-SNAP – tension stayed above {dor.displayName} Strength {dor.strength} for > {Simulation.Tension.SnapHoldTime:0.0} s", this);
                _respawnTimer = respawnDelay;
                SelfSnapped?.Invoke();
            }
            else if (result.Crashed)
            {
                if (logEvents) Debug.Log("[Kite] CRASH – kite hit the ground", this);
                _respawnTimer = respawnDelay;
                Crashed?.Invoke();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (anchor == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(anchor.position, 0.3f);
            if (Simulation != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
                Gizmos.DrawWireSphere(anchor.position, Simulation.Spool.LineOut);
            }
        }
    }
}
