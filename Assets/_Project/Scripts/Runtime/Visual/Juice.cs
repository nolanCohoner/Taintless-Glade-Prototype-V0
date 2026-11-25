using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>Micro-pause à l'impact : fige le jeu quelques dizaines de ms pour rendre les coups plus percutants.</summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop _instance;
        static float _nextAllowed;
        float _until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _nextAllowed = 0f;
        }

        /// <param name="cooldown">Délai minimum avant le prochain hit-stop, pour qu'un tir rapide ne fige jamais le jeu en continu.</param>
        public static void Hit(float duration = 0.04f, float timeScale = 0.05f, float cooldown = 0.12f)
        {
            if (Time.unscaledTime < _nextAllowed) return;
            if (_instance == null)
            {
                var go = new GameObject("HitStop");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitStop>();
            }
            Time.timeScale = timeScale;
            _instance._until = Time.unscaledTime + duration;
            _nextAllowed = _instance._until + cooldown;
        }

        void Update()
        {
            if (_until > 0f && Time.unscaledTime >= _until)
            {
                _until = 0f;
                Time.timeScale = 1f;
            }
        }

        void OnDisable()
        {
            if (_until > 0f) Time.timeScale = 1f;
        }
    }

    /// <summary>Joue des sons à hauteur légèrement aléatoire (sources recyclées). Sans clip assigné, ne fait rien.</summary>
    public static class Sfx
    {
        static readonly List<AudioSource> _sources = new List<AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _sources.Clear();

        public static void Play(AudioClip[] clips, Vector3 position, float volume = 1f, float pitchVariation = 0.08f)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            AudioSource source = null;
            for (int i = 0; i < _sources.Count; i++)
            {
                if (_sources[i] == null) continue;
                if (!_sources[i].isPlaying) { source = _sources[i]; break; }
            }
            if (source == null)
            {
                if (_sources.Count >= 16) return;
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.6f;
                _sources.Add(source);
            }
            source.transform.position = position;
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }
    }
}
