using UnityEngine;

namespace TaintlessGlade
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;
        /// <summary>Coup du rayon arc-en-ciel (impact multicolore).</summary>
        public bool rainbow;
    }

    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
