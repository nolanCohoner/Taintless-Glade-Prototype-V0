using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Attaque de base à distance (Crachat de l'Axolotl) : tir maintenu, cadence issue des stats.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerControls))]
    public class PlayerAttack : MonoBehaviour
    {
        public Projectile projectilePrefab;
        public GameObject muzzleVfxPrefab;
        public float spawnHeight = 0.5f;
        public float spawnForwardOffset = 0.45f;

        /// <summary>Déclenché à chaque tir (animation, son...).</summary>
        public event System.Action Fired;

        PlayerController _controller;
        PlayerControls _controls;
        float _cooldown;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _controls = GetComponent<PlayerControls>();
        }

        void Update()
        {
            _cooldown -= Time.deltaTime;
            if (_controls.AttackHeld && _cooldown <= 0f && !_controller.IsDashing)
                Fire();
        }

        void Fire()
        {
            CharacterStats stats = _controller.stats;
            _cooldown = stats.AttackCooldown;

            Vector3 dir = _controller.AimDirection;
            Vector3 origin = transform.position + Vector3.up * spawnHeight + dir * spawnForwardOffset;

            if (projectilePrefab != null)
            {
                Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
                var damage = new DamageInfo { amount = stats.attack, direction = dir, source = gameObject };
                projectile.Launch(damage, dir * stats.ProjectileSpeed, stats.ProjectileRange, transform);
            }

            if (muzzleVfxPrefab != null)
                Instantiate(muzzleVfxPrefab, origin, Quaternion.LookRotation(dir));

            if (_controller.spritePunch != null)
                _controller.spritePunch.Punch(new Vector3(0.08f, -0.06f, 0f));

            Fired?.Invoke();
        }
    }
}
