using Kitefall.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitefall.Input
{
    /// <summary>
    /// Reads player input with the new Input System and turns it into a <see cref="KiteInput"/>.
    ///
    /// Bindings (created in code so there is no .inputactions asset to break):
    ///   Steer : WASD / arrow keys / gamepad left stick / on-screen joystick (virtual left stick)
    ///   PULL  : Space / gamepad right trigger / on-screen PULL (virtual right trigger)
    ///   REEL  : Left Shift / gamepad left trigger / on-screen REEL (virtual left trigger)
    ///   DIVE  : Q or Left Ctrl / gamepad X (west) / on-screen DIVE / swipe down
    ///   Reset : Backspace / gamepad Select (debug, milestone 1 only)
    /// The on-screen controls (OnScreenStick / OnScreenButton) feed a virtual gamepad,
    /// so touch, keyboard and gamepad all go through the same actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KiteInputReader : MonoBehaviour
    {
        public const string SteerStickPath = "<Gamepad>/leftStick";
        public const string PullButtonPath = "<Gamepad>/rightTrigger";
        public const string ReelButtonPath = "<Gamepad>/leftTrigger";
        public const string ResetButtonPath = "<Gamepad>/select";
        public const string DiveButtonPath = "<Gamepad>/buttonWest";

        /// <summary>Steering multiplier (Settings ▸ Controls ▸ Sensitivity).</summary>
        public float Sensitivity { get; set; } = 1f;
        /// <summary>Set by the dive swipe gesture; consumed by the next ReadInput.</summary>
        public bool GestureDive { get; set; }

        private InputAction _steer;
        private InputAction _pull;
        private InputAction _reel;
        private InputAction _reset;
        private InputAction _dive;
        private bool _initialised;

        private void Awake() => EnsureActions();

        private void EnsureActions()
        {
            if (_initialised) return;
            _initialised = true;

            _steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Vector2");
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _steer.AddBinding(SteerStickPath);

            _pull = new InputAction("Pull", InputActionType.Button);
            _pull.AddBinding("<Keyboard>/space");
            _pull.AddBinding(PullButtonPath);

            _reel = new InputAction("Reel", InputActionType.Button);
            _reel.AddBinding("<Keyboard>/leftShift");
            _reel.AddBinding(ReelButtonPath);

            _reset = new InputAction("Reset", InputActionType.Button);
            _reset.AddBinding("<Keyboard>/backspace");
            _reset.AddBinding(ResetButtonPath);

            _dive = new InputAction("Dive", InputActionType.Button);
            _dive.AddBinding("<Keyboard>/q");
            _dive.AddBinding("<Keyboard>/leftCtrl");
            _dive.AddBinding(DiveButtonPath);
        }

        private void OnEnable()
        {
            EnsureActions();
            _steer.Enable();
            _pull.Enable();
            _reel.Enable();
            _reset.Enable();
            _dive.Enable();
        }

        private void OnDisable()
        {
            _steer?.Disable();
            _pull?.Disable();
            _reel?.Disable();
            _reset?.Disable();
            _dive?.Disable();
        }

        private void OnDestroy()
        {
            _steer?.Dispose();
            _pull?.Dispose();
            _reel?.Dispose();
            _reset?.Dispose();
            _dive?.Dispose();
        }

        /// <summary>Current input snapshot. Safe to call from FixedUpdate.</summary>
        public KiteInput ReadInput()
        {
            if (!isActiveAndEnabled) return default;
            Vector2 steer = Vector2.ClampMagnitude(_steer.ReadValue<Vector2>() * Mathf.Max(0.1f, Sensitivity), 1f);
            bool dive = _dive.IsPressed() || GestureDive;
            GestureDive = false;
            return new KiteInput
            {
                SteerX = steer.x,
                SteerY = steer.y,
                PullHeld = _pull.IsPressed(),
                ReelHeld = _reel.IsPressed(),
                DiveHeld = dive
            };
        }

        /// <summary>True on the frame the debug reset was pressed. Call from Update, not FixedUpdate.</summary>
        public bool ResetPressedThisFrame => isActiveAndEnabled && _reset.WasPressedThisFrame();
    }
}
