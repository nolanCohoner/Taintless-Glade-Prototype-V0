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
        [Tooltip("Éclat 2D image par image au museau (remplace le prefab de particules).")]
        public bool use2DMuzzle = true;
        public float spawnHeight = 0.5f;
        public float spawnForwardOffset = 0.45f;

        /// <summary>Déclenché à chaque tir (animation, son...).</summary>
        public event System.Action Fired;

        /// <summary>Tir maintenu : le prochain coup partira dès que le délai sera écoulé.</summary>
        public bool IsCharging => _controls != null && _controls.AttackHeld && !_controller.IsDashing;
        public float CooldownRemaining => Mathf.Max(0f, _cooldown);

        PlayerController _controller;
        PlayerControls _controls;
        float _cooldown;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _controls = GetComponent<PlayerControls>();
            // Rayon arc-en-ciel (clic maintenu 2,5 s) : ajouté automatiquement, pas besoin de modifier la scène.
            Beam = GetComponent<RainbowBeam>();
            if (Beam == null) Beam = gameObject.AddComponent<RainbowBeam>();
        }

        public RainbowBeam Beam { get; private set; }

        void Update()
        {
            _cooldown -= Time.deltaTime;
            bool beamActive = Beam != null && Beam.IsFiring;
            if (_controls.AttackHeld && _cooldown <= 0f && !_controller.IsDashing && !beamActive)
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

            if (use2DMuzzle)
                Fx2D.SpawnMuzzle(origin, Fx2D.SpitColor, 0.55f);
            else if (muzzleVfxPrefab != null)
                Instantiate(muzzleVfxPrefab, origin, Quaternion.LookRotation(dir));

            if (_controller.spritePunch != null)
                _controller.spritePunch.Punch(new Vector3(0.08f, -0.06f, 0f));

            Fired?.Invoke();
        }
    }
}
