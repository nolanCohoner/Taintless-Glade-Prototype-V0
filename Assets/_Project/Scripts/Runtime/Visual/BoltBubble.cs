using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Cœur de la balle du crachat : la bulle de l'arme (Cracha Arc en ciel bulle 1), face à la caméra.
    /// La bulle garde ses couleurs d'origine ; une deuxième copie additive par-dessus défile en arc-en-ciel (SpitColor).
    /// Un tir chargé projette en plus l'anneau arc-en-ciel de l'arme (Cracha Arc en ciel Fond) sur le sol, sous la balle.
    /// </summary>
    public class BoltBubble : MonoBehaviour
    {
        /// <summary>Centres des dessins (px, origine en haut à gauche), comme dans SpitWeaponInstaller.</summary>
        static readonly Vector2 BubbleCenterPx = new Vector2(433f, 329f);
        static readonly Vector2 RingCenterPx = new Vector2(423f, 400f);

        const float PopDuration = 0.22f;
        const float RingMinPower = 0.12f;

        static Material _baseMaterial, _glowMaterial;

        SpriteRenderer _base, _glow, _ring;
        Transform _art, _ringTransform, _parent;
        float _size = 1f, _phase, _pop, _power, _spin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _baseMaterial = _glowMaterial = null;

        static Material Make(bool additive)
        {
            Shader shader = Shader.Find("TaintlessGlade/Simple Particle");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.renderQueue = additive ? 3010 : 3005;
            return m;
        }

        /// <summary>Le sprite est centré sur le canevas : on le décale pour que le centre du dessin tombe sur le pivot.</summary>
        static Vector3 CenterOffset(Sprite sprite, Vector2 centerPx)
        {
            float ppu = sprite.pixelsPerUnit;
            Rect r = sprite.rect;
            return new Vector3(-(centerPx.x - r.width * 0.5f) / ppu, -(r.height * 0.5f - centerPx.y) / ppu, 0f);
        }

        /// <summary>Crée la bulle comme enfant de "parent". Retourne null si le sprite est absent. ringSprite est optionnel.</summary>
        public static BoltBubble Create(Transform parent, Sprite sprite, Sprite ringSprite)
        {
            if (sprite == null) return null;
            if (_baseMaterial == null) _baseMaterial = Make(false);
            if (_glowMaterial == null) _glowMaterial = Make(true);

            var go = new GameObject("BoltBubble");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.1f;
            var bubble = go.AddComponent<BoltBubble>();
            bubble._parent = parent;

            bubble._art = new GameObject("Art").transform;
            bubble._art.SetParent(go.transform, false);
            bubble._art.localPosition = CenterOffset(sprite, BubbleCenterPx);
            bubble._base = bubble.AddRenderer("Base", bubble._art, sprite, _baseMaterial, 0);
            bubble._glow = bubble.AddRenderer("RainbowOverlay", bubble._art, sprite, _glowMaterial, 1);

            if (ringSprite != null)
            {
                // Anneau posé à plat sur le sol : enfant du projectile mais orienté en monde dans LateUpdate.
                bubble._ringTransform = new GameObject("BoltRing").transform;
                bubble._ringTransform.SetParent(parent, false);
                var ringArt = new GameObject("Art").transform;
                ringArt.SetParent(bubble._ringTransform, false);
                ringArt.localPosition = CenterOffset(ringSprite, RingCenterPx);
                bubble._ring = bubble.AddRenderer("Ring", ringArt, ringSprite, _glowMaterial, -1);
                bubble._ringTransform.gameObject.SetActive(false);
            }

            go.SetActive(false);
            return bubble;
        }

        SpriteRenderer AddRenderer(string name, Transform parent, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = 20 + order;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        /// <summary>power : 0 = tir normal, 0..1 = niveau de charge (l'anneau au sol n'apparaît que pour un tir chargé).</summary>
        public void Show(float size, float power)
        {
            _size = size;
            _power = power;
            _pop = 0f;
            _phase = Random.value * 6.28f;
            gameObject.SetActive(true);
            if (_ringTransform != null) _ringTransform.gameObject.SetActive(power >= RingMinPower);
            Apply(0f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (_ringTransform != null) _ringTransform.gameObject.SetActive(false);
        }

        /// <summary>Écrasement-étirement bref : rebond sur un mur, traversée d'un ennemi.</summary>
        public void Pop() => _pop = 1f;

        void LateUpdate() => Apply(Time.deltaTime);

        void Apply(float dt)
        {
            Camera cam = Camera.main;
            // Face à la caméra, avec un léger balancement pour que la bulle paraisse vivante.
            float t = Time.time * 7f + _phase;
            if (cam != null) transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t) * 9f);

            _pop = Mathf.Max(0f, _pop - dt / PopDuration);
            float pulse = 1f + Mathf.Sin(t * 1.7f) * 0.05f;
            transform.localScale = new Vector3(1f + 0.45f * _pop, 1f - 0.3f * _pop, 1f) * (_size * pulse * (1f + 0.15f * _pop));

            Color rainbow = SpitColor.Current;
            _base.color = Color.Lerp(Color.white, rainbow, 0.2f);
            Color overlay = rainbow;
            overlay.a = 0.55f;
            _glow.color = overlay;

            if (_ringTransform != null && _ringTransform.gameObject.activeSelf)
            {
                _spin += dt * 120f;
                Vector3 p = _parent.position;
                _ringTransform.position = new Vector3(p.x, p.y - 0.46f, p.z);
                _ringTransform.rotation = Quaternion.Euler(0f, _spin, 0f) * Quaternion.Euler(90f, 0f, 0f);
                float ringScale = Mathf.Lerp(0.9f, 2.2f, _power) * (1f + Mathf.Sin(t * 1.3f) * 0.06f);
                _ringTransform.localScale = Vector3.one * ringScale;
                Color ring = SpitColor.Current;
                ring.a = Mathf.Lerp(0.45f, 0.9f, _power);
                _ring.color = ring;
            }
        }
    }
}
