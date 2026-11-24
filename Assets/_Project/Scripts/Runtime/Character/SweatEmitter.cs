using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Gouttes de sueur 2D autour du crâne quand le perso court vite ou dash (même rendu 2D que le perso : sprites).
    /// Le sprite de goutte est dessiné en code (contour noir + remplissage clair) : aucun fichier image.
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public class SweatEmitter : MonoBehaviour
    {
        public Material material;
        [Tooltip("Centre du crâne par rapport au pivot de la tête (référentiel de la tête).")]
        public Vector2 skullCenter = new Vector2(0f, 0.33f);
        public Vector2 skullRadius = new Vector2(0.5f, 0.4f);
        public float lifetime = 0.55f;
        public float gravity = 2.6f;
        public float dropScale = 0.3f;
        [Tooltip("Effet 3D (Free Quick Effects) à la place des sprites 2D si la bibliothèque existe.")]
        public bool use3D = true;
        public float sweat3DScale = 1f;

        class Drop
        {
            public Transform t;
            public Vector2 velocity;
            public float age;
            public float life;
        }

        static Sprite _sprite;

        CharacterAnimator _animator;
        Transform _container;
        readonly List<Drop> _active = new List<Drop>();
        readonly Stack<Drop> _pool = new Stack<Drop>();
        float _spawnDebt;
        GameObject _fx3D;
        ParticleSystem[] _fxSystems;

        /// <summary>Transforme la pluie du pack en grosses gouttes qui giclent du crâne puis retombent (appelé aussi par l'outil de prévisualisation).</summary>
        public static void Configure3D(GameObject fx, Vector2 skullRadius, Material dropMaterial = null)
        {
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (dropMaterial != null && renderer != null)
                {
                    // Gouttes rondes étirées dans le sens de la chute, avec la texture ronde du pack.
                    renderer.sharedMaterial = dropMaterial;
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 1.8f;
                    renderer.velocityScale = 0.08f;
                    renderer.cameraVelocityScale = 0f;
                }
                var main = ps.main;
                main.loop = true;
                main.startColor = new Color(1f, 1f, 1f, 1f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                main.startSize3D = false;
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
                main.gravityModifier = 1.1f;
                main.maxParticles = 200;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = skullRadius.x;
                shape.rotation = new Vector3(-90f, 0f, 0f); // hémisphère tournée vers le haut
                shape.scale = new Vector3(1f, skullRadius.y / skullRadius.x, 1f);
                shape.position = Vector3.zero;

                var vel = ps.velocityOverLifetime;
                vel.enabled = false;
                var force = ps.forceOverLifetime;
                force.enabled = false;
                var emission = ps.emission;
                emission.rateOverTime = 0f;
            }
        }

        void Awake()
        {
            _animator = GetComponent<CharacterAnimator>();
            _container = new GameObject("Sweat").transform;
            _container.SetParent(transform.parent != null ? transform.parent : transform, false);

            if (use3D && Vfx3D.Available && Vfx3D.Library.sweat != null)
            {
                _fx3D = Vfx3D.Instantiate(Vfx3D.Library.sweat, transform.position, Quaternion.identity, sweat3DScale, _container);
                Configure3D(_fx3D, skullRadius, Vfx3D.Library.sweatDropMaterial);
                _fxSystems = _fx3D.GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        void OnDestroy()
        {
            if (_fx3D != null) Destroy(_fx3D);
        }

        void Update()
        {
            CharacterRig rig = _animator.rig;
            RigAnimationProfile p = _animator.profile;
            if (rig == null || rig.head == null || p == null) return;

            float dt = Time.deltaTime;
            float rate = _animator.IsDashingVisual ? p.sweatDashDrops : p.sweatRunDrops * _animator.Exertion;

            if (_fx3D != null)
            {
                _fx3D.transform.position = rig.head.TransformPoint(skullCenter);
                foreach (ParticleSystem ps in _fxSystems)
                {
                    var emission = ps.emission;
                    emission.rateOverTime = rate;
                    if (rate > 0.01f && !ps.isPlaying) ps.Play(false);
                }
                return;
            }
            _spawnDebt += rate * dt;
            while (_spawnDebt >= 1f)
            {
                _spawnDebt -= 1f;
                Spawn(rig.head);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Drop d = _active[i];
                d.age += dt;
                if (d.age >= d.life)
                {
                    d.t.gameObject.SetActive(false);
                    _pool.Push(d);
                    _active.RemoveAt(i);
                    continue;
                }

                d.velocity.y -= gravity * dt;
                d.t.localPosition += (Vector3)(d.velocity * dt);
                float k = d.age / d.life;
                float grow = Mathf.Min(1f, k * 8f + 0.3f);
                float shrink = k > 0.65f ? Mathf.Lerp(1f, 0f, (k - 0.65f) / 0.35f) : 1f;
                d.t.localScale = Vector3.one * (dropScale * grow * shrink);
            }
        }

        void Spawn(Transform head)
        {
            Drop d = _pool.Count > 0 ? _pool.Pop() : CreateDrop();
            // Plutôt sur le haut et les côtés du crâne.
            float angle = Random.Range(10f, 170f) * Mathf.Deg2Rad;
            var outward = new Vector2(Mathf.Cos(angle) * skullRadius.x, Mathf.Sin(angle) * skullRadius.y);
            Vector3 center = _container.InverseTransformPoint(head.TransformPoint(skullCenter));
            d.t.localPosition = center + (Vector3)outward;
            d.velocity = outward.normalized * Random.Range(0.5f, 1.1f) + Vector2.up * 0.5f;
            d.age = 0f;
            d.life = lifetime * Random.Range(0.8f, 1.2f);
            d.t.localScale = Vector3.one * dropScale * 0.3f;
            d.t.gameObject.SetActive(true);
            _active.Add(d);
        }

        Drop CreateDrop()
        {
            var go = new GameObject("SweatDrop");
            go.transform.SetParent(_container, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite();
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = 20;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return new Drop { t = go.transform };
        }

        /// <summary>Goutte (pointe en haut) : contour noir épais, remplissage cyan clair, reflet blanc.</summary>
        static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;

            const int w = 48, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            var outline = new Color32(0, 0, 0, 255);
            var fill = new Color32(150, 240, 255, 255);
            var shine = new Color32(255, 255, 255, 255);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = DropDistance(x + 0.5f, y + 0.5f); // < 0 = dedans
                    Color32 c = clear;
                    if (d < 0f)
                    {
                        c = d > -5f ? outline : fill;
                        if (d <= -5f && Mathf.Abs(x + 0.5f - 17f) < 3f && y > 12 && y < 26) c = shine;
                    }
                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _sprite.hideFlags = HideFlags.HideAndDontSave;
            return _sprite;
        }

        // Distance signée approximative à une goutte : cercle en bas, pointe vers le haut.
        static float DropDistance(float x, float y)
        {
            const float cx = 24f, cy = 22f, r = 19f, apexY = 62f;
            float circle = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
            if (y < cy || y > apexY) return circle;
            float t = (y - cy) / (apexY - cy);
            float halfWidth = r * Mathf.Pow(1f - t, 1.5f);
            return Mathf.Min(circle, Mathf.Abs(x - cx) - halfWidth);
        }
    }
}
