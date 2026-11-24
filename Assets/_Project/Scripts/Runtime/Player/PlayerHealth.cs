using UnityEngine;
using UnityEngine.InputSystem;

namespace TaintlessGlade
{
    /// <summary>
    /// Points de vie du joueur (maxHp des stats : 3 pour l'Axolotl). Pour ce premier test, la vie baisse
    /// au contact d'un pantin d'entraînement et à chaque attaque ; pas d'écran de mort pour le moment.
    /// Ajouté automatiquement par le PlayerController.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Test")]
        [Tooltip("Points de vie perdus à chaque attaque (tir normal ou chargé).")]
        public float damagePerAttack = 1f;
        [Tooltip("Points de vie perdus au contact d'un pantin d'entraînement.")]
        public float collisionDamage = 1f;
        [Tooltip("Délai (s) pendant lequel le contact avec un pantin ne blesse plus après un coup.")]
        public float invulnerability = 0.8f;
        [Tooltip("Touche pour se soigner complètement (test).")]
        public Key healKey = Key.H;

        public float Current { get; private set; }
        public float Max => _controller != null && _controller.stats != null ? _controller.stats.maxHp : 3f;

        /// <summary>Vie actuelle, vie précédente.</summary>
        public event System.Action<float, float> Changed;

        PlayerController _controller;
        float _invulnerableUntil;
        bool _initialized;

        void Awake() => _controller = GetComponent<PlayerController>();

        void Start() => Init();

        void Init()
        {
            if (_initialized) return;
            _initialized = true;
            Current = Max;
        }

        void Update()
        {
            Init();
            if (Keyboard.current != null && Keyboard.current[healKey].wasPressedThisFrame) Heal();
        }

        public void Heal()
        {
            Init();
            Set(Max);
        }

        /// <summary>Appelé par PlayerAttack à chaque tir.</summary>
        public void OnAttack() => Hurt(damagePerAttack);

        public void Hurt(float amount)
        {
            Init();
            if (amount <= 0f) return;
            Set(Mathf.Max(0f, Current - amount));
        }

        void Set(float value)
        {
            float previous = Current;
            Current = Mathf.Clamp(value, 0f, Max);
            if (!Mathf.Approximately(previous, Current)) Changed?.Invoke(Current, previous);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Time.time < _invulnerableUntil) return;
            if (hit.collider.GetComponentInParent<TrainingDummy>() == null) return;

            _invulnerableUntil = Time.time + invulnerability;
            Hurt(collisionDamage);

            Vector3 away = transform.position - hit.collider.bounds.center;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) _controller.Recoil(away.normalized * 7f);
            if (_controller.spritePunch != null) _controller.spritePunch.Punch(new Vector3(0.3f, -0.25f, 0f));
            if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.08f, 0.12f);
        }
    }
}
