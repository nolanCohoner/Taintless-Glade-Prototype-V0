using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Objet d'environnement qui se dissout en pixels quand il masque le joueur (voir WallOcclusion).
    /// Ses renderers doivent utiliser le shader "TaintlessGlade/Environment Pixel Dissolve".
    /// </summary>
    public class FadeableWall : MonoBehaviour
    {
        public static readonly List<FadeableWall> All = new();
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        public float fadeOutDuration = 0.35f;
        public float fadeInDuration = 0.5f;
        [Tooltip("Reste dissous un court instant après ne plus masquer, pour éviter le clignotement.")]
        public float lingerTime = 0.25f;
        [Tooltip("Nombre de paliers de l'animation (effet rétro saccadé). 0 = continu.")]
        public int animationSteps = 12;

        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        float _dissolve;
        float _appliedDissolve = -1f;
        float _lastOccludedTime = float.NegativeInfinity;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void MarkOccluding() => _lastOccludedTime = Time.time;

        void Update()
        {
            bool hidden = Time.time - _lastOccludedTime < lingerTime;
            float duration = hidden ? fadeOutDuration : fadeInDuration;
            _dissolve = Mathf.MoveTowards(_dissolve, hidden ? 1f : 0f, Time.deltaTime / Mathf.Max(duration, 0.0001f));

            float value = animationSteps > 0 ? Mathf.Round(_dissolve * animationSteps) / animationSteps : _dissolve;
            if (Mathf.Approximately(value, _appliedDissolve)) return;
            _appliedDissolve = value;

            foreach (Renderer r in _renderers)
            {
                r.GetPropertyBlock(_block);
                _block.SetFloat(DissolveId, value);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
