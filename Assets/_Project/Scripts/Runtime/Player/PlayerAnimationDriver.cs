using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Transmet l'état du joueur (vitesse, visée, dash, tirs) au CharacterAnimator.
    /// Un ennemi aura son propre "driver" branché sur son IA, avec le même animateur.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        public CharacterAnimator characterAnimator;

        PlayerController _controller;
        PlayerControls _controls;
        PlayerAttack _attack;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _controls = GetComponent<PlayerControls>();
            _attack = GetComponent<PlayerAttack>();
        }

        void OnEnable()
        {
            if (_attack != null) _attack.Fired += OnFired;
        }

        void OnDisable()
        {
            if (_attack != null) _attack.Fired -= OnFired;
        }

        void Update()
        {
            if (characterAnimator == null) return;
            characterAnimator.Velocity = _controller.Velocity;
            characterAnimator.AimDirection = _controller.AimDirection;
            characterAnimator.WalkSpeed = _controller.WalkSpeed;
            characterAnimator.RunSpeed = _controller.RunSpeed;
            characterAnimator.IsDashing = _controller.IsDashing;

            // Anticipation : tir maintenu et prochain coup imminent (le tout premier coup part sans délai).
            float windupTime = characterAnimator.profile != null ? Mathf.Max(characterAnimator.profile.windupTime, 0.01f) : 0.1f;
            bool charging = _attack != null && _attack.IsCharging && _attack.CooldownRemaining > 0f;
            characterAnimator.AttackWindup = charging ? 1f - Mathf.Clamp01(_attack.CooldownRemaining / windupTime) : 0f;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Direction de visée telle qu'on la voit à l'écran (la caméra est inclinée : la projection "monde" la déformerait).
            Vector3 origin = transform.position + Vector3.up * _controller.aimPlaneHeight;
            Vector2 aimScreen = (Vector2)(cam.WorldToScreenPoint(origin + _controller.AimDirection) - cam.WorldToScreenPoint(origin));
            characterAnimator.AimScreen = aimScreen;

            // Regard : direction exacte de la souris depuis la tête (à la manette : direction de visée).
            Transform head = characterAnimator.rig != null ? characterAnimator.rig.head : null;
            if (_controls != null && _controls.UsingMouseAim && head != null)
                characterAnimator.LookPixels = _controls.PointerPosition - (Vector2)cam.WorldToScreenPoint(head.position);
            else
                characterAnimator.LookPixels = aimScreen.normalized * 200f;
        }

        void OnFired()
        {
            if (characterAnimator != null) characterAnimator.PlayAttack();
        }
    }
}
