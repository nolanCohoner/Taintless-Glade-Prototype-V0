using UnityEngine;

namespace TaintlessGlade
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;
        /// <summary>Force de poussée à appliquer à la cible, dans "direction" (0 = aucune).</summary>
        public float knockback;
    }

    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
