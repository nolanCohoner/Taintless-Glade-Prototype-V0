using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Dash en "téléportation" courte : éclat cyan au départ, le joueur file en laissant une traînée d'étincelles cyan,
    /// éclat à l'arrivée. Ajouté automatiquement par le PlayerController ; ne change pas les contrôles du dash.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class DashVfx : MonoBehaviour
    {
        public float startScale = 0.9f;
        public float trailScale = 0.7f;
        public float endScale = 1.1f;
        public float trailHeight = 0.5f;
        [Tooltip("Lumière cyan qui accompagne le dash.")]
        public float lightIntensity = 3.5f;

        static readonly Color Cyan = new Color(0.2f, 0.95f, 1f, 1f);

        PlayerController _controller;
        Transform _anchor;
        Light _light;
        readonly List<BulletFx> _trail = new List<BulletFx>(3);
        float _lightTimer;

        void Awake() => _controller = GetComponent<PlayerController>();

        void OnEnable()
        {
            _controller.DashStarted += OnStart;
            _controller.DashEnded += OnEnd;
        }

        void OnDisable()
        {
            _controller.DashStarted -= OnStart;
            _controller.DashEnded -= OnEnd;
        }

        bool HasVfx => Vfx3D.Available && Vfx3D.Library.dashTrail != null && Vfx3D.Library.dashTrail.Length > 0;

        Vector3 Center => transform.position + Vector3.up * trailHeight;

        void OnStart()
        {
            if (!HasVfx)
            {
                if (_controller.dashParticles != null) _controller.dashParticles.Play();
                return;
            }

            Vfx3DLibrary lib = Vfx3D.Library;
            Vector3 dir = _controller.DashDirection;
            Quaternion rot = Quaternion.LookRotation(dir);

            // Départ : le joueur "disparaît" dans un éclat cyan (le souffle part vers l'arrière).
            foreach (GameObject fx in lib.dashStart) Vfx3D.Spawn(fx, Center, Quaternion.LookRotation(-dir), startScale, Cyan);
            Vfx3D.Flash(Center, Cyan, lightIntensity, 4f, 0.15f);

            if (_anchor == null)
            {
                _anchor = new GameObject("DashAnchor").transform;
                _light = _anchor.gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
                _light.color = Cyan;
                _light.range = 4f;
            }
            _anchor.SetPositionAndRotation(Center, rot);
            _light.intensity = lightIntensity;
            _light.enabled = true;

            _trail.Clear();
            for (int i = 0; i < lib.dashTrail.Length; i++)
            {
                GameObject prefab = lib.dashTrail[i];
                if (prefab == null) continue;
                bool isCore = prefab == lib.boltCore;
                _trail.Add(BulletFx.Spawn(prefab, Center, rot, trailScale, _anchor, Cyan,
                    isCore ? 1f : 3f, isCore ? 1f : 0.3f, !isCore, false));
            }
        }

        void OnEnd()
        {
            if (!HasVfx) return;
            Vector3 end = Center;
            foreach (BulletFx fx in _trail) if (fx != null) fx.Detach(end);
            _trail.Clear();
            if (_light != null) _light.enabled = false;

            // Arrivée : éclat cyan dans le sens de la course.
            Quaternion rot = Quaternion.LookRotation(_controller.DashDirection);
            foreach (GameObject fx in Vfx3D.Library.dashEnd) Vfx3D.Spawn(fx, end, rot, endScale, Cyan);
            Vfx3D.Flash(end, Cyan, lightIntensity * 1.2f, 4f, 0.18f);
        }

        void LateUpdate()
        {
            if (_anchor != null && _light != null && _light.enabled)
                _anchor.position = Center;
        }
    }
}
