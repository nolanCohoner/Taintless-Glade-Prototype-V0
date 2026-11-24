using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>Utilitaires pour instancier / arrêter proprement les effets de particules 3D.</summary>
    public static class Vfx3D
    {
        static Vfx3DLibrary _library;
        static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _library = null;
            _loaded = false;
            _pool.Clear();
            _poolTinted.Clear();
            _flashPool.Clear();
            _flashActive = 0;
        }

        public static Vfx3DLibrary Library
        {
            get
            {
                if (!_loaded)
                {
                    _library = Resources.Load<Vfx3DLibrary>("Vfx3DLibrary");
                    _loaded = true;
                }
                return _library;
            }
        }

        public static bool Available => Library != null;

        /// <summary>Instancie l'effet (échelle uniforme appliquée à toute la hiérarchie de particules).</summary>
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, Transform parent = null)
        {
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, position, rotation, parent);
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            return go;
        }

        /// <summary>Remplace les couleurs de l'effet par une teinte unique (dégradé de vie : même teinte qui s'efface).</summary>
        public static void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = color;
                var col = ps.colorOverLifetime;
                if (col.enabled) col.color = fade;
                var trails = ps.trails;
                if (trails.enabled)
                {
                    trails.inheritParticleColor = true;
                    trails.colorOverLifetime = fade;
                    trails.colorOverTrail = fade;
                }
            }
        }

        /// <summary>Bleu des tirs de base (le crachat de l'Axolotl utilise SpitColor).</summary>
        public static readonly Color Blue = new Color(0.35f, 0.8f, 1f, 1f);

        // Deux pools par prefab : une instance teintée ne doit pas être réutilisée telle quelle sans teinte.
        static readonly Dictionary<GameObject, Stack<GameObject>> _pool = new Dictionary<GameObject, Stack<GameObject>>();
        static readonly Dictionary<GameObject, Stack<GameObject>> _poolTinted = new Dictionary<GameObject, Stack<GameObject>>();

        /// <summary>Tire-et-oublie : joue l'effet (recyclé via un pool, sans Instantiate/Destroy à chaque tir) puis le range.</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, Color? tint = null)
        {
            if (prefab == null) return null;
            var pools = tint.HasValue ? _poolTinted : _pool;
            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack)) pools[prefab] = stack = new Stack<GameObject>();

            GameObject go = null;
            while (stack.Count > 0 && go == null) go = stack.Pop();

            PooledFx fx;
            if (go == null)
            {
                go = Instantiate(prefab, position, rotation, scale);
                fx = go.AddComponent<PooledFx>();
                fx.prefab = prefab;
                fx.tinted = tint.HasValue;
                fx.life = Lifetime(go);
            }
            else
            {
                fx = go.GetComponent<PooledFx>();
                go.transform.SetPositionAndRotation(position, rotation);
                go.transform.localScale = prefab.transform.localScale * scale;
                go.SetActive(true);
            }

            if (tint.HasValue) Tint(go, tint.Value);
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(false);
                ps.Play(false);
                var main = ps.main;
                main.loop = false;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            fx.Arm();
            return go;
        }

        static void Recycle(PooledFx fx)
        {
            if (fx.prefab != null && (fx.tinted ? _poolTinted : _pool).TryGetValue(fx.prefab, out Stack<GameObject> stack)) stack.Push(fx.gameObject);
            else Object.Destroy(fx.gameObject);
        }

        static float Lifetime(GameObject go)
        {
            float longest = 0f;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float life = main.startLifetime.constantMax;
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoCurves || main.startLifetime.mode == ParticleSystemCurveMode.Curve)
                    life = main.startLifetime.curveMultiplier;
                longest = Mathf.Max(longest, main.duration + main.startDelay.constantMax + life);
            }
            return Mathf.Clamp(longest, 0.5f, 6f) + 0.25f;
        }

        class PooledFx : MonoBehaviour
        {
            public GameObject prefab;
            public float life;
            public bool tinted;
            float _until;

            public void Arm()
            {
                _until = Time.time + life;
                enabled = true;
            }

            void Update()
            {
                if (Time.time < _until) return;
                enabled = false;
                gameObject.SetActive(false);
                Recycle(this);
            }
        }

        /// <summary>Coupe l'émission, laisse finir les particules déjà en vol, puis détruit.</summary>
        public static void StopAndDestroy(GameObject go, bool detach = false)
        {
            if (go == null) return;
            if (detach) go.transform.SetParent(null, true);
            float longest = 0f;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                float life = main.startLifetime.constantMax;
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoCurves || main.startLifetime.mode == ParticleSystemCurveMode.Curve)
                    life = main.startLifetime.curveMultiplier;
                longest = Mathf.Max(longest, main.duration + main.startDelay.constantMax + life);
            }
            Object.Destroy(go, Mathf.Clamp(longest, 0.5f, 6f) + 0.25f);
        }

        /// <summary>Active / coupe l'émission d'un effet persistant sans le détruire.</summary>
        public static void SetEmitting(GameObject go, bool emitting)
        {
            if (go == null) return;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;
                if (emission.enabled == emitting) continue;
                emission.enabled = emitting;
                if (emitting && !ps.isPlaying) ps.Play(false);
            }
        }

        const int MaxFlashes = 6;
        static readonly Stack<LightFade> _flashPool = new Stack<LightFade>();
        static int _flashActive;

        /// <summary>Flash de lumière ponctuelle éphémère. Les lumières sont recyclées et plafonnées pour ne pas saturer URP en tir rapide.</summary>
        public static void Flash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            if (_flashActive >= MaxFlashes) return;
            LightFade fade = null;
            while (_flashPool.Count > 0 && fade == null) fade = _flashPool.Pop();
            if (fade == null)
            {
                var go = new GameObject("VfxFlash");
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                fade = go.AddComponent<LightFade>();
                fade.Light = l;
            }
            fade.transform.position = position;
            fade.Light.color = color;
            fade.Light.intensity = intensity;
            fade.Light.range = range;
            fade.gameObject.SetActive(true);
            fade.Begin(duration);
            _flashActive++;
        }

        class LightFade : MonoBehaviour
        {
            public Light Light;
            float _duration, _time, _start;
            bool _running;

            public void Begin(float duration)
            {
                _duration = Mathf.Max(0.01f, duration);
                _time = 0f;
                _start = Light.intensity;
                _running = true;
            }

            void Update()
            {
                _time += Time.deltaTime;
                float k = _time / _duration;
                if (k >= 1f)
                {
                    _running = false;
                    _flashActive--;
                    gameObject.SetActive(false);
                    _flashPool.Push(this);
                    return;
                }
                Light.intensity = _start * (1f - k) * (1f - k);
            }

            void OnDestroy()
            {
                if (_running) _flashActive--;
            }
        }
    }
}
