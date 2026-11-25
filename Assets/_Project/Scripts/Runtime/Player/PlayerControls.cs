using UnityEngine;
using UnityEngine.InputSystem;

namespace TaintlessGlade
{
    /// <summary>
    /// Entrées du joueur. Les actions et leurs touches par défaut sont définies en code dans <see cref="PlayerInputSet"/>
    /// (les touches choisies par le joueur dans le menu des contrôles y sont rechargées automatiquement).
    /// Les touches clavier sont "physiques" : WASD en QWERTY = ZQSD en AZERTY.
    /// </summary>
    public class PlayerControls : MonoBehaviour
    {
        PlayerInputSet _set;
        InputAction _move;
        InputAction _aimStick;
        InputAction _attack;
        InputAction _dash;
        InputAction _run;
        InputAction _color;
        InputAction _bullet;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 AimStick => _aimStick.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed();
        public bool AttackPressed => _attack.WasPressedThisFrame();
        public bool AttackReleased => _attack.WasReleasedThisFrame();
        public bool DashPressed => _dash.WasPressedThisFrame();
        public bool RunHeld => _run.IsPressed();
        /// <summary>Bascule la couleur du crachat (cyan / arc-en-ciel).</summary>
        public bool ColorTogglePressed => _color.WasPressedThisFrame();
        /// <summary>Passe au style de balle suivant (Arcane d'origine, puis les effets du pack Hovl).</summary>
        public bool BulletCyclePressed => _bullet.WasPressedThisFrame();
        public bool UsingMouseAim { get; private set; } = true;
        public Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        void Awake()
        {
            _set = new PlayerInputSet();
            _move = _set.Move;
            _aimStick = _set.AimStick;
            _attack = _set.Attack;
            _dash = _set.Dash;
            _run = _set.Run;
            _color = _set.ColorToggle;
            _bullet = _set.Bullet;
        }

        void OnEnable() => _set?.Enable();

        void OnDisable() => _set?.Disable();

        void OnDestroy() => _set?.Dispose();

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
