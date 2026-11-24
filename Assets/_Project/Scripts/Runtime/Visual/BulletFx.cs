using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Effet de particules du pack Hovl utilisé comme balle : il suit le projectile en vol, puis arrête son émission
    /// à l'impact et finit ses particules avant de se ranger. Recyclé via un pool.
    /// Les particules sont émises en espace monde et l'effet regarde vers l'arrière : elles forment une traînée derrière la balle.
    /// </summary>
    public class BulletFx : MonoBehaviour
    {
        static readonly Dictionary<GameObject, Stack<BulletFx>> _pool = new Dictionary<GameObject, Stack<BulletFx>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _pool.Clear();

        GameObject _prefab;
        ParticleSystem[] _systems;
        float[] _baseSize, _baseSpeed;
        bool[] _world;
        readonly HashSet<Renderer> _muted = new HashSet<Renderer>();
        TrailRenderer[] _trails;
        float _tail;
        Transform _follow;
        float _until;
        bool _active;
        bool _reverse, _live;
        Vector3 _baseScale;

        /// <summary>Joue l'effet en position / rotation (la rotation est inversée : traînée derrière) en suivant "follow" jusqu'à Detach.</summary>
        public static BulletFx Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale, Transform follow, Color? tint, float sizeBoost = 1f, float speedFactor = 1f, bool reverse = true, bool liveSpitColor = false)
        {
            if (prefab == null) return null;
            if (!_pool.TryGetValue(prefab, out Stack<BulletFx> stack)) _pool[prefab] = stack = new Stack<BulletFx>();

            BulletFx fx = null;
            while (stack.Count > 0 && fx == null) fx = stack.Pop();
            if (fx == null) fx = Create(prefab, reverse);

            fx._reverse = reverse;
            fx._live = liveSpitColor;
            fx._baseScale = prefab.transform.localScale;
            fx.transform.SetPositionAndRotation(position, rotation * (reverse ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity));
            fx.transform.localScale = fx._baseScale * scale;
            fx.gameObject.SetActive(true);
            fx.Begin(follow, tint, sizeBoost, speedFactor);
            return fx;
        }

        static BulletFx Create(GameObject prefab, bool worldSpace)
        {
            GameObject go = Instantiate(prefab);
            go.SetActive(false);

            var fx = go.AddComponent<BulletFx>();
            fx._prefab = prefab;
            fx._systems = go.GetComponentsInChildren<ParticleSystem>(true);
            float longest = 0f;
            fx._baseSize = new float[fx._systems.Length];
            fx._baseSpeed = new float[fx._systems.Length];
            fx._world = new bool[fx._systems.Length];
            fx._trails = go.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < fx._systems.Length; i++)
            {
                ParticleSystem ps = fx._systems[i];
                var main = ps.main;
                fx._baseSize[i] = main.startSizeMultiplier;
                fx._baseSpeed[i] = main.startSpeedMultiplier;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                // Étincelles : en espace monde (traînée derrière la balle). Tête de balle : en espace local, elle reste collée au projectile.
                if (worldSpace) main.simulationSpace = ParticleSystemSimulationSpace.World;
                fx._world[i] = main.simulationSpace == ParticleSystemSimulationSpace.World;
                main.loop = true;
                float life = main.startLifetime.constantMax;
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoCurves || main.startLifetime.mode == ParticleSystemCurveMode.Curve)
                    life = main.startLifetime.curveMultiplier;
                longest = Mathf.Max(longest, life);
            }
            fx._tail = Mathf.Clamp(longest, 0.3f, 3f) + 0.1f;
            return fx;
        }

        /// <summary>sizeBoost agrandit les particules sans agrandir l'effet ; speedFactor réduit leur vitesse (traînée plus serrée).</summary>
        void Begin(Transform follow, Color? tint, float sizeBoost, float speedFactor)
        {
            _follow = follow;
            _active = true;
            _until = float.MaxValue;
            if (tint.HasValue) Vfx3D.Tint(gameObject, tint.Value);
            foreach (TrailRenderer tr in _trails)
            {
                tr.Clear();
                tr.emitting = true;
                if (tint.HasValue) TintTrail(tr, tint.Value);
            }
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem ps = _systems[i];
                var main = ps.main;
                main.startSizeMultiplier = _baseSize[i] * sizeBoost;
                main.startSpeedMultiplier = _baseSpeed[i] * speedFactor;
                var emission = ps.emission;
                emission.enabled = true;
                // Rend leur affichage aux sous-effets coupés (Drop / KeepOnly) lors d'un emploi précédent.
                var renderer = ps.GetComponent<Renderer>();
                if (renderer != null && _muted.Remove(renderer)) renderer.enabled = true;
                ps.Clear(false);
                ps.Play(false);
            }
        }

        /// <summary>La balle est arrivée en "point" : l'émission s'arrête, les particules déjà émises finissent, puis l'effet se range.</summary>
        public void Detach(Vector3 point)
        {
            if (!_active) return;
            transform.position = point;
            _follow = null;
            for (int i = 0; i < _systems.Length; i++)
                _systems[i].Stop(false, _world[i] ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (TrailRenderer tr in _trails) tr.emitting = false;
            _until = Time.time + _tail;
        }

        /// <summary>Coupe les sous-effets dont le nom contient l'un des mots (ex. "Smoke") : à appeler après Spawn.</summary>
        public void Drop(params string[] names)
        {
            foreach (ParticleSystem ps in _systems)
            {
                foreach (string n in names)
                {
                    if (ps.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Mute(ps);
                    break;
                }
            }
        }

        /// <summary>Ne garde que les sous-effets dont le nom est exactement l'un des mots : à appeler après Spawn.</summary>
        public void KeepOnly(params string[] names)
        {
            foreach (ParticleSystem ps in _systems)
            {
                bool keep = false;
                foreach (string n in names) keep |= string.Equals(ps.name, n, System.StringComparison.OrdinalIgnoreCase);
                if (!keep) Mute(ps);
            }
        }

        void Mute(ParticleSystem ps)
        {
            var emission = ps.emission;
            emission.enabled = false;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = ps.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                _muted.Add(renderer);
            }
        }

        static void TintTrail(TrailRenderer tr, Color c)
        {
            tr.startColor = c;
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        /// <summary>Change l'échelle de l'effet en cours de vie (charge du tir).</summary>
        public void SetScale(float scale) => transform.localScale = _baseScale * scale;

        void Update()
        {
            if (!_active) return;
            if (_follow != null)
            {
                transform.SetPositionAndRotation(_follow.position, _reverse ? _follow.rotation * Quaternion.Euler(0f, 180f, 0f) : _follow.rotation);
                // Arc-en-ciel : chaque particule émise prend la teinte du moment, la traînée se déroule en dégradé.
                if (_live)
                {
                    Color c = SpitColor.Current;
                    foreach (ParticleSystem ps in _systems)
                    {
                        var main = ps.main;
                        main.startColor = c;
                    }
                    foreach (TrailRenderer tr in _trails) TintTrail(tr, c);
                }
            }
            else if (Time.time >= _until) Recycle();
        }

        void Recycle()
        {
            _active = false;
            gameObject.SetActive(false);
            if (_prefab != null && _pool.TryGetValue(_prefab, out Stack<BulletFx> stack)) stack.Push(this);
            else Destroy(gameObject);
        }
    }
}
