using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaintlessGlade
{
    /// <summary>
    /// Rayon de la mort arc-en-ciel : clic gauche maintenu pendant "chargeTime" secondes -> un énorme rayon part dans la direction
    /// de la visée, tant que le clic reste maintenu (durée max "maxDuration"). Il traverse tout, s'arrête sur les murs et inflige
    /// "damageMultiplier" x l'attaque à chaque tir normal.
    /// Rendu 100 % 2D image par image (voir Fx2D) : bande arc-en-ciel contourée à 4 frames, orbe, impact frames.
    /// PlayerAttack l'ajoute tout seul au joueur.
    /// </summary>
    public class RainbowBeam : MonoBehaviour
    {
        [Header("Déclenchement")]
        [Tooltip("Secondes de clic maintenu avant que le rayon parte.")]
        public float chargeTime = 2.5f;
        [Tooltip("Durée max du rayon (s). Ensuite la charge repart de zéro.")]
        public float maxDuration = 4f;

        [Header("Dégâts")]
        [Tooltip("Dégâts du rayon à chaque tick = attaque du perso x ce facteur. Un tick par intervalle d'attaque normal.")]
        public float damageMultiplier = 5f;
        public LayerMask hitMask = ~0;

        [Header("Forme")]
        public float length = 22f;
        [Tooltip("Rayon de la zone qui touche les ennemis.")]
        public float hitRadius = 0.9f;
        [Tooltip("Largeur visible de la bande (unités).")]
        public float beamWidth = 1.7f;
        public float height = 0.75f;
        public float forwardOffset = 0.5f;
        [Tooltip("Images par seconde de l'animation de la bande.")]
        public float framesPerSecond = 12f;

        public float Charge01 => Mathf.Clamp01(_charge / Mathf.Max(chargeTime, 0.01f));
        public bool IsFiring { get; private set; }
        /// <summary>Déclenché à chaque tick de dégâts (animation, son...).</summary>
        public event Action Ticked;

        PlayerController _controller;
        PlayerAttack _attack;
        Transform _visuals;
        SpriteRenderer _beam, _orb, _endOrb;
        Light _light;
        float _charge, _fireTime, _tickTimer, _power;
        bool _needRelease;
        readonly RaycastHit[] _hits = new RaycastHit[32];
        readonly HashSet<IDamageable> _damaged = new HashSet<IDamageable>();

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _attack = GetComponent<PlayerAttack>();
            BuildVisuals();
        }

        void OnDisable()
        {
            _charge = 0f;
            IsFiring = false;
            _power = 0f;
            if (_visuals != null) _visuals.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool held = _attack != null && _attack.IsCharging;

            if (IsFiring)
            {
                _fireTime += dt;
                if (!held || _fireTime >= maxDuration)
                {
                    IsFiring = false;
                    _charge = 0f;
                    _needRelease = held; // après un rayon, il faut relâcher le clic avant de recharger
                }
                else
                {
                    _tickTimer -= dt;
                    if (_tickTimer <= 0f) DoTick();
                }
            }
            else if (!held)
            {
                _charge = 0f;
                _needRelease = false;
            }
            else if (!_needRelease)
            {
                _charge += dt;
                if (_charge >= chargeTime) StartFiring();
            }

            _power = Mathf.MoveTowards(_power, IsFiring ? 1f : 0f, dt * (IsFiring ? 7f : 5f));
        }

        void StartFiring()
        {
            IsFiring = true;
            _fireTime = 0f;
            _tickTimer = 0f;
            DoTick();
        }

        // ---------- Dégâts ----------

        Vector3 Origin(Vector3 dir) => transform.position + Vector3.up * height + dir * forwardOffset;

        /// <summary>Longueur utile du rayon : s'arrête au premier obstacle qui n'est pas une cible (mur, pilier...).</summary>
        float BlockedLength(Vector3 origin, Vector3 dir)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, _hits, length, hitMask, QueryTriggerInteraction.Ignore);
            float cut = length;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<IDamageable>() != null) continue;
                if (h.distance < cut) cut = h.distance;
            }
            return cut;
        }

        void DoTick()
        {
            CharacterStats stats = _controller != null ? _controller.stats : null;
            float attack = stats != null ? stats.attack : 1f;
            _tickTimer = stats != null ? stats.AttackCooldown : 0.33f;

            Vector3 dir = _controller.AimDirection;
            Vector3 origin = Origin(dir);
            float cut = BlockedLength(origin, dir);

            _damaged.Clear();
            int count = Physics.SphereCastNonAlloc(origin, hitRadius, dir, _hits, cut, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = _hits[i].collider;
                if (col.transform.IsChildOf(transform)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !_damaged.Add(target)) continue;

                Vector3 point = col.ClosestPoint(origin + dir * Mathf.Min(_hits[i].distance + hitRadius, cut));
                target.TakeDamage(new DamageInfo
                {
                    amount = attack * damageMultiplier,
                    point = point,
                    direction = dir,
                    source = gameObject,
                    rainbow = true,
                });
            }

            // Impact frames au bout du rayon quand il frappe un mur.
            if (cut < length - 0.05f)
                Fx2D.SpawnImpact(origin + dir * cut, Fx2D.Rainbow[UnityEngine.Random.Range(0, Fx2D.Rainbow.Length)], 1.8f);

            if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.12f, 0.12f);
            Ticked?.Invoke();
        }

        // ---------- Visuels (2D image par image) ----------

        void LateUpdate()
        {
            Camera cam = Camera.main;
            float charge = Charge01;
            bool showCharge = !IsFiring && charge > 0.05f;
            bool showBeam = _power > 0.01f;
            _visuals.gameObject.SetActive(showCharge || showBeam);
            if (!showCharge && !showBeam || cam == null) return;

            Vector3 dir = _controller.AimDirection;
            Vector3 origin = Origin(dir);
            float t = Time.time;
            float hue = Mathf.Repeat(t * 1.5f, 1f);
            Quaternion facing = cam.transform.rotation;

            // --- Orbe arc-en-ciel au museau : grossit et tremble pendant la charge, gros et vibrant pendant le tir ---
            _orb.transform.SetPositionAndRotation(origin, facing * Quaternion.Euler(0f, 0f, t * (showBeam ? 220f : 60f + charge * 300f)));
            float orbSize = showBeam
                ? (1.9f + 0.25f * Mathf.Sin(t * 40f)) * _power
                : Mathf.Lerp(0.15f, 1.25f, charge * charge) * (1f + 0.1f * Mathf.Sin(t * 30f));
            _orb.transform.localScale = Vector3.one * orbSize;

            _light.color = Color.HSVToRGB(hue, 0.7f, 1f);
            _light.transform.position = origin + Vector3.up * 0.4f;
            _light.intensity = showBeam ? 14f * _power : charge * 4f;
            _light.range = showBeam ? 11f : 5f;

            _beam.enabled = showBeam;
            _endOrb.enabled = showBeam;
            if (!showBeam) return;

            // --- Bande : sprite à plat face caméra, orienté comme le rayon À L'ÉCRAN, étirée sur sa longueur ---
            float cut = BlockedLength(origin, dir);
            Vector3 camRight = cam.transform.right, camUp = cam.transform.up;
            var onScreen = new Vector2(Vector3.Dot(dir, camRight), Vector3.Dot(dir, camUp));
            Vector3 projected = camRight * onScreen.x + camUp * onScreen.y; // direction du rayon dans le plan de l'écran
            float grow = Mathf.SmoothStep(0f, 1f, _power);
            float screenLength = Mathf.Max(onScreen.magnitude * cut * grow, 0.05f);
            float angle = Mathf.Atan2(onScreen.y, onScreen.x) * Mathf.Rad2Deg;

            float pulse = 1f + 0.07f * Mathf.Sin(t * 35f);
            float scale = beamWidth * _power * pulse;
            Sprite[] frames = Fx2D.BeamFrames;
            _beam.sprite = frames[(int)(t * framesPerSecond) % frames.Length];
            // En mode Sliced, les extrémités gardent leur taille : on agrandit par l'échelle pour qu'elles restent rondes.
            _beam.size = new Vector2(Mathf.Max(screenLength / Mathf.Max(scale, 0.01f), 2.2f), 1f);
            _beam.transform.SetPositionAndRotation(origin + projected * (cut * grow * 0.5f), facing * Quaternion.Euler(0f, 0f, angle));
            _beam.transform.localScale = new Vector3(scale, scale, 1f);

            // --- Orbe au bout : tourne en sens inverse ---
            _endOrb.transform.SetPositionAndRotation(origin + projected * (cut * grow), facing * Quaternion.Euler(0f, 0f, -t * 260f));
            _endOrb.transform.localScale = Vector3.one * ((1.7f + 0.3f * Mathf.Sin(t * 33f)) * _power);
        }

        void BuildVisuals()
        {
            _visuals = new GameObject("RainbowBeamVisuals").transform;
            // Hors du joueur : le rayon reste en espace monde et n'hérite ni du billboard ni de l'échelle du perso.
            _visuals.SetParent(null);
            _visuals.gameObject.SetActive(false);

            _beam = Fx2D.CreateRenderer("Beam", _visuals, Fx2D.BeamFrames[0], Color.white, Fx2D.SortingOrder);
            _beam.drawMode = SpriteDrawMode.Sliced;
            _beam.enabled = false;
            _orb = Fx2D.CreateRenderer("Orb", _visuals, Fx2D.RainbowOrb, Color.white, Fx2D.SortingOrder + 2);
            _endOrb = Fx2D.CreateRenderer("EndOrb", _visuals, Fx2D.RainbowOrb, Color.white, Fx2D.SortingOrder + 1);
            _endOrb.enabled = false;

            var lightGo = new GameObject("BeamLight");
            lightGo.transform.SetParent(_visuals, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
        }

        void OnDestroy()
        {
            if (_visuals != null) Destroy(_visuals.gameObject);
        }
    }
}
