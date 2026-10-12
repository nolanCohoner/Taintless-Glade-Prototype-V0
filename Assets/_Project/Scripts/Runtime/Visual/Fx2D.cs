using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaintlessGlade
{
    /// <summary>
    /// Effets 2D "dessinés à la main en code" dans le style du perso (contour noir épais, aplats clairs, ombrage cel) :
    /// boules, étoiles d'impact (impact frames), gouttelettes, orbe arc-en-ciel, bande du rayon. Aucune image ni particule Unity :
    /// toutes les formes sont générées une fois au premier usage. À remplacer plus tard par tes vrais dessins
    /// (il suffira de changer les sprites renvoyés par les propriétés ci-dessous).
    /// </summary>
    public static class Fx2D
    {
        public const int SortingOrder = 40;

        public static readonly Color[] Rainbow =
        {
            new Color(1f, 0.02f, 0.05f),
            new Color(0.96f, 0.59f, 0.06f),
            new Color(1f, 0.9f, 0f),
            new Color(0.1f, 1f, 0f),
            new Color(0.1f, 0.72f, 0.88f),
            new Color(0.72f, 0f, 1f),
        };

        /// <summary>Cyan clair du perso (crachat).</summary>
        public static readonly Color SpitColor = new Color(0.55f, 0.95f, 1f);

        // ------------------------------------------------------------------ Sprites (générés au premier accès)

        static Material _material;
        static Sprite _ball, _puff, _starFull, _starRing, _droplets, _orb;
        static Sprite[] _beamFrames;

        /// <summary>Matériau sprite non éclairé à sommets colorés (shader intégré de Unity, aucun asset à créer).</summary>
        public static Material Material
        {
            get
            {
                if (_material == null)
                {
                    Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return _material;
            }
        }

        /// <summary>Boule contourée, ombrage cel en bas à droite, reflet en haut à gauche (à teinter).</summary>
        public static Sprite Ball => _ball != null ? _ball : _ball = Make(96, 96, 96f, (x, y) =>
        {
            Vector2 p = Centered(x, y, 96, 96);
            float d = 0.5f - p.magnitude;
            float px = d * 96f;
            float shade = Vector2.Dot(p, new Vector2(0.55f, -0.55f)) > 0.12f ? 0.78f : 1f;
            bool shine = Mathf.Abs(p.magnitude - 0.3f) < 0.035f && p.x < -0.08f && p.y > 0.08f;
            float v = shine ? 1f : shade;
            return Shade(px, 6f, new Color(v, v, v));
        });

        /// <summary>Petite bulle contourée (traînée).</summary>
        public static Sprite Puff => _puff != null ? _puff : _puff = Make(48, 48, 96f, (x, y) =>
        {
            Vector2 p = Centered(x, y, 48, 48);
            return Shade((0.5f - p.magnitude) * 48f, 4f, Color.white);
        });

        /// <summary>Impact frame 1 : grosse étoile pleine, très contrastée (blanc + contour noir).</summary>
        public static Sprite StarFull => _starFull != null ? _starFull : _starFull = Make(160, 160, 128f, (x, y) =>
        {
            Vector2 p = Centered(x, y, 160, 160);
            float d = StarDistance(p, 9, 0.28f, 0.5f, 0f) * 160f;
            return Shade(d, 8f, Color.white);
        });

        /// <summary>Impact frame 2 : étoile creuse (anneau) qui s'ouvre.</summary>
        public static Sprite StarRing => _starRing != null ? _starRing : _starRing = Make(160, 160, 128f, (x, y) =>
        {
            Vector2 p = Centered(x, y, 160, 160);
            float outer = StarDistance(p, 9, 0.3f, 0.5f, 0.35f) * 160f;
            float hole = (p.magnitude - 0.24f) * 160f;
            return Shade(Mathf.Min(outer, hole), 7f, Color.white);
        });

        /// <summary>Impact frame 3 : gouttelettes éclatées autour du point d'impact.</summary>
        public static Sprite Droplets => _droplets != null ? _droplets : _droplets = Make(160, 160, 128f, (x, y) =>
        {
            Vector2 p = Centered(x, y, 160, 160);
            float best = -1f;
            const int n = 9;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f * (i % 2)) * Mathf.PI * 2f / n;
                float ring = i % 2 == 0 ? 0.36f : 0.26f;
                float radius = i % 3 == 0 ? 0.075f : 0.05f;
                var c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                best = Mathf.Max(best, radius - Vector2.Distance(p, c));
            }
            return Shade(best * 160f, 5f, Color.white);
        });

        /// <summary>Orbe arc-en-ciel : anneaux concentriques aux couleurs du perso, contour noir, reflet. Non teinté (couleurs fixes).</summary>
        public static Sprite RainbowOrb => _orb != null ? _orb : _orb = Make(128, 128, 128f, (x, y) =>
        {
            Vector2 p = Centered(x, y, 128, 128);
            float r = p.magnitude;
            float px = (0.5f - r) * 128f;
            int band = Mathf.Clamp((int)((0.5f - r) / 0.5f * Rainbow.Length * 1.0f), 0, Rainbow.Length - 1);
            // Anneau extérieur = rouge, centre = violet.
            Color c = Rainbow[band];
            bool shine = Vector2.Distance(p, new Vector2(-0.17f, 0.18f)) < 0.07f;
            if (shine) c = Color.white;
            return Shade(px, 5f, c);
        });

        /// <summary>4 images de la bande du rayon (ondulation différente) : à jouer à ~12 i/s comme une vraie animation image par image.</summary>
        public static Sprite[] BeamFrames
        {
            get
            {
                if (_beamFrames != null && _beamFrames.Length > 0 && _beamFrames[0] != null) return _beamFrames;
                _beamFrames = new Sprite[4];
                for (int f = 0; f < _beamFrames.Length; f++)
                    _beamFrames[f] = MakeBeamFrame(f);
                return _beamFrames;
            }
        }

        public const int BeamHeight = 112; // 6 bandes de 16 px + contour de 8 px de chaque côté
        public const int BeamCap = 56;     // demi-cercle de chaque extrémité (= BeamHeight / 2)

        static Sprite MakeBeamFrame(int frame)
        {
            const int w = 256, h = BeamHeight;
            float phase = frame * Mathf.PI * 0.5f;
            var sprite = Make(w, h, h, (x, y) =>
            {
                float wave = Mathf.Sin(x * 0.07f + phase) * 4f + Mathf.Sin(x * 0.19f - phase * 1.7f) * 1.6f;
                float yc = y - h * 0.5f + 0.5f - wave * 0.5f;

                // Extrémités arrondies (demi-cercles), le milieu est étiré par le mode "Sliced".
                float capX = x < BeamCap ? BeamCap - x : (x > w - BeamCap ? x - (w - BeamCap) : 0f);
                float half = h * 0.5f;
                float capScale = capX > 0f ? Mathf.Sqrt(Mathf.Max(0f, 1f - (capX * capX) / (BeamCap * (float)BeamCap))) : 1f;
                float edge = half * capScale - Mathf.Abs(yc);

                float t = Mathf.Clamp01((yc / Mathf.Max(capScale, 0.001f) + 48f) / 96f);
                int band = Mathf.Clamp((int)(t * Rainbow.Length), 0, Rainbow.Length - 1);
                Color c = Rainbow[band];
                // Reflet blanc sur la bande du haut.
                float shineY = (yc / Mathf.Max(capScale, 0.001f) + 48f);
                if (shineY > 80f && shineY < 86f && x > 14 && x < w - 14) c = Color.Lerp(c, Color.white, 0.75f);
                return Shade(edge, 7f, c);
            }, new Vector4(BeamCap, 0f, BeamCap, 0f));
            return sprite;
        }

        // ------------------------------------------------------------------ Génération

        static Sprite Make(int w, int h, float ppu, Func<int, int, Color> pixel, Vector4? border = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = pixel(x, y);
            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite s = border.HasValue
                ? Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border.Value)
                : Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Vector2 Centered(int x, int y, int w, int h) => new Vector2((x + 0.5f) / w - 0.5f, (y + 0.5f) / h - 0.5f);

        /// <summary>d = distance (px) à l'intérieur de la forme (négatif dehors) : contour noir de "outline" px, puis aplat.</summary>
        static Color Shade(float d, float outline, Color fill)
        {
            float alpha = Mathf.Clamp01(d + 0.5f);
            Color c = Color.Lerp(Color.black, fill, Mathf.Clamp01(d - outline + 0.5f));
            c.a = alpha;
            return c;
        }

        /// <summary>Distance approchée (en fraction de la taille) à une étoile à "spikes" branches ; "twist" tourne les branches.</summary>
        static float StarDistance(Vector2 p, int spikes, float inner, float outer, float twist)
        {
            float angle = Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f) * spikes + twist;
            float tri = 1f - Mathf.Abs(2f * (angle - Mathf.Floor(angle)) - 1f);
            float radius = Mathf.Lerp(inner, outer, tri);
            return (radius - p.magnitude) * 0.75f;
        }

        // ------------------------------------------------------------------ Création d'effets

        public static SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, Color color, int order = SortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sharedMaterial = Material;
            sr.sortingOrder = order;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        /// <summary>Impact en 3 "frames" : étoile blanche pleine (flash), étoile creuse colorée, gouttelettes qui s'éparpillent.</summary>
        public static void SpawnImpact(Vector3 position, Color tint, float size = 1f)
        {
            var effect = NewEffect("Impact2D", position);
            float roll = UnityEngine.Random.Range(0f, 360f);
            effect.Play(roll, size, new[]
            {
                new Fx2DEffect.Frame { sprite = StarFull, color = Color.white, duration = 0.045f, scaleFrom = 0.8f, scaleTo = 1.05f },
                new Fx2DEffect.Frame { sprite = StarRing, color = tint, duration = 0.06f, scaleFrom = 1.1f, scaleTo = 1.35f },
                new Fx2DEffect.Frame { sprite = Droplets, color = tint, duration = 0.11f, scaleFrom = 1.2f, scaleTo = 1.8f, fadeOut = true },
            });
        }

        /// <summary>Petit éclat au bout du museau / de l'arc quand une boule part (2 frames).</summary>
        public static void SpawnMuzzle(Vector3 position, Color tint, float size = 0.5f)
        {
            var effect = NewEffect("Muzzle2D", position);
            effect.Play(UnityEngine.Random.Range(0f, 360f), size, new[]
            {
                new Fx2DEffect.Frame { sprite = StarFull, color = Color.white, duration = 0.04f, scaleFrom = 0.7f, scaleTo = 1f },
                new Fx2DEffect.Frame { sprite = Droplets, color = tint, duration = 0.08f, scaleFrom = 0.9f, scaleTo = 1.4f, fadeOut = true },
            });
        }

        /// <summary>Petite bulle qui rétrécit (traînée de la boule).</summary>
        public static void SpawnPuff(Vector3 position, Color tint, float size, float life)
        {
            var effect = NewEffect("Puff2D", position);
            effect.Play(0f, size, new[]
            {
                new Fx2DEffect.Frame { sprite = Puff, color = tint, duration = life, scaleFrom = 1f, scaleTo = 0.1f, fadeOut = false },
            });
        }

        static Fx2DEffect NewEffect(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<Billboard>();
            return go.AddComponent<Fx2DEffect>();
        }
    }

    /// <summary>Joue quelques sprites à la suite (flipbook) face caméra, puis se détruit.</summary>
    public class Fx2DEffect : MonoBehaviour
    {
        public struct Frame
        {
            public Sprite sprite;
            public Color color;
            public float duration;
            public float scaleFrom, scaleTo;
            public bool fadeOut;
        }

        Frame[] _frames;
        SpriteRenderer _renderer;
        float _size, _time;
        int _index;

        public void Play(float roll, float size, Frame[] frames)
        {
            _frames = frames;
            _size = size;
            _renderer = Fx2D.CreateRenderer("Frame", transform, frames[0].sprite, frames[0].color);
            _renderer.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            Apply(0f);
        }

        void Update()
        {
            if (_frames == null) return;
            _time += Time.deltaTime;
            while (_index < _frames.Length && _time >= _frames[_index].duration)
            {
                _time -= _frames[_index].duration;
                _index++;
            }
            if (_index >= _frames.Length) { Destroy(gameObject); return; }
            Apply(_time / Mathf.Max(_frames[_index].duration, 0.0001f));
        }

        void Apply(float k)
        {
            Frame f = _frames[Mathf.Min(_index, _frames.Length - 1)];
            _renderer.sprite = f.sprite;
            Color c = f.color;
            if (f.fadeOut) c.a *= 1f - k * k;
            _renderer.color = c;
            _renderer.transform.localScale = Vector3.one * (Mathf.Lerp(f.scaleFrom, f.scaleTo, k) * _size);
        }
    }
}
