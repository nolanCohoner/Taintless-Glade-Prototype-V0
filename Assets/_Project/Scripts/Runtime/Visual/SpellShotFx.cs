using System.Collections.Generic;
using FullOpaqueVFX;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Effet "Arcane Tracking Shot" (pack Full Opaque Spell) utilisé comme balle : l'effet part de la bouche, et son champ de force
    /// (ForceFieldTarget) suit la balle en vol, comme le fait le pack avec une cible. Les traînées convergent donc sur la tête du tir
    /// et finissent sur le point d'impact. Recyclé via un pool ; la couleur vient de SpitColor.
    /// </summary>
    public class SpellShotFx : MonoBehaviour
    {
        /// <summary>Durée (s) laissée à l'effet pour finir ses particules une fois la balle arrivée.</summary>
        const float TailTime = 2.5f;

        static readonly Dictionary<GameObject, Stack<SpellShotFx>> _pool = new Dictionary<GameObject, Stack<SpellShotFx>>();
        static Transform _holder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pool.Clear();
            _holder = null;
        }

        GameObject _prefab;
        Transform _field;
        Vector3 _fieldRest;
        ParticleSystem[] _systems;
        Transform _follow;
        float _until;
        bool _active;

        /// <summary>Joue l'effet depuis position / rotation ; son champ de force suit "follow" jusqu'à Detach.</summary>
        public static SpellShotFx Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale, Transform follow)
        {
            if (prefab == null) return null;
            if (!_pool.TryGetValue(prefab, out Stack<SpellShotFx> stack)) _pool[prefab] = stack = new Stack<SpellShotFx>();

            SpellShotFx fx = null;
            while (stack.Count > 0 && fx == null) fx = stack.Pop();
            if (fx == null) fx = Create(prefab);

            fx.transform.SetPositionAndRotation(position, rotation);
            fx.transform.localScale = prefab.transform.localScale * scale;
            fx.gameObject.SetActive(true);
            fx.Begin(follow);
            return fx;
        }

        /// <summary>Instancie le prefab dans un parent inactif : on retire le MainSpellManager du pack (il détruirait l'effet à la fin des particules) avant son activation.</summary>
        static SpellShotFx Create(GameObject prefab)
        {
            if (_holder == null)
            {
                var holder = new GameObject("SpellShotHolder");
                holder.SetActive(false);
                _holder = holder.transform;
            }

            GameObject go = Instantiate(prefab, _holder);
            var manager = go.GetComponent<MainSpellManager>();
            Transform field = manager != null && manager.forceFieldTarget != null ? manager.forceFieldTarget : go.transform.Find("ForceFieldTarget");
            if (manager != null) Destroy(manager);

            // L'impact du pack se joue à la cible : on garde celui du jeu (au point d'impact réel).
            Transform impact = go.transform.Find("Impact");
            if (impact != null) impact.gameObject.SetActive(false);

            var fx = go.AddComponent<SpellShotFx>();
            fx._prefab = prefab;
            fx._field = field;
            fx._fieldRest = field != null ? field.localPosition : Vector3.zero;
            fx._systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in fx._systems)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            go.transform.SetParent(null, true);
            go.SetActive(false);
            return fx;
        }

        void Begin(Transform follow)
        {
            _follow = follow;
            _active = true;
            _until = float.MaxValue;
            if (_field != null) _field.localPosition = _fieldRest;
            ApplyColor();
            foreach (ParticleSystem ps in _systems)
            {
                ps.Clear(true);
                ps.Play(false);
            }
        }

        /// <summary>La balle est arrivée en "point" : le champ de force s'y arrête, l'effet finit ses particules puis se range.</summary>
        public void Detach(Vector3 point)
        {
            if (!_active) return;
            if (_field != null && _follow != null) _field.position = point;
            _follow = null;
            _until = Time.time + TailTime;
        }

        void ApplyColor()
        {
            Color color = SpitColor.Current;
            foreach (ParticleSystem ps in _systems)
            {
                var main = ps.main;
                main.startColor = color;
            }
        }

        void Update()
        {
            if (!_active) return;
            if (_follow != null)
            {
                if (_field != null) _field.position = _follow.position;
            }
            else if (Time.time >= _until)
            {
                Recycle();
                return;
            }
            // Arc-en-ciel : les particules émises à chaque instant prennent la teinte du moment.
            if (SpitColor.Mode == SpitColorMode.Rainbow) ApplyColor();
        }

        void Recycle()
        {
            _active = false;
            gameObject.SetActive(false);
            if (_prefab != null && _pool.TryGetValue(_prefab, out Stack<SpellShotFx> stack)) stack.Push(this);
            else Destroy(gameObject);
        }
    }
}
