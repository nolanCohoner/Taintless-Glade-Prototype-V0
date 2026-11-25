using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Particules d'interface (sans ParticleSystem) : spores qui flottent en permanence et gerbes lors des clics.
    /// À placer sur un RectTransform plein écran, au-dessus du feuillage et sous les panneaux.
    /// </summary>
    public class UiParticles : MonoBehaviour
    {
        public static UiParticles Instance { get; private set; }

        [Min(0)] public float ambientPerSecond = 7f;
        public Color ambientColorA = new Color(0.8f, 1f, 0.55f, 0.55f);
        public Color ambientColorB = new Color(0.55f, 1f, 0.85f, 0.4f);
        public int poolSize = 140;

        class Particle
        {
            public Image image;
            public Vector2 position, velocity;
            public float life, maxLife, size, gravity, drag, wobble, wobbleSpeed, seed;
            public Color color;
            public bool alive;
        }

        readonly List<Particle> _pool = new List<Particle>();
        RectTransform _rect;
        float _ambientCarry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            _rect = transform as RectTransform;
            MenuTheme theme = MenuTheme.Instance;
            for (int i = 0; i < poolSize; i++)
            {
                Image image = MenuUi.Image("P" + i, transform, theme != null ? theme.softCircle : null, Color.white);
                image.gameObject.SetActive(false);
                _pool.Add(new Particle { image = image });
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Particle Take()
        {
            foreach (Particle p in _pool) if (!p.alive) return p;
            return null;
        }

        void Spawn(Vector2 position, Vector2 velocity, float size, float life, Color color, float gravity, float drag, float wobble)
        {
            Particle p = Take();
            if (p == null) return;
            p.alive = true;
            p.position = position;
            p.velocity = velocity;
            p.size = size;
            p.life = p.maxLife = life;
            p.color = color;
            p.gravity = gravity;
            p.drag = drag;
            p.wobble = wobble;
            p.wobbleSpeed = Random.Range(1.5f, 3.5f);
            p.seed = Random.value * 10f;
            p.image.gameObject.SetActive(true);
            p.image.rectTransform.sizeDelta = Vector2.one * size;
            p.image.rectTransform.anchoredPosition = position;
        }

        /// <summary>Gerbe de particules centrée sur un élément d'UI.</summary>
        public void BurstAt(RectTransform target, int count, Color color)
        {
            if (target == null) return;
            Vector3 world = target.TransformPoint(target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            BurstScreen(screen, count, color);
        }

        public void BurstScreen(Vector2 screenPoint, int count, Color color)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, null, out Vector2 local)) return;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float speed = Random.Range(180f, 620f);
                Color c = Color.Lerp(color, Color.white, Random.value * 0.4f);
                Spawn(local, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Random.Range(10f, 26f), Random.Range(0.35f, 0.75f), c, -420f, 3.2f, 0f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _ambientCarry += ambientPerSecond * dt;
            while (_ambientCarry >= 1f)
            {
                _ambientCarry -= 1f;
                Rect r = _rect.rect;
                Vector2 pos = new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));
                Color c = Color.Lerp(ambientColorA, ambientColorB, Random.value);
                Spawn(pos, new Vector2(Random.Range(-8f, 8f), Random.Range(10f, 34f)), Random.Range(5f, 14f), Random.Range(4f, 8f), c, 0f, 0f, Random.Range(8f, 26f));
            }

            foreach (Particle p in _pool)
            {
                if (!p.alive) continue;
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.alive = false;
                    p.image.gameObject.SetActive(false);
                    continue;
                }
                p.velocity.y += p.gravity * dt;
                p.velocity *= Mathf.Max(0f, 1f - p.drag * dt);
                p.position += p.velocity * dt;
                float t = 1f - p.life / p.maxLife;
                float sway = p.wobble > 0f ? Mathf.Sin(Time.unscaledTime * p.wobbleSpeed + p.seed) * p.wobble : 0f;
                p.image.rectTransform.anchoredPosition = p.position + new Vector2(sway, 0f);
                float fade = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                Color c = p.color;
                c.a *= p.wobble > 0f ? fade : 1f - t * t;
                p.image.color = c;
                p.image.rectTransform.localScale = Vector3.one * (p.wobble > 0f ? 1f : 1f - t * 0.7f);
            }
        }
    }
}
