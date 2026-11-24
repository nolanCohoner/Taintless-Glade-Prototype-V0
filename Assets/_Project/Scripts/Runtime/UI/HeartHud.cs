using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Vie du joueur affichée comme un liquide rouge dans un récipient en forme de cœur.
    /// Le niveau suit la vie en douceur (volume réel du cœur) ; la surface ballotte comme si le perso tenait la bouteille :
    /// elle penche quand il accélère, s'agite quand il court, et reçoit une secousse à chaque tir, dash et coup reçu.
    /// Un reflet animé passe sur le métal du cadre toutes les quelques secondes.
    /// </summary>
    public class HeartHud : MonoBehaviour
    {
        [Header("Niveau")]
        [Tooltip("Durée (s) du lissage du niveau quand la vie change.")]
        public float levelSmoothTime = 0.55f;

        [Header("Ballottement")]
        [Tooltip("Raideur / amortissement de l'inclinaison : amortissement faible = plus de rebonds.")]
        public float tiltStiffness = 60f;
        public float tiltDamping = 5.5f;
        [Tooltip("Inclinaison causée par l'accélération du perso (inertie du liquide).")]
        public float accelerationToTilt = 1.6f;
        [Tooltip("Pente maximale de la surface (px par px) à inclinaison maximale.")]
        public float maxSlope = 0.3f;
        public float bobStiffness = 130f;
        public float bobDamping = 7f;
        [Tooltip("Secousses (vitesse initiale) au tir, au dash et à la perte de vie.")]
        public float attackTiltKick = 5f, attackBobKick = 220f;
        public float dashTiltKick = 9f;
        public float damageBobKick = 420f, damageTiltKick = 8f;

        HudConfig _config;
        Image _frame;
        TMP_Text _text;
        HeartLiquidGraphic _back, _front;
        RectTransform _rect;
        Vector2 _restPosition;

        PlayerHealth _health;
        PlayerController _controller;
        PlayerAttack _attack;

        float _target = 1f, _shown = 1f, _shownVelocity;
        float _tilt, _tiltVelocity, _bob, _bobVelocity;
        float _lastVx, _ax, _agitation, _phase;
        float _shake, _textPunch;
        float _nextReflection, _reflectionTime = -1f;
        int _lastShownHp = int.MinValue;

        public void Setup(HudConfig config, Image frame, TMP_Text text, HeartLiquidGraphic back, HeartLiquidGraphic front)
        {
            _config = config;
            _frame = frame;
            _text = text;
            _back = back;
            _front = front;
            _rect = (RectTransform)transform;
            _restPosition = _rect.anchoredPosition;
            _nextReflection = Time.unscaledTime + Mathf.Max(1f, config.reflectionInterval);
        }

        void OnDisable() => Unsubscribe();

        void Unsubscribe()
        {
            if (_health != null) _health.Changed -= OnHealthChanged;
            if (_attack != null) _attack.Fired -= OnFired;
            if (_controller != null) _controller.DashStarted -= OnDash;
            _health = null;
            _attack = null;
            _controller = null;
        }

        void FindPlayer()
        {
            if (_health != null && _controller != null) return;
            _controller = FindFirstObjectByType<PlayerController>();
            if (_controller == null) return;
            _health = _controller.GetComponent<PlayerHealth>();
            _attack = _controller.GetComponent<PlayerAttack>();
            if (_health == null) return;

            _health.Changed += OnHealthChanged;
            _controller.DashStarted += OnDash;
            if (_attack != null) _attack.Fired += OnFired;
            _target = _health.Max > 0f ? _health.Current / _health.Max : 0f;
            _shown = _target;
        }

        /// <summary>Composante horizontale (à l'écran) d'un vecteur monde.</summary>
        static float ScreenX(Vector3 world)
        {
            Camera cam = Camera.main;
            if (cam == null) return world.x;
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            return Vector3.Dot(world, right);
        }

        void OnFired()
        {
            _tiltVelocity += -ScreenX(_controller.AimDirection) * attackTiltKick;
            _bobVelocity += attackBobKick;
            _agitation = Mathf.Max(_agitation, 0.6f);
        }

        void OnDash()
        {
            _tiltVelocity += -ScreenX(_controller.DashDirection) * dashTiltKick;
            _bobVelocity += attackBobKick;
            _agitation = 1f;
        }

        void OnHealthChanged(float current, float previous)
        {
            _target = _health.Max > 0f ? current / _health.Max : 0f;
            if (current >= previous) return;

            // Perte de vie : le liquide plonge, ballotte fort, le cœur tremble et le texte gonfle.
            _bobVelocity += damageBobKick;
            _tiltVelocity += (Random.value < 0.5f ? -1f : 1f) * damageTiltKick;
            _agitation = 1f;
            _shake = 0.3f;
            _textPunch = 1f;
        }

        void Update()
        {
            if (_config == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (dt <= 0f) return;

            FindPlayer();
            Simulate(dt);
            UpdateLiquid();
            UpdateText(dt);
            UpdateShake(dt);
            UpdateReflection();
        }

        void Simulate(float dt)
        {
            float speed01 = 0f;
            if (_controller != null)
            {
                Vector3 velocity = _controller.Velocity;
                float vx = ScreenX(velocity);
                float ax = (vx - _lastVx) / dt;
                _lastVx = vx;
                _ax = Mathf.Lerp(_ax, ax, 1f - Mathf.Exp(-25f * dt));
                speed01 = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, _controller.RunSpeed));
            }

            // Inclinaison : ressort amorti poussé par l'inertie (le liquide reste en arrière quand le perso accélère).
            _tiltVelocity += (-_ax * accelerationToTilt * 0.1f - tiltStiffness * _tilt - tiltDamping * _tiltVelocity) * dt;
            _tilt += _tiltVelocity * dt;
            if (Mathf.Abs(_tilt) > 1f)
            {
                _tilt = Mathf.Sign(_tilt);
                _tiltVelocity *= 0.5f;
            }

            // Rebond vertical du niveau (secousses de tir, de coup reçu).
            _bobVelocity += (-bobStiffness * _bob - bobDamping * _bobVelocity) * dt;
            _bob += _bobVelocity * dt;

            // Agitation : retombe doucement, mais marcher / courir la maintient.
            _agitation = Mathf.Max(_agitation - 1.4f * dt, speed01 * 0.55f);
            _phase += dt * (5f + 9f * _agitation);

            _shown = Mathf.SmoothDamp(_shown, _target, ref _shownVelocity, levelSmoothTime, float.MaxValue, dt);
        }

        void UpdateLiquid()
        {
            bool filled = _shown > 0.004f;
            float walkBob = Mathf.Sin(_phase * 2.3f) * _agitation * 5f;
            float level = HeartLiquidGraphic.SurfaceForFraction(_shown) + _bob + walkBob;
            float slope = _tilt * maxSlope;
            float amp1 = 4f + 26f * _agitation;
            float amp2 = 2f + 11f * _agitation;

            Apply(_front, filled, level, slope, amp1, amp2, _phase, _phase * 0.8f);
            // Deuxième nappe, plus sombre, un peu plus haute et déphasée : donne de la profondeur au liquide.
            Apply(_back, filled, level - 14f, slope * 0.85f, amp1 * 0.9f, amp2 * 1.2f, _phase + 1.9f, _phase * 0.7f + 0.6f);
        }

        static void Apply(HeartLiquidGraphic g, bool filled, float v, float slope, float a1, float a2, float p1, float p2)
        {
            if (g == null) return;
            g.filled = filled;
            g.surfaceV = v;
            g.slope = slope;
            g.amplitude1 = a1;
            g.amplitude2 = a2;
            g.phase1 = p1;
            g.phase2 = p2;
            g.SetVerticesDirty();
        }

        void UpdateText(float dt)
        {
            if (_text == null) return;
            int hp = _health != null ? Mathf.CeilToInt(_health.Current) : 0;
            int max = _health != null ? Mathf.RoundToInt(_health.Max) : 0;
            if (hp != _lastShownHp)
            {
                _lastShownHp = hp;
                _text.text = hp + "<size=65%>/" + max + "</size>";
            }
            _textPunch = Mathf.MoveTowards(_textPunch, 0f, dt * 3.5f);
            _text.rectTransform.localScale = Vector3.one * (1f + 0.35f * _textPunch * _textPunch);
        }

        void UpdateShake(float dt)
        {
            _shake = Mathf.MoveTowards(_shake, 0f, dt);
            Vector2 offset = _shake > 0f ? Random.insideUnitCircle * (_shake * 40f) : Vector2.zero;
            _rect.anchoredPosition = _restPosition + offset;
        }

        /// <summary>Toutes les reflectionInterval secondes, les frames du reflet remplacent le cadre normal le temps de l'animation.</summary>
        void UpdateReflection()
        {
            Sprite[] frames = _config.reflectionFrames;
            if (_frame == null || frames == null || frames.Length == 0) return;

            if (_reflectionTime < 0f)
            {
                if (Time.unscaledTime < _nextReflection) return;
                _reflectionTime = 0f;
            }

            _reflectionTime += Time.unscaledDeltaTime;
            int index = Mathf.FloorToInt(_reflectionTime * Mathf.Max(1f, _config.reflectionFps));
            if (index >= frames.Length)
            {
                _reflectionTime = -1f;
                _nextReflection = Time.unscaledTime + Mathf.Max(1f, _config.reflectionInterval);
                _frame.sprite = _config.heartFrame;
                return;
            }
            _frame.sprite = frames[index];
        }
    }
}
