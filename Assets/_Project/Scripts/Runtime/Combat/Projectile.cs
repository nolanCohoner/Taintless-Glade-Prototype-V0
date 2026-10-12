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

        [Header("Rendu 2D (remplace les particules du prefab)")]
        public bool use2DVisuals = true;
        public Color ballColor = new Color(0.55f, 0.95f, 1f);
        [Tooltip("Taille de la boule (unités).")]
        public float ballSize = 0.42f;
        [Tooltip("Intervalle (s) entre deux bulles de traînée.")]
        public float trailInterval = 0.04f;
        public float impactSize = 1f;

        DamageInfo _damage;
        Vector3 _velocity;
        float _remainingRange;
        Transform _owner;
        bool _launched;
        Transform _ball;
        float _trailTimer;
        float _wobblePhase;        readonly RaycastHit[] _hits = new RaycastHit[8];

        public void Launch(DamageInfo damage, Vector3 velocity, float range, Transform owner)
        {
            _damage = damage;
            _velocity = velocity;
            _remainingRange = range;
            _owner = owner;
            _launched = true;
            if (use2DVisuals) Setup2D();
        }

        /// <summary>Cache les particules du prefab et dessine une boule 2D contourée, étirée dans le sens du mouvement.</summary>
        void Setup2D()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _wobblePhase = Random.value * 6.28f;
            var root = new GameObject("Ball2D");
            root.transform.SetParent(transform, false);
            root.AddComponent<Billboard>();
            _ball = Fx2D.CreateRenderer("Sprite", root.transform, Fx2D.Ball, ballColor).transform;
        }

        void Update2D(Vector3 dir, float speed)
        {
            if (_ball == null) return;
            Camera cam = Camera.main;
            float angle = 0f;
            if (cam != null)
                angle = Mathf.Atan2(Vector3.Dot(dir, cam.transform.up), Vector3.Dot(dir, cam.transform.right)) * Mathf.Rad2Deg;

            // Squash & stretch : allongée dans le sens du vol, avec un petit battement.
            float wobble = 1f + 0.08f * Mathf.Sin(Time.time * 35f + _wobblePhase);
            _ball.localRotation = Quaternion.Euler(0f, 0f, angle);
            _ball.localScale = new Vector3(1.3f * wobble, 0.85f / wobble, 1f) * ballSize;

            _trailTimer -= Time.deltaTime;
            if (_trailTimer <= 0f)
            {
                _trailTimer = trailInterval;
                Fx2D.SpawnPuff(transform.position, ballColor, ballSize * 0.7f, 0.22f);
            }
        }
        void Update()
        {
            if (!_launched) return;

            float speed = _velocity.magnitude;
            if (speed < 0.0001f) { Impact(transform.position); return; }

            Vector3 dir = _velocity / speed;
            if (use2DVisuals) Update2D(dir, speed);
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
                    damageable.TakeDamage(info);
                }
                Impact(point);
                return;
            }

            transform.position += dir * stepDistance;
            _remainingRange -= stepDistance;
            if (_remainingRange <= 0f)
                Impact(transform.position);
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
                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        void Impact(Vector3 point)
        {
            if (use2DVisuals)
                Fx2D.SpawnImpact(point, ballColor, impactSize);
            else if (impactVfxPrefab != null)
                Instantiate(impactVfxPrefab, point, Quaternion.identity);

            // Laisse les traînées de particules finir de se dissiper au lieu de les couper net.
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            {
                ps.transform.SetParent(null, true);
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(gameObject);
        }
    }
}
