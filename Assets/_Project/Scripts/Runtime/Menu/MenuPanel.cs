using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Groupe d'UI qui apparaît / disparaît avec un ressort nerveux (rebond d'échelle + glissement + fondu).
    /// Désactive ses éléments cliquables tant qu'il est caché.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MenuPanel : MonoBehaviour
    {
        [Tooltip("Décalage de départ (px) : le panneau glisse depuis cette position vers sa place.")]
        public Vector2 hiddenOffset = new Vector2(0f, -70f);
        [Range(0.5f, 1f)] public float hiddenScale = 0.86f;
        public float stiffness = 380f;
        public float damping = 17f;
        [Tooltip("Reste actif (mais invisible) une fois caché.")]
        public bool keepActive;

        CanvasGroup _group;
        RectTransform _rect;
        Vector2 _basePosition;
        Spring _show;
        float _delay;
        bool _initialized;

        public bool IsShown => _show.target > 0.5f;
        public bool IsFullyHidden => _show.target < 0.5f && _show.value < 0.02f;

        void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _group = GetComponent<CanvasGroup>();
            _rect = transform as RectTransform;
            _basePosition = _rect.anchoredPosition;
        }

        void Awake() => Init();

        /// <summary>Cache immédiatement (sans animation).</summary>
        public void HideInstant()
        {
            Init();
            _show.Snap(0f);
            _delay = 0f;
            Apply();
            if (!keepActive) gameObject.SetActive(false);
        }

        public void Show(float delay = 0f)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Init();
            _show.target = 1f;
            _delay = delay;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        public void Hide()
        {
            Init();
            _show.target = 0f;
            _delay = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        void Update()
        {
            if (_delay > 0f)
            {
                _delay -= Time.unscaledDeltaTime;
                if (_delay > 0f) { Apply(); return; }
            }
            bool showing = _show.target > 0.5f;
            _show.Step(Time.unscaledDeltaTime, showing ? stiffness : stiffness * 1.4f, showing ? damping : damping * 1.3f);
            Apply();
            if (!showing && _show.value < 0.01f && !keepActive) gameObject.SetActive(false);
        }

        void Apply()
        {
            float v = _show.value;
            _group.alpha = Mathf.Clamp01(v * 1.4f);
            float scale = Mathf.LerpUnclamped(hiddenScale, 1f, v);
            _rect.localScale = new Vector3(scale, scale, 1f);
            _rect.anchoredPosition = _basePosition + hiddenOffset * (1f - v);
        }
    }
}
