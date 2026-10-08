using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Squash &amp; stretch avec un petit ressort amorti (feedback de tir, dash, coup reçu).
    /// </summary>
    public class ScalePunch : MonoBehaviour
    {
        public float stiffness = 300f;
        public float damping = 14f;

        Vector3 _baseScale;
        Vector3 _offset;
        Vector3 _velocity;

        void Awake() => _baseScale = transform.localScale;

        /// <summary>amount = variation relative de l'échelle, ex. (0.2, -0.2, 0) = écrasé.</summary>
        public void Punch(Vector3 amount)
        {
            _offset = amount;
            _velocity = Vector3.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _velocity += (-stiffness * _offset - damping * _velocity) * dt;
            _offset += _velocity * dt;
            transform.localScale = Vector3.Scale(_baseScale, Vector3.one + _offset);
        }
    }
}
