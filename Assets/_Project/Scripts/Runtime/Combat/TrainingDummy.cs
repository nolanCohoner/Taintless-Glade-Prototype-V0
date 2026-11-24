using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Pantin d'entraînement immortel : encaisse tout, flashe, affiche dégâts, total et DPS.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [Header("Feedback")]
        public SpriteRenderer spriteRenderer;
        public ScalePunch spritePunch;
        public Color flashColor = Color.white;
        public float flashDuration = 0.08f;
        public GameObject hitVfxPrefab;
        public float cameraShakeStrength = 0.08f;
        public float cameraShakeDuration = 0.1f;

        [Header("Dégâts")]
        public DamageNumberSpawner damageNumbers;
        public TMP_Text statsText;
        [Tooltip("Fenêtre glissante pour le calcul du DPS.")]
        public float dpsWindow = 3f;
        [Tooltip("Sans coup pendant ce délai, le total est remis à zéro au prochain coup.")]
        public float resetTotalAfter = 3f;

        Color _baseColor;
        float _flashTimer;
        float _total;
        float _lastHitTime = float.NegativeInfinity;
        readonly Queue<(float time, float amount)> _recentHits = new();

        void Awake()
        {
            if (spriteRenderer != null) _baseColor = spriteRenderer.color;
            RefreshStats();
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (Time.time - _lastHitTime > resetTotalAfter) _total = 0f;
            _lastHitTime = Time.time;
            _total += info.amount;
            _recentHits.Enqueue((Time.time, info.amount));

            _flashTimer = flashDuration;
            if (spritePunch != null) spritePunch.Punch(new Vector3(0.25f, -0.2f, 0f) * (1f + info.knockback * 0.2f));
            if (damageNumbers != null) damageNumbers.Show(info.amount);
            // Avec les effets 3D, les projectiles jouent déjà leur propre impact.
            if (!Vfx3D.Available && hitVfxPrefab != null)
            {
                Vector3 back = info.direction.sqrMagnitude > 0f ? -info.direction : Vector3.back;
                Instantiate(hitVfxPrefab, info.point, Quaternion.LookRotation(back));
            }
            if (CameraFollow.Instance != null)
                CameraFollow.Instance.Shake(cameraShakeStrength, cameraShakeDuration);
        }

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (spriteRenderer != null)
                    spriteRenderer.color = _flashTimer > 0f ? flashColor : _baseColor;
            }

            while (_recentHits.Count > 0 && Time.time - _recentHits.Peek().time > dpsWindow)
                _recentHits.Dequeue();

            RefreshStats();
        }

        void RefreshStats()
        {
            if (statsText == null) return;
            float sum = 0f;
            foreach (var hit in _recentHits) sum += hit.amount;
            statsText.text = $"Total {_total:0}\nDPS {sum / dpsWindow:0.0}";
        }
    }
}
