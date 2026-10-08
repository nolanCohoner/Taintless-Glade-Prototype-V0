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

        DamageInfo _damage;
        Vector3 _velocity;
        float _remainingRange;
        Transform _owner;
        bool _launched;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        public void Launch(DamageInfo damage, Vector3 velocity, float range, Transform owner)
        {
            _damage = damage;
            _velocity = velocity;
            _remainingRange = range;
            _owner = owner;
            _launched = true;
        }

        void Update()
        {
            if (!_launched) return;

            float speed = _velocity.magnitude;
            if (speed < 0.0001f) { Impact(transform.position); return; }

            Vector3 dir = _velocity / speed;
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
            if (impactVfxPrefab != null)
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
