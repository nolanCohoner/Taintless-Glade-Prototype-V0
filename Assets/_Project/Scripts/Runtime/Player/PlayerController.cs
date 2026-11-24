using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Déplacement top-down (relatif à la caméra), dash et direction de visée.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerControls))]
    public class PlayerController : MonoBehaviour
    {
        public CharacterStats stats;

        [Header("Mouvement")]
        public float acceleration = 70f;
        public float gravity = -30f;
        [Tooltip("Multiplicateur de vitesse en course (touche Maj).")]
        public float runSpeedMultiplier = 1.6f;

        [Header("Dash")]
        public float dashDuration = 0.14f;
        public float dashCooldown = 0.45f;
        public ParticleSystem dashParticles;

        [Header("Visuel")]
        [Tooltip("Pivot de la flèche de visée au sol, tourné vers la direction de visée.")]
        public Transform aimIndicator;
        public ScalePunch spritePunch;
        [Tooltip("Hauteur du plan utilisé pour viser à la souris (hauteur des projectiles).")]
        public float aimPlaneHeight = 0.5f;

        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public Vector3 DashDirection => _dashDirection;
        /// <summary>Déclenché au début du dash (position de départ) et à son arrivée (position d'arrivée).</summary>
        public event System.Action DashStarted, DashEnded;
        public bool IsDashing => _dashTimer > 0f;
        public bool IsRunning { get; private set; }
        public Vector3 Velocity => _velocity;
        public float WalkSpeed => stats.MoveSpeed;
        public float RunSpeed => stats.MoveSpeed * runSpeedMultiplier;

        CharacterController _cc;
        PlayerControls _controls;
        Camera _cam;
        Vector3 _velocity;
        float _verticalVelocity;
        float _dashTimer;
        float _dashCooldownTimer;
        Vector3 _dashDirection;
        Vector3 _impulse;

        /// <summary>Petite poussée qui retombe vite (recul du tir).</summary>
        public void Recoil(Vector3 velocity)
        {
            if (!IsDashing) _impulse += velocity;
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _controls = GetComponent<PlayerControls>();
            if (GetComponent<DashVfx>() == null) gameObject.AddComponent<DashVfx>();
            if (GetComponent<PlayerHealth>() == null) gameObject.AddComponent<PlayerHealth>();
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            float dt = Time.deltaTime;

            Vector3 moveDir = CameraRelative(_controls.Move);
            UpdateAim(moveDir);

            _dashCooldownTimer -= dt;
            if (_controls.DashPressed && _dashCooldownTimer <= 0f && !IsDashing)
                StartDash(moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : AimDirection);

            if (IsDashing)
            {
                _dashTimer -= dt;
                _velocity = _dashDirection * (stats.DashDistance / dashDuration);
                if (!IsDashing)
                {
                    _velocity = _dashDirection * stats.MoveSpeed;
                    DashEnded?.Invoke();
                }
            }
            else
            {
                IsRunning = _controls.RunHeld && moveDir.sqrMagnitude > 0.01f;
                _velocity = Vector3.MoveTowards(_velocity, moveDir * (IsRunning ? RunSpeed : WalkSpeed), acceleration * dt);
            }

            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * dt;

            _impulse = Vector3.MoveTowards(_impulse, Vector3.zero, 20f * dt);
            _cc.Move((_velocity + _impulse + Vector3.up * _verticalVelocity) * dt);

            if (aimIndicator != null)
                aimIndicator.rotation = Quaternion.LookRotation(AimDirection, Vector3.up);
        }

        void StartDash(Vector3 direction)
        {
            _dashDirection = direction;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;
            DashStarted?.Invoke();
            if (spritePunch != null) spritePunch.Punch(new Vector3(-0.3f, 0.3f, 0f));
        }

        Vector3 CameraRelative(Vector2 input)
        {
            if (_cam == null) return new Vector3(input.x, 0f, input.y);
            Vector3 forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        void UpdateAim(Vector3 moveDir)
        {
            Vector3 aim = Vector3.zero;

            if (_controls.UsingMouseAim && _cam != null)
            {
                Ray ray = _cam.ScreenPointToRay(_controls.PointerPosition);
                var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + aimPlaneHeight, 0f));
                if (plane.Raycast(ray, out float enter))
                    aim = ray.GetPoint(enter) - transform.position;
            }
            else if (_controls.AimStick.sqrMagnitude > 0.1f)
            {
                aim = CameraRelative(_controls.AimStick);
            }
            else if (moveDir.sqrMagnitude > 0.01f)
            {
                aim = moveDir;
            }

            aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f)
                AimDirection = aim.normalized;
        }
    }
}
