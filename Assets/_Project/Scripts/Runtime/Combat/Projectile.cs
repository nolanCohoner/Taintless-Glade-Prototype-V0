using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Projectile déplacé manuellement avec un SphereCast par frame (pas de Rigidbody, pas de tunneling).
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float radius = 0.15f;
        public LayerMask hitMask = ~0;
        public GameObject impactVfxPrefab;

        [Header("Rendu 3D (sort Arcane Tracking Shot du pack Full Opaque Spell, prioritaire sur les particules du prefab si la bibliothèque existe)")]
        public bool use3DVisuals = true;
        [Tooltip("Échelle du sort (1 = taille d'origine du pack : ses traînées convergent sur la balle).")]
        public float spellScale = 1f;
        [Tooltip("Impact sur un décor (mur, sol).")]
        public float impact3DScale = 0.8f;
        [Tooltip("Impact sur une cible vivante : plus gros et plus lumineux.")]
        public float enemyImpact3DScale = 1.15f;

        [Header("Balles du pack Hovl (style choisi par PlayerAttack)")]
        [Tooltip("Échelle de l'effet qui suit la balle.")]
        public float packBulletScale = 1f;
        [Tooltip("Agrandit les particules de la balle sans agrandir l'effet (les particules du pack sont minuscules : 0,04 m).")]
        public float packParticleSize = 3f;
        [Tooltip("Vitesse des particules de la balle (1 = d'origine). Plus bas = traînée plus serrée derrière la balle.")]
        public float packParticleSpeed = 0.3f;
        [Tooltip("Échelle des explosions sur un décor.")]
        public float packImpactScale = 1f;
        [Tooltip("Échelle des explosions sur une cible vivante.")]
        public float packEnemyImpactScale = 1.4f;
        [Tooltip("Applique la couleur du crachat (arc-en-ciel / cyan) aux effets du pack au lieu de leurs couleurs d'origine.")]
        public bool tintPackEffects = true;

        [Header("Crachat magique (style 0)")]
        [Tooltip("La balle ne s'arrête qu'au contact : plus de portée maximale.")]
        public bool infiniteRange = true;
        [Tooltip("Sécurité : la balle explose si elle vole aussi longtemps (s).")]
        public float maxLifetime = 6f;
        [Tooltip("Rebonds sur un mur avant d'exploser. Un ennemi arrête toujours la balle.")]
        public int maxWallBounces = 1;
        [Tooltip("Rayon de collision de la balle (la tête est grosse : un rayon plus large colle à ce qu'on voit).")]
        public float boltRadius = 0.28f;
        [Tooltip("Plafond du rayon (tir chargé) : au-dessus de ~0,45 la balle, tirée à 0,5 m du sol, toucherait le sol et exploserait sur place.")]
        public float boltMaxRadius = 0.42f;
        [Tooltip("Échelle de la boule lumineuse au cœur de la balle (utilisée seulement sans la bulle de l'arme).")]
        public float boltOrbScale = 0.5f;
        [Tooltip("Taille de la bulle de l'arme au cœur de la balle (1 = environ 0,54 m de diamètre).")]
        public float boltBubbleScale = 1.1f;
        [Tooltip("Échelle de la tête / traînée de la balle.")]
        public float boltScale = 1.4f;
        [Tooltip("Échelle du halo d'étoiles autour de la balle.")]
        public float boltAuraScale = 0.5f;
        public float boltExplosionScale = 1.5f;
        public float boltEnemyExplosionScale = 2f;
        public float boltBounceScale = 0.8f;
        [Tooltip("Échelle de l'explosion supplémentaire du tir chargé (Red energy explosion, vfx_Explosion_02).")]
        public float chargedBurstScale = 0.7f;
        [Tooltip("Lumière arc-en-ciel qui accompagne la balle.")]
        public float glowIntensity = 2.5f;
        public float glowRange = 5f;
        [Tooltip("Tir chargé à pleine charge : rayon de collision, échelle visuelle et explosion (multiplicateurs).")]
        public float chargedRadiusMultiplier = 2.2f;
        public float chargedVisualMultiplier = 2.6f;
        public float chargedExplosionMultiplier = 2.8f;

        [Header("Mécanique du crachat magique")]
        [Tooltip("Poussée infligée à la cible (tir normal -> tir chargé à pleine charge).")]
        public float knockback = 0.6f;
        public float chargedKnockback = 4f;
        [Tooltip("Le tir chargé traverse des ennemis avant d'exploser au suivant, à partir de cette charge (0..1).")]
        [Range(0f, 1f)] public float pierceMinPower = 0.5f;
        public int piercedEnemies = 1;
        [Tooltip("Tir chargé : rayon de l'explosion qui blesse aussi les ennemis proches (à pleine charge), et part des dégâts qu'ils reçoivent.")]
        public float chargedAreaRadius = 2.4f;
        [Range(0f, 1f)] public float areaDamageFactor = 0.5f;
        [Tooltip("Après un rebond sur un mur, la balle se courbe vers l'ennemi le plus proche devant elle.")]
        public bool homingAfterBounce = true;
        public float homingRange = 9f;
        [Tooltip("Demi-angle (degrés) dans lequel un ennemi peut être choisi, par rapport à la direction du rebond.")]
        public float homingCone = 70f;
        [Tooltip("Vitesse de virage (degrés par seconde).")]
        public float homingTurnRate = 240f;

        [Header("Comportement")]
        [Tooltip("Vitesse au départ, en fraction de la vitesse de croisière : la balle démarre nerveuse puis accélère.")]
        [Range(0.2f, 1f)] public float startSpeedFactor = 0.6f;
        public float accelerationTime = 0.12f;

        [Header("Impact de jeu (feel)")]
        public bool hitStop = true;
        public float hitStopDuration = 0.04f;
        public float hitShakeStrength = 0.035f;
        public float hitShakeDuration = 0.07f;

        [Header("Sons (optionnels : rien ne joue tant qu'aucun clip n'est assigné)")]
        public AudioClip[] enemyHitClips;
        public AudioClip[] wallHitClips;

        DamageInfo _damage;
        Vector3 _velocity;
        float _remainingRange;
        Transform _owner;
        bool _launched;
        bool _use3D;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        Vector3 _dir;
        float _baseSpeed, _age;
        SpellShotFx _spellFx;
        Color _tint;
        bool _visualReady;
        Projectile _poolKey;
        BulletFx _bulletFx;
        bool _packStyle;
        bool _bolt;
        float _power, _lifetime, _baseRadius;
        int _bounces;
        readonly List<BulletFx> _boltFx = new List<BulletFx>(4);
        Light _glow;
        BoltBubble _bubble;
        int _pierced;
        Collider _homing;
        readonly List<IDamageable> _hitList = new List<IDamageable>(4);
        static readonly Collider[] _overlap = new Collider[32];
        static readonly List<IDamageable> _areaDone = new List<IDamageable>(8);

        static readonly Dictionary<Projectile, Stack<Projectile>> _pool = new Dictionary<Projectile, Stack<Projectile>>();

        /// <summary>0 = sort Arcane Tracking Shot d'origine ; 1..n = Vfx3DLibrary.hovlBullets[n - 1].</summary>
        public static int BulletStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pool.Clear();
            BulletStyle = 0;
        }

        static bool BoltAvailable => BulletStyle == 0 && Vfx3D.Available && Vfx3D.Library.boltCore != null;

        static GameObject PackBullet
        {
            get
            {
                if (BulletStyle <= 0 || !Vfx3D.Available) return null;
                GameObject[] list = Vfx3D.Library.hovlBullets;
                return list != null && BulletStyle <= list.Length ? list[BulletStyle - 1] : null;
            }
        }

        /// <summary>Récupère un projectile recyclé (ou en crée un) : évite un Instantiate/Destroy par tir.</summary>
        public static Projectile Spawn(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            if (!_pool.TryGetValue(prefab, out Stack<Projectile> stack)) _pool[prefab] = stack = new Stack<Projectile>();
            Projectile p = null;
            while (stack.Count > 0 && p == null) p = stack.Pop();
            if (p == null)
            {
                p = Instantiate(prefab, position, rotation);
                p._poolKey = prefab;
            }
            else
            {
                p.transform.SetPositionAndRotation(position, rotation);
                p.gameObject.SetActive(true);
            }
            return p;
        }

        void Release()
        {
            _launched = false;
            DetachSpell(transform.position);
            if (_poolKey != null && _pool.TryGetValue(_poolKey, out Stack<Projectile> stack))
            {
                gameObject.SetActive(false);
                stack.Push(this);
            }
            else Destroy(gameObject);
        }

        /// <summary>power : 0 = tir normal, 0..1 = tir chargé (gonfle la balle, son rayon et son explosion).</summary>
        public void Launch(DamageInfo damage, Vector3 velocity, float range, Transform owner, float power = 0f)
        {
            _damage = damage;
            _baseSpeed = velocity.magnitude;
            _dir = _baseSpeed > 0.0001f ? velocity / _baseSpeed : transform.forward;
            _velocity = velocity;
            _bolt = use3DVisuals && BoltAvailable;
            _remainingRange = _bolt && infiniteRange ? float.PositiveInfinity : range;
            _power = Mathf.Clamp01(power);
            _lifetime = 0f;
            _bounces = 0;
            _pierced = 0;
            _homing = null;
            _hitList.Clear();
            if (_baseRadius <= 0f) _baseRadius = radius;
            radius = (_bolt ? boltRadius : _baseRadius) * Mathf.Lerp(1f, chargedRadiusMultiplier, _power);
            if (_bolt) radius = Mathf.Min(radius, boltMaxRadius);
            _age = 0f;
            _owner = owner;
            _launched = true;
            _packStyle = use3DVisuals && PackBullet != null;
            _use3D = _bolt || _packStyle || (use3DVisuals && Vfx3D.Available && Vfx3D.Library.spellShot != null);
            if (_use3D) Setup3D(velocity);
        }

        /// <summary>Cache les particules d'origine du prefab et lance le sort du pack : il part d'ici, son champ de force suit la balle.</summary>
        void Setup3D(Vector3 velocity)
        {
            if (!_visualReady)
            {
                _visualReady = true;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Quaternion rot = velocity.sqrMagnitude > 0f ? Quaternion.LookRotation(velocity) : Quaternion.identity;
            _tint = SpitColor.Current;
            if (_bolt)
            {
                Vfx3DLibrary lib = Vfx3D.Library;
                float visual = boltScale * Mathf.Lerp(1f, chargedVisualMultiplier, _power);
                _boltFx.Clear();
                _boltFx.Add(BulletFx.Spawn(lib.boltCore, transform.position, rot, visual, transform, _tint, 1f, 1f, false, true));
                if (lib.boltSparks != null)
                    _boltFx.Add(BulletFx.Spawn(lib.boltSparks, transform.position, rot, visual, transform, _tint, packParticleSize, packParticleSpeed, true, true));
                if (lib.boltSparksExtra != null)
                    _boltFx.Add(BulletFx.Spawn(lib.boltSparksExtra, transform.position, rot, visual, transform, _tint, packParticleSize * 0.8f, packParticleSpeed, true, true));
                if (_bubble == null && lib.boltBubble != null) _bubble = BoltBubble.Create(transform, lib.boltBubble, lib.boltRing);
                if (_bubble != null)
                {
                    // La bulle de l'arme remplace la tête carrée de Free Quick Effects et la boule Hovl.
                    _bubble.Show(boltBubbleScale * Mathf.Lerp(1f, chargedVisualMultiplier, _power), _power);
                    if (_boltFx[0] != null) _boltFx[0].Drop("Head");
                }
                else if (lib.boltOrb != null)
                {
                    BulletFx orb = BulletFx.Spawn(lib.boltOrb, transform.position, rot, boltOrbScale * Mathf.Lerp(1f, chargedVisualMultiplier, _power), transform, _tint, 1f, 1f, false, true);
                    if (orb != null) orb.KeepOnly("Glow");
                    _boltFx.Add(orb);
                }
                if (lib.boltAura != null)
                {
                    BulletFx aura = BulletFx.Spawn(lib.boltAura, transform.position, rot, boltAuraScale * Mathf.Lerp(1f, chargedVisualMultiplier, _power), transform, _tint, 1f, 1f, false, true);
                    if (aura != null) aura.Drop("Smoke");
                    _boltFx.Add(aura);
                }

                if (_glow == null)
                {
                    _glow = new GameObject("BoltGlow").AddComponent<Light>();
                    _glow.transform.SetParent(transform, false);
                    _glow.transform.localPosition = Vector3.up * 0.2f;
                    _glow.type = LightType.Point;
                    _glow.shadows = LightShadows.None;
                }
                _glow.enabled = true;
                _glow.intensity = glowIntensity * (1f + _power * 1.5f);
                _glow.range = glowRange * (1f + _power);
            }
            else if (_packStyle)
                _bulletFx = BulletFx.Spawn(PackBullet, transform.position, rot, packBulletScale, transform, tintPackEffects ? _tint : (Color?)null, packParticleSize, packParticleSpeed);
            else
                _spellFx = SpellShotFx.Spawn(Vfx3D.Library.spellShot, transform.position, rot, spellScale, transform);
        }

        /// <summary>La balle est arrivée : le sort finit ses traînées sur le point d'impact puis se range.</summary>
        void DetachSpell(Vector3 point)
        {
            foreach (BulletFx fx in _boltFx) if (fx != null) fx.Detach(point);
            _boltFx.Clear();
            if (_glow != null) _glow.enabled = false;
            if (_bubble != null) _bubble.Hide();
            if (_bulletFx != null)
            {
                _bulletFx.Detach(point);
                _bulletFx = null;
            }
            if (_spellFx == null) return;
            _spellFx.Detach(point);
            _spellFx = null;
        }

        /// <summary>Explosions du pack à l'impact ; en fin de portée, seule la dernière (dissipation) est jouée, en plus petit.</summary>
        void SpawnPackImpact(Vector3 point, Vector3 back, bool onTarget, bool expired)
        {
            GameObject[] list = Vfx3D.Library.hovlImpacts;
            if (list == null || list.Length == 0) return;
            Color? tint = tintPackEffects ? _tint : (Color?)null;
            Quaternion rot = Quaternion.LookRotation(back);
            float scale = onTarget ? packEnemyImpactScale : packImpactScale;
            if (expired)
            {
                Vfx3D.Spawn(list[list.Length - 1], point, rot, packImpactScale * 0.5f, tint);
                return;
            }
            foreach (GameObject fx in list) Vfx3D.Spawn(fx, point, rot, scale, tint);
        }

        void Update()
        {
            if (!_launched) return;

            if (_baseSpeed < 0.0001f) { Impact(transform.position, false, false); return; }

            if (_glow != null && _glow.enabled) _glow.color = SpitColor.Current;
            _lifetime += Time.deltaTime;
            if (_bolt && _lifetime > maxLifetime) { Impact(transform.position, false, true); return; }

            // Démarrage nerveux : 60 % de la vitesse puis montée douce jusqu'à la vitesse de croisière.
            _age += Time.deltaTime;
            float speed = _baseSpeed * Mathf.Lerp(startSpeedFactor, 1f, Mathf.SmoothStep(0f, 1f, _age / Mathf.Max(0.01f, accelerationTime)));
            if (_homing != null) SteerToTarget();
            Vector3 dir = _dir;
            _velocity = dir * speed;

            float stepDistance = Mathf.Min(speed * Time.deltaTime, _remainingRange);

            if (TryFindHit(dir, stepDistance, out RaycastHit hit))
            {
                Vector3 point = hit.distance > 0f ? transform.position + dir * hit.distance : transform.position;
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    DamageInfo info = _damage;
                    info.point = point;
                    info.direction = dir;
                    info.knockback = Mathf.Lerp(knockback, chargedKnockback, _power);
                    damageable.TakeDamage(info);
                    _hitList.Add(damageable);
                    if (_bolt && _power >= pierceMinPower && _pierced < piercedEnemies)
                    {
                        Pierce(point, dir);
                        return;
                    }
                }
                if (damageable == null && _bounces < maxWallBounces && TryBounce(hit, point)) return;
                Impact(point, damageable != null, false);
                return;
            }

            transform.position += dir * stepDistance;
            _remainingRange -= stepDistance;
            if (_remainingRange <= 0f)
                Impact(transform.position, false, true);
        }

        /// <summary>Tir chargé : traverse un ennemi en laissant une petite explosion, puis continue tout droit.</summary>
        void Pierce(Vector3 point, Vector3 dir)
        {
            _pierced++;
            transform.position = point;
            if (_bubble != null) _bubble.Pop();
            if (Vfx3D.Available)
            {
                Quaternion rot = Quaternion.LookRotation(-dir);
                foreach (GameObject fx in Vfx3D.Library.boltBounce)
                    Vfx3D.Spawn(fx, point, rot, boltBounceScale * 2.4f, _tint);
                Vfx3D.Flash(point + Vector3.up * 0.2f, _tint, 4f, 4f, 0.15f);
            }
            Sfx.Play(enemyHitClips, point);
            if (hitStop) HitStop.Hit(hitStopDuration);
            if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(hitShakeStrength * 1.5f, hitShakeDuration);
        }

        /// <summary>Tir chargé : blesse aussi les ennemis autour de l'explosion et les repousse.</summary>
        void ApplyArea(Vector3 point)
        {
            float r = chargedAreaRadius * _power;
            if (r < 0.5f) return;
            int count = Physics.OverlapSphereNonAlloc(point, r, _overlap, hitMask, QueryTriggerInteraction.Ignore);
            _areaDone.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlap[i];
                if (_owner != null && c.transform.IsChildOf(_owner)) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || _hitList.Contains(d) || _areaDone.Contains(d)) continue;
                _areaDone.Add(d);

                Vector3 away = c.bounds.center - point;
                away.y = 0f;
                DamageInfo info = _damage;
                info.amount *= areaDamageFactor;
                info.point = c.ClosestPoint(point);
                info.direction = away.sqrMagnitude > 0.001f ? away.normalized : _dir;
                info.knockback = chargedKnockback * 0.7f * _power;
                d.TakeDamage(info);
            }
        }

        /// <summary>Choisit l'ennemi visible le plus proche de la direction du rebond (dans un cône), sans mur entre les deux.</summary>
        Collider FindHomingTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, homingRange, _overlap, hitMask, QueryTriggerInteraction.Ignore);
            Collider best = null;
            float bestAngle = homingCone;
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlap[i];
                if (_owner != null && c.transform.IsChildOf(_owner)) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null) continue;

                Vector3 toTarget = c.bounds.center - transform.position;
                toTarget.y = 0f;
                float angle = Vector3.Angle(_dir, toTarget);
                if (angle >= bestAngle) continue;

                // Ligne de vue : un mur (ou un autre ennemi) entre la balle et la cible l'écarte.
                Vector3 origin = transform.position;
                Vector3 aim = c.bounds.center;
                aim.y = origin.y;
                Vector3 line = aim - origin;
                if (Physics.Raycast(origin, line.normalized, out RaycastHit block, line.magnitude, hitMask, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<IDamageable>() != d) continue;

                best = c;
                bestAngle = angle;
            }
            return best;
        }

        void SteerToTarget()
        {
            Vector3 to = _homing.bounds.center - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            _dir = Vector3.RotateTowards(_dir, to.normalized, homingTurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            _dir.y = 0f;
            _dir.Normalize();
            transform.rotation = Quaternion.LookRotation(_dir);
        }

        /// <summary>Rebond logique sur un mur : la direction est réfléchie sur la normale du mur (à plat, la balle reste à hauteur constante).</summary>
        bool TryBounce(RaycastHit hit, Vector3 point)
        {
            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.01f) return false;
            normal.Normalize();

            Vector3 reflected = Vector3.Reflect(_dir, normal);
            reflected.y = 0f;
            if (reflected.sqrMagnitude < 0.01f) return false;
            _dir = reflected.normalized;
            _bounces++;
            _age = accelerationTime; // pas de ré-accélération : le rebond garde sa vitesse.
            transform.position = point + normal * 0.03f;
            transform.rotation = Quaternion.LookRotation(_dir);

            if (_bubble != null) _bubble.Pop();
            if (_bolt && homingAfterBounce) _homing = FindHomingTarget();

            if (_bolt && Vfx3D.Available)
            {
                Quaternion rot = Quaternion.LookRotation(normal);
                foreach (GameObject fx in Vfx3D.Library.boltBounce)
                    Vfx3D.Spawn(fx, point, rot, boltBounceScale * Mathf.Lerp(1f, chargedExplosionMultiplier, _power), _tint);
                Vfx3D.Flash(point + Vector3.up * 0.2f, _tint, 2f, 3f, 0.1f);
            }
            Sfx.Play(wallHitClips, point);
            return true;
        }

        /// <summary>Explosion finale du crachat magique : flash + explosion + étoiles + étincelles, teintés cyan / arc-en-ciel.</summary>
        void SpawnBoltImpact(Vector3 point, Vector3 back, bool onTarget, bool expired)
        {
            Vfx3DLibrary lib = Vfx3D.Library;
            Quaternion rot = Quaternion.LookRotation(back);
            float charged = Mathf.Lerp(1f, chargedExplosionMultiplier, _power);
            if (expired)
            {
                if (lib.boltExplosion.Length > 0) Vfx3D.Spawn(lib.boltExplosion[lib.boltExplosion.Length - 1], point, rot, boltExplosionScale * 0.5f, _tint);
                return;
            }
            float scale = (onTarget ? boltEnemyExplosionScale : boltExplosionScale) * charged;
            foreach (GameObject fx in lib.boltExplosion) Vfx3D.Spawn(fx, point, rot, scale, _tint);
            if (lib.boltFlash != null) Vfx3D.Spawn(lib.boltFlash, point, rot, (onTarget ? enemyImpact3DScale : impact3DScale) * charged, _tint);
            // Tir chargé : explosion supplémentaire, proportionnelle à la charge, sur ennemi comme sur mur.
            if (_power > 0f && lib.boltChargedExplosion != null)
                foreach (GameObject fx in lib.boltChargedExplosion) Vfx3D.Spawn(fx, point, rot, chargedBurstScale * (0.5f + _power * 1.3f) * (onTarget ? 1.2f : 1f), _tint);
        }

        bool TryFindHit(Vector3 dir, float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, dir, _hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (_owner != null && h.collider.transform.IsChildOf(_owner)) continue;
                // Un ennemi déjà traversé ne bloque plus la balle.
                if (_hitList.Count > 0)
                {
                    var d = h.collider.GetComponentInParent<IDamageable>();
                    if (d != null && _hitList.Contains(d)) continue;
                }
                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        void Impact(Vector3 point, bool onTarget, bool expired)
        {
            Vector3 back = -_dir;
            DetachSpell(point);
            if (_bolt)
            {
                SpawnBoltImpact(point, back, onTarget, expired);
                if (!expired) ApplyArea(point);
                if (!expired) Vfx3D.Flash(point + Vector3.up * 0.2f, _tint, (onTarget ? 4.5f : 3f) * (1f + _power), 3.5f + _power * 2f, 0.2f);
            }
            else if (_packStyle)
            {
                SpawnPackImpact(point, back, onTarget, expired);
                if (!expired) Vfx3D.Flash(point + Vector3.up * 0.2f, _tint, onTarget ? 4.5f : 2.5f, 3.5f, 0.18f);
            }
            else if (_use3D)
            {
                if (expired)
                {
                    // Fin de portée : simple dissipation, sans flash ni son.
                    Vfx3D.Spawn(Vfx3D.Library.impact, point, Quaternion.LookRotation(back), impact3DScale * 0.5f, _tint);
                }
                else
                {
                    float scale = onTarget ? enemyImpact3DScale : impact3DScale;
                    Vfx3D.Spawn(Vfx3D.Library.impact, point, Quaternion.LookRotation(back), scale, _tint);
                    Vfx3D.Flash(point + Vector3.up * 0.2f, _tint, onTarget ? 4.5f : 2.5f, 3.5f, 0.18f);
                }
            }
            else if (impactVfxPrefab != null)
                Instantiate(impactVfxPrefab, point, Quaternion.identity);

            if (!expired)
            {
                Sfx.Play(onTarget ? enemyHitClips : wallHitClips, point);
                if (onTarget)
                {
                    if (hitStop) HitStop.Hit(hitStopDuration * (1f + _power));
                    if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(hitShakeStrength * (1f + _power * 1.5f), hitShakeDuration);
                }
                else if (_bolt && CameraFollow.Instance != null)
                    CameraFollow.Instance.Shake(hitShakeStrength * 0.5f * (1f + _power * 2f), hitShakeDuration);
            }

            Release();
        }
    }
}
