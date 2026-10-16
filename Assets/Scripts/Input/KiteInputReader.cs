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

        private InputAction _steer;
        private InputAction _pull;
        private InputAction _reel;
        private InputAction _reset;
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
        }

        private void OnEnable()
        {
            EnsureActions();
            _steer.Enable();
            _pull.Enable();
            _reel.Enable();
            _reset.Enable();
        }

        private void OnDisable()
        {
            _steer?.Disable();
            _pull?.Disable();
            _reel?.Disable();
            _reset?.Disable();
        }

        private void OnDestroy()
        {
            _steer?.Dispose();
            _pull?.Dispose();
            _reel?.Dispose();
            _reset?.Dispose();
        }

        /// <summary>Current input snapshot. Safe to call from FixedUpdate.</summary>
        public KiteInput ReadInput()
        {
            if (!isActiveAndEnabled) return default;
            Vector2 steer = Vector2.ClampMagnitude(_steer.ReadValue<Vector2>(), 1f);
            return new KiteInput
            {
                SteerX = steer.x,
                SteerY = steer.y,
                PullHeld = _pull.IsPressed(),
                ReelHeld = _reel.IsPressed()
            };
        }

        /// <summary>True on the frame the debug reset was pressed. Call from Update, not FixedUpdate.</summary>
        public bool ResetPressedThisFrame => isActiveAndEnabled && _reset.WasPressedThisFrame();
    }
}
