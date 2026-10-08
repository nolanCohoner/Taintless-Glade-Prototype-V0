using UnityEngine;
using UnityEngine.InputSystem;

namespace TaintlessGlade
{
    /// <summary>
    /// Entrées du joueur, définies en code pour rester autonome.
    /// Les touches clavier sont "physiques" : WASD en QWERTY = ZQSD en AZERTY.
    /// </summary>
    public class PlayerControls : MonoBehaviour
    {
        InputAction _move;
        InputAction _aimStick;
        InputAction _attack;
        InputAction _dash;
        InputAction _run;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 AimStick => _aimStick.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed();
        public bool DashPressed => _dash.WasPressedThisFrame();
        public bool RunHeld => _run.IsPressed();
        public bool UsingMouseAim { get; private set; } = true;
        public Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _aimStick = new InputAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick");

            _attack = new InputAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/rightTrigger");

            _dash = new InputAction("Dash", InputActionType.Button);
            _dash.AddBinding("<Keyboard>/space");
            _dash.AddBinding("<Mouse>/rightButton");
            _dash.AddBinding("<Gamepad>/buttonSouth");

            _run = new InputAction("Run", InputActionType.Button);
            _run.AddBinding("<Keyboard>/leftShift");
            _run.AddBinding("<Gamepad>/leftStickPress");
            _run.AddBinding("<Gamepad>/leftShoulder");
        }

        void OnEnable()
        {
            _move.Enable();
            _aimStick.Enable();
            _attack.Enable();
            _dash.Enable();
            _run.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _aimStick.Disable();
            _attack.Disable();
            _dash.Disable();
            _run.Disable();
        }

        void OnDestroy()
        {
            _move.Dispose();
            _aimStick.Dispose();
            _attack.Dispose();
            _dash.Dispose();
            _run.Dispose();
        }

        void Update()
        {
            // Bascule automatiquement entre visée souris et visée stick selon le dernier utilisé.
            if (AimStick.sqrMagnitude > 0.1f)
                UsingMouseAim = false;
            else if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f)
                UsingMouseAim = true;
        }
    }
}
