using System;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Le « buisson » 2D qui cache les bords de l'écran : plusieurs calques de feuillage noir (images générées par script)
    /// qui entrent en jeu avec un rebond, ondulent en continu, frémissent au moindre événement et se retirent
    /// pour laisser de la place aux sous-menus. « Fermer » les rabat sur l'écran pour masquer un changement de scène.
    /// </summary>
    public class FoliageFrame : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public RectTransform rect;
            [Tooltip("Direction (vers le coin d'origine) d'où le calque arrive et où il se retire.")]
            public Vector2 outward = new Vector2(1f, 1f);
            [Range(0f, 1.5f)]
            [Tooltip("0 = calque du fond (lent), 1 = calque de devant (nerveux).")]
            public float depth = 0.5f;
            [Tooltip("Retard d'entrée (s).")] public float introDelay;
            [HideInInspector] public Spring intro;
            [HideInInspector] public Vector2 basePosition;
            [HideInInspector] public float seed;
        }

        public Layer[] layers = Array.Empty<Layer>();
        [Tooltip("Distance parcourue à l'entrée / au retrait complet (px).")]
        public float travel = 900f;
        public float swayDegrees = 0.9f;
        public float swaySpeed = 0.55f;
        [Tooltip("Valeur de retrait par défaut (0 = menu principal).")]
        [Range(0f, 1f)] public float openAmount;

        Spring _open;
        Spring _rustle;
        Spring _close;
        float _timer;
        bool _introStarted;

        void Awake()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                Layer l = layers[i];
                if (l.rect == null) continue;
                l.basePosition = l.rect.anchoredPosition;
                l.seed = i * 3.17f;
                l.intro.Snap(0f);
            }
            _open.Snap(openAmount);
        }

        void OnEnable() => PlayIntro();

        public void PlayIntro()
        {
            _timer = 0f;
            _introStarted = true;
            foreach (Layer l in layers) l.intro.Snap(0f);
            _rustle.velocity += 40f;
        }

        /// <summary>Retire le feuillage (0 = écran presque fermé, 1 = très dégagé) avec un rebond.</summary>
        public void SetOpen(float amount)
        {
            _open.target = amount;
            _rustle.velocity += 22f;
        }

        /// <summary>Frémissement du feuillage (clic, changement de menu...).</summary>
        public void Rustle(float strength = 1f) => _rustle.velocity += 30f * strength;

        /// <summary>Rabat le feuillage vers le centre (transition de scène).</summary>
        public void Close() { _close.target = 1f; _rustle.velocity += 50f; }

        public void Reopen() => _close.target = 0f;

        public bool IsClosed => _close.target > 0.5f && _close.value > 0.92f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _timer += dt;
            _open.Step(dt, 140f, 10f);
            _rustle.Step(dt, 90f, 4.5f);
            _close.Step(dt, 120f, 15f);

            foreach (Layer l in layers)
            {
                if (l.rect == null) continue;
                if (_introStarted && _timer >= l.introDelay) l.intro.target = 1f;
                l.intro.Step(dt, 150f + 120f * l.depth, 9f + 3f * l.depth);

                float away = (1f - l.intro.value) * travel + _open.value * travel * (0.35f + 0.4f * l.depth);
                Vector2 dir = l.outward.normalized;
                Vector2 pos = l.basePosition + dir * away - dir * _close.value * 120f * (0.5f + l.depth);

                float t = Time.unscaledTime * swaySpeed * (0.6f + l.depth) + l.seed;
                float sway = (Mathf.PerlinNoise(t, l.seed) - 0.5f) * 2f * swayDegrees * (0.4f + l.depth);
                float rustle = _rustle.value * 0.12f * (0.4f + l.depth) * (Mathf.Sin(l.seed) >= 0f ? 1f : -1f);
                float breath = 1f + Mathf.Sin(t * 1.3f) * 0.006f * (1f + l.depth) + _close.value * (0.45f + 0.25f * l.depth);

                l.rect.anchoredPosition = pos;
                l.rect.localRotation = Quaternion.Euler(0f, 0f, sway + rustle);
                l.rect.localScale = new Vector3(breath, breath, 1f);
            }
        }
    }
}
