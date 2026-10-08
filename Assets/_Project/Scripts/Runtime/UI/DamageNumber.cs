using TMPro;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Chiffre de dégâts en 3D. Phase 1 : reste en place tant que le combo continue (punch à chaque coup).
    /// Phase 2 : monte et disparaît.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        public TMP_Text text;

        [Header("Couleurs")]
        public Color singleHitColor = new Color(1f, 0.97f, 0.85f);
        public Color comboStartColor = new Color(1f, 0.8f, 0.2f);
        public Color comboMaxColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Nombre de coups pour atteindre la couleur max.")]
        public int hitsForMaxColor = 10;

        [Header("Animation")]
        public float punchScale = 1.6f;
        public float punchRecoverSpeed = 8f;
        public float growthPerHit = 0.06f;
        public float maxGrowth = 0.8f;
        public float holdDriftSpeed = 0.15f;
        public float riseSpeed = 1.6f;
        public float fadeDuration = 0.6f;

        float _total;
        int _hits;
        float _holdTimer;
        float _holdDuration;
        float _punch;
        float _fadeTimer;
        Vector3 _baseScale;

        public bool IsAccumulating => _holdTimer > 0f;

        void Awake()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            _baseScale = transform.localScale;
        }

        public void Init(float amount, float holdDuration)
        {
            _total = amount;
            _hits = 1;
            _holdDuration = holdDuration;
            _holdTimer = holdDuration;
            _punch = 1f;
            Refresh();
        }

        public void AddHit(float amount)
        {
            _total += amount;
            _hits++;
            _holdTimer = _holdDuration;
            _punch = 1f;
            Refresh();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _punch = Mathf.MoveTowards(_punch, 0f, punchRecoverSpeed * dt);

            float growth = Mathf.Min(maxGrowth, (_hits - 1) * growthPerHit);
            float punch = 1f + (punchScale - 1f) * _punch * _punch;
            transform.localScale = _baseScale * ((1f + growth) * punch);

            if (_holdTimer > 0f)
            {
                _holdTimer -= dt;
                transform.position += Vector3.up * (holdDriftSpeed * dt);
                return;
            }

            transform.position += Vector3.up * (riseSpeed * dt);
            _fadeTimer += dt;
            text.alpha = 1f - Mathf.Clamp01(_fadeTimer / fadeDuration);
            if (_fadeTimer >= fadeDuration)
                Destroy(gameObject);
        }

        void Refresh()
        {
            int value = Mathf.RoundToInt(_total);
            if (_hits > 1)
            {
                text.text = $"{value}<size=55%> x{_hits}</size>";
                text.color = Color.Lerp(comboStartColor, comboMaxColor, (_hits - 2f) / Mathf.Max(1, hitsForMaxColor - 2));
            }
            else
            {
                text.text = value.ToString();
                text.color = singleHitColor;
            }
        }
    }
}
