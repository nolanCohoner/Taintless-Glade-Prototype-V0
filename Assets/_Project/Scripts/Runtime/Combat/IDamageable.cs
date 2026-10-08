using UnityEngine;

namespace TaintlessGlade
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;
    }

    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
