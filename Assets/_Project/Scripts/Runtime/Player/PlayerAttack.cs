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
        [Tooltip("Éclat de bouche 3D (Free Quick Effects).")]
        public bool use3DMuzzle = true;
        public float muzzleScale = 0.6f;
        public float spawnHeight = 0.5f;
        public float spawnForwardOffset = 0.45f;

        [Header("Dispersion")]
        [Tooltip("Dispersion maximale (degrés, de chaque côté) en tir soutenu. Le premier coup est toujours précis.")]
        public float maxSpread = 2.5f;
        [Tooltip("Dispersion ajoutée à chaque tir consécutif (degrés).")]
        public float spreadPerShot = 0.7f;
        [Tooltip("Sans tirer pendant ce délai (s), la dispersion retombe à zéro.")]
        public float spreadResetTime = 0.45f;

        [Header("Tir chargé (maintenir le bouton)")]
        [Tooltip("Un clic = un tir. Maintenu plus longtemps que ce délai (s), la charge commence ; relâcher lâche le tir chargé.")]
        public float chargeDelay = 0.22f;
        [Tooltip("Durée (s) de la charge, une fois commencée, pour atteindre le maximum.")]
        public float chargeTime = 0.9f;
        [Tooltip("En dessous de cette charge (0..1), relâcher n'envoie rien.")]
        [Range(0f, 1f)] public float minCharge = 0.2f;
        [Tooltip("Dégâts à pleine charge (multiplicateur).")]
        public float chargedDamageMultiplier = 2.5f;
        public float chargedRecoil = 5f;
        [Tooltip("Délai avant le tir suivant après un tir chargé (s).")]
        public float chargedCooldown = 0.45f;

        [Header("Vitesse de la balle")]
        [Tooltip("Multiplicateur de la vitesse issue des stats : le crachat magique est rapide.")]
        public float boltSpeedMultiplier = 1.5f;

        [Header("Recul")]
        [Tooltip("Vitesse de recul du personnage à chaque tir (m/s).")]
        public float recoil = 2f;

        [Header("Couleur du crachat")]
        [Tooltip("Cyan fixe, ou arc-en-ciel qui change de teinte avec le temps. Touche C (ou Y / triangle à la manette) pour basculer en jeu.")]
        public SpitColorMode colorMode = SpitColorMode.Rainbow;

        [Header("Style de balle")]
        [Tooltip("0 = crachat magique (traînée + explosion + rebond, tir chargé) ; 1..n = bulle du crachat + effets du pack Hovl (1 = étincelles roses et explosions). Touche V (ou croix droite à la manette) pour passer au suivant.")]
        public int bulletStyle = 0;

        [Header("Sons (optionnels : rien ne joue tant qu'aucun clip n'est assigné)")]
        public AudioClip[] shotClips;

        /// <summary>Déclenché à chaque tir (animation, son...).</summary>
        public event System.Action Fired;

        /// <summary>Tir maintenu : le prochain coup partira dès que le délai sera écoulé.</summary>
        public bool IsCharging => _charging;
        /// <summary>Niveau de charge du tir maintenu, de 0 à 1.</summary>
        public float Charge01 => _charge;
        public float CooldownRemaining => Mathf.Max(0f, _cooldown);

        PlayerController _controller;
        PlayerControls _controls;
        float _cooldown;
        float _spread;
        float _lastShotTime = -10f;
        float _holdTime, _charge, _buffer;
        bool _holding, _charging, _fullFlashed;
        Transform _anchor;
        Light _chargeLight;
        BulletFx _chargeFx;
        Transform _bodyAnchor;
        PlayerHealth _health;
        readonly System.Collections.Generic.List<BulletFx> _auraFx = new System.Collections.Generic.List<BulletFx>(2);

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _controls = GetComponent<PlayerControls>();
            // La scène garde l'ancienne valeur sérialisée (style 1 = étincelles Hovl) : on repart toujours sur le crachat magique.
            bulletStyle = 0;
        }

        void Update()
        {
            if (_controls.ColorTogglePressed)
                colorMode = colorMode == SpitColorMode.Cyan ? SpitColorMode.Rainbow : SpitColorMode.Cyan;
            SpitColor.Mode = colorMode;

            int styleCount = 1 + (Vfx3D.Available && Vfx3D.Library.hovlBullets != null ? Vfx3D.Library.hovlBullets.Length : 0);
            if (_controls.BulletCyclePressed) bulletStyle++;
            bulletStyle = bulletStyle % styleCount;
            Projectile.BulletStyle = bulletStyle;

            float dt = Time.deltaTime;
            _cooldown -= dt;
            _buffer -= dt;

            if (_controller.IsDashing) CancelCharge();
            else
            {
                // Clic : tir immédiat (mis en mémoire 0,1 s si la cadence n'est pas finie). Maintien : charge, relâcher lâche le tir.
                if (_controls.AttackPressed)
                {
                    _holding = true;
                    _holdTime = 0f;
                    _buffer = 0.1f;
                }
                if (_buffer > 0f && _cooldown <= 0f)
                {
                    _buffer = 0f;
                    Fire(0f);
                }

                if (_holding && _controls.AttackHeld)
                {
                    _holdTime += dt;
                    if (_holdTime > chargeDelay) UpdateCharge();
                }
                if (_holding && !_controls.AttackHeld)
                {
                    if (_charging && _charge >= minCharge) Fire(_charge);
                    CancelCharge();
                }
            }

            if (_charging) FollowMuzzle();
        }

        Vector3 Muzzle() => transform.position + Vector3.up * spawnHeight + _controller.AimDirection * spawnForwardOffset;

        void UpdateCharge()
        {
            _charge = Mathf.Clamp01((_holdTime - chargeDelay) / Mathf.Max(0.05f, chargeTime));
            if (!_charging)
            {
                _charging = true;
                _fullFlashed = false;
                StartChargeFx();
            }

            Color color = SpitColor.Current;
            float size = Mathf.Lerp(0.35f, 1.25f, _charge);
            if (_chargeFx != null) _chargeFx.SetScale(size);
            float aura = Mathf.Lerp(0.45f, 1.5f, _charge) * (_charge >= 1f ? 1f + Mathf.Sin(Time.time * 18f) * 0.08f : 1f);
            foreach (BulletFx fx in _auraFx) if (fx != null) fx.SetScale(aura);
            if (_chargeLight != null)
            {
                _chargeLight.color = color;
                _chargeLight.intensity = Mathf.Lerp(0.5f, 4.5f, _charge) * (1f + Mathf.Sin(Time.time * 40f) * 0.15f * _charge);
                _chargeLight.range = Mathf.Lerp(1.5f, 4f, _charge);
            }
            // Petit tremblement du sprite qui s'amplifie.
            if (_controller.spritePunch != null && Time.frameCount % 3 == 0)
                _controller.spritePunch.Punch(new Vector3(0.015f, -0.015f, 0f) * (0.5f + _charge));

            if (_charge >= 1f && !_fullFlashed)
            {
                _fullFlashed = true;
                Vector3 origin = Muzzle();
                if (Vfx3D.Available && Vfx3D.Library.boltLaunch != null)
                    foreach (GameObject fx in Vfx3D.Library.boltLaunch)
                        Vfx3D.Spawn(fx, origin, Quaternion.LookRotation(_controller.AimDirection), 1.1f, color);
                Vfx3D.Flash(origin + Vector3.up * 0.15f, color, 5f, 4f, 0.15f);
                if (_controller.spritePunch != null) _controller.spritePunch.Punch(new Vector3(0.18f, -0.14f, 0f));
                if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.02f, 0.08f);
            }
        }

        void StartChargeFx()
        {
            if (_anchor == null)
            {
                _anchor = new GameObject("ChargeAnchor").transform;
                _anchor.SetParent(transform, false);
                _chargeLight = _anchor.gameObject.AddComponent<Light>();
                _chargeLight.type = LightType.Point;
                _chargeLight.shadows = LightShadows.None;
            }
            _anchor.position = Muzzle();
            _chargeLight.enabled = true;
            if (_bodyAnchor == null) _bodyAnchor = new GameObject("ChargeBodyAnchor").transform;
            _bodyAnchor.position = transform.position + Vector3.up * 0.5f;
            _bodyAnchor.rotation = Quaternion.identity;
            _auraFx.Clear();
            if (Vfx3D.Available && Vfx3D.Library.chargeAura != null)
                foreach (GameObject prefab in Vfx3D.Library.chargeAura)
                {
                    BulletFx aura = BulletFx.Spawn(prefab, _bodyAnchor.position, Quaternion.identity, 0.45f, _bodyAnchor, SpitColor.Current, 1f, 1f, false, true);
                    if (aura != null) aura.Drop("Smoke", "Trail", "Arrow");
                    _auraFx.Add(aura);
                }
            if (Vfx3D.Available && Vfx3D.Library.boltSparks != null)
                _chargeFx = BulletFx.Spawn(Vfx3D.Library.boltSparks, _anchor.position, Quaternion.LookRotation(_controller.AimDirection), 0.35f, _anchor, SpitColor.Current, 3f, 0.3f, true, true);
        }

        void FollowMuzzle()
        {
            if (_anchor != null) _anchor.SetPositionAndRotation(Muzzle(), Quaternion.LookRotation(_controller.AimDirection));
            if (_bodyAnchor != null) _bodyAnchor.position = transform.position + Vector3.up * 0.5f;
        }

        void CancelCharge()
        {
            _holding = false;
            _charging = false;
            _charge = 0f;
            _holdTime = 0f;
            if (_chargeFx != null)
            {
                _chargeFx.Detach(_chargeFx.transform.position);
                _chargeFx = null;
            }
            foreach (BulletFx fx in _auraFx) if (fx != null) fx.Detach(fx.transform.position);
            _auraFx.Clear();
            if (_chargeLight != null) _chargeLight.enabled = false;
        }

        void OnGUI()
        {
            string name = Vfx3D.Available && Vfx3D.Library.boltCore != null ? "Crachat magique" : "Arcane Tracking Shot";
            if (bulletStyle > 0 && Vfx3D.Available && Vfx3D.Library.hovlBullets != null && bulletStyle <= Vfx3D.Library.hovlBullets.Length)
                name = Vfx3D.Library.hovlBullets[bulletStyle - 1].name;
            GUI.Label(new Rect(12f, 10f, 400f, 24f), "Balle [V] : " + name);
        }

        /// <summary>charge : 0 = tir normal, sinon niveau de charge (0..1) du tir chargé.</summary>
        void Fire(float charge)
        {
            CharacterStats stats = _controller.stats;
            bool charged = charge > 0f;
            _cooldown = charged ? Mathf.Max(chargedCooldown, stats.AttackCooldown) : stats.AttackCooldown;

            // Test : chaque attaque coûte un point de vie.
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_health != null) _health.OnAttack();

            Vector3 aim = _controller.AimDirection;
            Vector3 origin = transform.position + Vector3.up * spawnHeight + aim * spawnForwardOffset;

            // Dispersion : précis au premier tir, puis de plus en plus ouvert en tir soutenu.
            _spread = Time.time - _lastShotTime > spreadResetTime ? 0f : Mathf.Min(maxSpread, _spread + spreadPerShot);
            _lastShotTime = Time.time;
            // Le tir chargé est précis ; la dispersion ne concerne que les tirs rapides.
            Vector3 dir = charged ? aim : Quaternion.Euler(0f, Random.Range(-_spread, _spread), 0f) * aim;

            if (projectilePrefab != null)
            {
                Projectile projectile = Projectile.Spawn(projectilePrefab, origin, Quaternion.LookRotation(dir));
                float amount = stats.attack * (charged ? Mathf.Lerp(1.2f, chargedDamageMultiplier, charge) : 1f);
                var damage = new DamageInfo { amount = amount, direction = dir, source = gameObject };
                float speed = stats.ProjectileSpeed * (Projectile.BulletStyle == 0 ? boltSpeedMultiplier : 1f);
                projectile.Launch(damage, dir * speed, stats.ProjectileRange, transform, charge);
            }

            _controller.Recoil(-aim * (charged ? Mathf.Lerp(recoil, chargedRecoil, charge) : recoil));
            Sfx.Play(shotClips, origin);

            if (use3DMuzzle && Vfx3D.Available && Vfx3D.Library.muzzle != null)
            {
                Color spit = SpitColor.Current;
                Vfx3D.Spawn(Vfx3D.Library.muzzle, origin, Quaternion.LookRotation(aim), muzzleScale, spit);
                Vfx3D.Flash(origin + Vector3.up * 0.15f, spit, 2.5f, 3f, 0.1f);
            }
            else if (muzzleVfxPrefab != null)
                Instantiate(muzzleVfxPrefab, origin, Quaternion.LookRotation(aim));

            if (Vfx3D.Available && Vfx3D.Library.boltLaunch != null)
            {
                Color spit = SpitColor.Current;
                foreach (GameObject fx in Vfx3D.Library.boltLaunch)
                    Vfx3D.Spawn(fx, origin, Quaternion.LookRotation(aim), charged ? 1f : 0.5f, spit);
            }
            if (charged && CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.05f * charge, 0.1f);

            if (_controller.spritePunch != null)
                _controller.spritePunch.Punch(charged ? new Vector3(0.2f, -0.15f, 0f) : new Vector3(0.08f, -0.06f, 0f));

            Fired?.Invoke();
        }
    }
}
