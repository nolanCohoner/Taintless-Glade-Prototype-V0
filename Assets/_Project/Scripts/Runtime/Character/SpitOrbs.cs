using System;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Arme crachat de l'Axolotl : un halo arc-en-ciel et plusieurs boules de crachat superposées, chacune animée à sa façon.
    /// Repos : flottaison douce, différente pour chaque boule. Marche / course : flottaison plus vive, les boules traînent
    /// derrière l'arme (effet gélatine). Anticipation : elles se resserrent. Tir : recul en cascade et gonflement.
    /// Se branche sur le CharacterAnimator (qui garde la main sur la position et l'orientation de l'arme).
    /// Les boules restent droites à l'écran même quand l'arme pivote vers la visée.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class SpitOrbs : MonoBehaviour
    {
        [Serializable]
        public class Orb
        {
            [Tooltip("Pivot de la boule, placé au centre du dessin : c'est lui qui flotte, tourne et gonfle.")] public Transform pivot;
            [Tooltip("Position de repos (unités), dans le référentiel de l'arme.")] public Vector2 home;
            [Tooltip("Rythme de flottaison propre à la boule (cycles par seconde).")] [Range(0.2f, 2f)] public float frequency = 0.6f;
            [Tooltip("Décalage de l'onde pour que les boules ne flottent pas en même temps.")] public float phase;
            [Tooltip("Amplitude de flottaison au repos (unités).")] public float amplitude = 0.035f;
            [Tooltip("Retard pris dans les déplacements : 0 = collée à l'arme, 1 = traîne beaucoup.")] [Range(0f, 1.5f)] public float inertia = 0.8f;
            [Tooltip("Partie du tir (0..0.5) qui s'écoule avant que cette boule réagisse : elles partent en cascade.")] [Range(0f, 0.5f)] public float shotDelay;

            [NonSerialized] public Vector2 position, velocity;
            [NonSerialized] public float time, kickIn;
        }

        public CharacterAnimator animator;
        [Tooltip("Conteneur qui garde les boules droites à l'écran (enfant de l'arme).")] public Transform art;
        [Tooltip("Anneau arc-en-ciel derrière les boules (tourne lentement).")] public Transform halo;
        public Vector2 haloHome;
        public Orb[] orbs = Array.Empty<Orb>();

        [Header("Ressort")]
        [Tooltip("Raideur du rappel vers la position de repos : grande valeur = boules plus nerveuses.")] public float stiffness = 60f;
        [Tooltip("Amortissement : petite valeur = plus de rebonds gélatineux.")] public float damping = 9f;
        [Tooltip("Force du retard pris quand l'arme se déplace.")] public float trailStrength = 0.25f;
        [Tooltip("Retard maximal par rapport à la position de repos (unités).")] public float trailMax = 0.3f;

        [Header("Mouvement")]
        [Tooltip("Flottaison plus rapide en marchant (multiplicateur ajouté).")] public float moveFloatBoost = 0.8f;
        [Tooltip("Flottaison plus rapide en courant (multiplicateur ajouté).")] public float runFloatBoost = 1.2f;
        [Tooltip("Amplitude de flottaison ajoutée en marchant / en courant (relative).")] public Vector2 moveAmplitudeBoost = new Vector2(0.5f, 0.8f);

        [Header("Tir")]
        [Tooltip("Resserrement des boules quand le tir se prépare (0..1).")] public float windupSqueeze = 0.2f;
        [Tooltip("Recul des boules pendant l'anticipation (unités).")] public float windupPull = 0.06f;
        [Tooltip("Impulsion de recul de chaque boule au tir (unités par seconde).")] public float recoilKick = 1.2f;
        [Tooltip("Gonflement de chaque boule au tir (relatif).")] public float shotPulse = 0.2f;
        [Tooltip("Durée (s) sur laquelle les boules sont décalées au tir.")] public float shotStagger = 0.18f;

        [Header("Halo")]
        public float haloSpin = 8f;
        public float haloMoveSpin = 40f;
        public float haloShotSpin = 140f;

        const float TwoPi = Mathf.PI * 2f;

        Vector3 _lastAnchor;
        float _lastProgress = 1f;
        float _haloAngle;
        bool _initialized;

        void OnEnable() => _initialized = false;

        void LateUpdate()
        {
            if (art == null || orbs.Length == 0) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            if (animator == null) animator = GetComponentInParent<CharacterAnimator>();

            float move = 0f, run = 0f, dash = 0f, windup = 0f, progress = 1f;
            if (animator != null)
            {
                move = animator.Move01;
                run = animator.Run01;
                dash = animator.Dash01;
                windup = animator.Windup01 * (1f - dash);
                progress = animator.AttackProgress;
            }

            // Les boules restent droites à l'écran : on annule la rotation de l'arme et le retournement du perso.
            float angle = transform.localEulerAngles.z;
            float flip = transform.lossyScale.x < 0f ? -1f : 1f;
            art.localRotation = Quaternion.Euler(0f, 0f, -angle);
            art.localScale = new Vector3(flip, 1f, 1f);
            Vector2 aim = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * flip, Mathf.Sin(angle * Mathf.Deg2Rad));

            // Déplacement de l'arme depuis la dernière image, dans le référentiel des boules.
            Vector3 anchor = transform.position;
            if (!_initialized) { _lastAnchor = anchor; _lastProgress = progress; _initialized = true; }
            Vector2 delta = art.InverseTransformVector(anchor - _lastAnchor);
            _lastAnchor = anchor;
            delta = Vector2.ClampMagnitude(delta, 0.5f);

            bool shot = progress < _lastProgress - 0.0001f;
            _lastProgress = progress;

            float speedMul = 1f + move * moveFloatBoost + run * runFloatBoost;
            float ampMul = 1f + move * moveAmplitudeBoost.x + run * moveAmplitudeBoost.y;
            float squeeze = 1f - windupSqueeze * windup;
            float limit = trailMax * (1f + dash * 0.5f);
            float dashTrail = 1f + dash * 1.5f;
            float shotEnvelope = progress < 1f ? Mathf.Sin(progress * Mathf.PI) : 0f;

            foreach (Orb o in orbs)
            {
                if (o.pivot == null) continue;

                // Flottaison : deux ondes de rythmes différents (x, y) -> trajectoire en boucle qui ne se répète pas.
                o.time += dt * o.frequency * speedMul;
                float a = o.time * TwoPi + o.phase;
                Vector2 wander = new Vector2(Mathf.Sin(a), Mathf.Sin(a * 1.37f + 1.3f) * 1.25f) * (o.amplitude * ampMul);
                Vector2 target = o.home * squeeze - aim * (windupPull * windup) + wander;

                // Tir : recul (impulsion) en cascade, puis le ressort ramène la boule avec un petit rebond.
                if (shot) o.kickIn = o.shotDelay * shotStagger / 0.5f;
                if (o.kickIn > 0f)
                {
                    o.kickIn -= dt;
                    if (o.kickIn <= 0f) { o.kickIn = 0f; o.velocity -= aim * recoilKick; }
                }

                o.velocity += ((target - o.position) * stiffness - o.velocity * damping) * dt;
                o.position += o.velocity * dt - delta * (o.inertia * trailStrength * dashTrail);
                Vector2 away = o.position - target;
                if (away.sqrMagnitude > limit * limit) o.position = target + away.normalized * limit;

                float local = Mathf.Clamp01((progress - o.shotDelay) / Mathf.Max(1f - o.shotDelay, 0.01f));
                float pulse = progress < 1f ? Mathf.Sin(local * Mathf.PI) : 0f;
                float breathe = 1f + Mathf.Sin(a * 1.1f + 0.7f) * 0.03f;
                float scale = breathe * squeeze * (1f + pulse * shotPulse);
                float tilt = Mathf.Sin(a * 0.8f) * 4f - Mathf.Clamp(o.velocity.x, -1.5f, 1.5f) * 12f;

                o.pivot.localPosition = new Vector3(o.position.x, o.position.y, o.pivot.localPosition.z);
                o.pivot.localRotation = Quaternion.Euler(0f, 0f, tilt);
                o.pivot.localScale = new Vector3(scale, scale, 1f);
            }

            if (halo != null)
            {
                _haloAngle += dt * (haloSpin + haloMoveSpin * Mathf.Max(move, dash) + haloShotSpin * shotEnvelope);
                float haloScale = (1f + shotEnvelope * 0.12f) * (1f - windup * 0.06f) * (1f + Mathf.Sin(Time.time * 1.3f) * 0.015f);
                halo.localPosition = new Vector3(haloHome.x, haloHome.y, halo.localPosition.z);
                halo.localRotation = Quaternion.Euler(0f, 0f, _haloAngle);
                halo.localScale = new Vector3(haloScale, haloScale, 1f);
            }
        }
    }
}
