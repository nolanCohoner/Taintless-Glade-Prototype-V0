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
        PlayerAttack _attack;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
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
        }

        void OnFired()
        {
            if (characterAnimator != null) characterAnimator.PlayAttack();
        }
    }
}
