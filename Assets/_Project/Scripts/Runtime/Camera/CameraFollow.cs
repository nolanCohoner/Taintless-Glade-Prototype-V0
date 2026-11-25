using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Caméra top-down inclinée qui suit la cible, avec limites de salle optionnelles et screen shake.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

        [Header("Cadrage")]
        [Range(30f, 90f)] public float pitch = 60f;
        public float yaw = 0f;
        public float distance = 15f;
        public float smoothTime = 0.12f;

        [Header("Limites de salle (optionnel)")]
        [Tooltip("Le point visé par la caméra reste dans cette boîte (\"hitbox caméra\" du GDD).")]
        public BoxCollider roomBounds;

        Vector3 _focus;
        Vector3 _focusVelocity;
        float _shakeTimer;
        float _shakeDuration;
        float _shakeStrength;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => SnapToTarget();

        public void Shake(float strength, float duration)
        {
            strength *= GameSettings.ScreenShake;
            if (strength <= 0f) return;
            if (strength < _shakeStrength * (_shakeTimer / Mathf.Max(_shakeDuration, 0.0001f))) return;
            _shakeStrength = strength;
            _shakeDuration = duration;
            _shakeTimer = duration;
        }

        [ContextMenu("Snap To Target")]
        public void SnapToTarget()
        {
            if (target == null) return;
            _focus = DesiredFocus();
            _focusVelocity = Vector3.zero;
            ApplyTransform(Vector3.zero);
        }

        void LateUpdate()
        {
            if (target == null) return;

            _focus = Vector3.SmoothDamp(_focus, DesiredFocus(), ref _focusVelocity, smoothTime);

            Vector3 shake = Vector3.zero;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float falloff = Mathf.Clamp01(_shakeTimer / _shakeDuration);
                shake = Random.insideUnitSphere * (_shakeStrength * falloff);
            }

            ApplyTransform(shake);
        }

        Vector3 DesiredFocus()
        {
            Vector3 focus = target.position + targetOffset;
            if (roomBounds != null)
            {
                Vector3 center = roomBounds.transform.TransformPoint(roomBounds.center);
                Vector3 extents = Vector3.Scale(roomBounds.size, roomBounds.transform.lossyScale) * 0.5f;
                focus.x = Mathf.Clamp(focus.x, center.x - extents.x, center.x + extents.x);
                focus.z = Mathf.Clamp(focus.z, center.z - extents.z, center.z + extents.z);
            }
            return focus;
        }

        void ApplyTransform(Vector3 shake)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * distance + shake, rotation);
        }
    }
}
