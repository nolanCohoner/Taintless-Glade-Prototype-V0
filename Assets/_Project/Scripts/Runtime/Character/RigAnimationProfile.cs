using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Réglages d'animation procédurale, partageables entre persos (un profil "quadrupède" pour l'Axolotl,
    /// le Renard, l'Ours... et des variantes pour ceux qui bougent différemment). Angles en degrés.
    /// </summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Rig Animation Profile", fileName = "AnimProfile")]
    public class RigAnimationProfile : ScriptableObject
    {
        [Header("Marche / Course")]
        public float walkCyclesPerSecond = 2.2f;
        public float runCyclesPerSecond = 3.6f;
        [Tooltip("Amplitude avant/arrière du pas (unités).")] public float walkStride = 0.05f;
        public float runStride = 0.12f;
        [Tooltip("Inclinaison du pied pendant le pas (degrés).")] public float stepTilt = 10f;
        [Tooltip("0 = trot en diagonale, 1 = bonds (pattes avant ensemble, puis arrière) en course.")]
        [Range(0f, 1f)] public float runGallop = 0.8f;
        [Tooltip("Roulis du corps à chaque pas (degrés).")] public float bodyRoll = 3f;
        [Tooltip("Le corps s'étire en haut du rebond et s'écrase en bas.")] public float stepStretch = 0.08f;
        public float walkLegLift = 0.04f;
        public float runLegLift = 0.09f;
        public float walkBodyBob = 0.035f;
        public float runBodyBob = 0.08f;
        [Tooltip("Inclinaison du corps vers l'avant en course.")]
        public float runBodyLean = 8f;
        [Tooltip("Part du mouvement du corps transmise aux hanches (0 = pattes fixes, 1 = collées au corps).")]
        [Range(0f, 1f)] public float hipFollow = 0.5f;

        [Header("Repos")]
        public float breathSpeed = 2.2f;
        public float breathAmount = 0.03f;

        [Header("Tête")]
        [Tooltip("Part de l'angle de visée appliquée à la tête.")]
        [Range(0f, 1f)] public float aimHeadInfluence = 0.6f;
        [Tooltip("Inclinaison de la tête quand on se déplace vers le haut / le bas de l'écran.")]
        public float moveHeadInfluence = 12f;
        public float maxHeadAngle = 35f;
        public float headSmoothTime = 0.08f;
        [Tooltip("Retard du rebond de la tête par rapport au corps.")]
        public float headLag = 0.06f;
        [Range(0f, 2f)] public float headBobFollow = 0.8f;
        [Tooltip("Part de l'angle de visée appliquée au corps.")]
        [Range(0f, 1f)] public float bodyAimInfluence = 0.12f;

        [Header("Queue")]
        public float idleTailAmplitude = 6f;
        public float idleTailFrequency = 1.1f;
        public float walkTailAmplitude = 14f;
        public float runTailAmplitude = 30f;
        [Tooltip("La queue se lève en course (angle).")]
        public float runTailLift = 12f;
        public float tailResponsiveness = 14f;

        [Header("Attaque / utilisation d'arme")]
        public float attackDuration = 0.18f;
        public float headThrust = 0.12f;
        public float bodyRecoil = 0.06f;
        public float attackSquash = 0.12f;
        public float attackTailFlick = 20f;

        [Header("Esquive (dash)")]
        public float dashStretch = 0.3f;
        public float dashLegTuck = 40f;
        [Tooltip("Pattes avant tendues devant / arrière derrière pendant le dash (unités).")] public float dashLegStretch = 0.14f;
        public float dashLegLift = 0.08f;
        [Tooltip("Angle relatif de la queue pendant le dash (négatif = vers le bas / tendue).")]
        public float dashTailLift = -15f;
        public float dashBodyAngle = -8f;

        [Header("Tentacules (tête et queue)")]
        public float tentacleIdleAmplitude = 4f;
        public float tentacleMoveAmplitude = 9f;
        public float tentacleAttackAmplitude = 14f;
        public float tentacleDashAmplitude = 12f;
        public float tentacleIdleFrequency = 0.7f;
        public float tentacleMoveFrequency = 2.4f;
        [Tooltip("Les tentacules traînent derrière le rebond du corps (angle par unité de vitesse verticale).")]
        public float tentacleDrag = 6f;
        [Tooltip("Les tentacules se relèvent en dash.")]
        public float tentacleDashLift = 22f;
        public float tentacleResponsiveness = 14f;

        [Header("Arme (arc) et regard")]
        [Tooltip("0 = la patte avant balance normalement, 1 = elle tient l'arme pointée vers la visée.")]
        [Range(0f, 1f)] public float bowHold = 0.9f;
        [Tooltip("Recul de l'arme au tir (unités).")]
        public float bowKick = 0.07f;
        [Tooltip("La patte avant éloignée tire la corde : angle de recul pendant le tir.")]
        public float drawPawAngle = 25f;
        [Tooltip("Recul de la patte qui tire la corde (unités).")] public float drawPawPull = 0.1f;
        [Tooltip("Distance dont la patte avant se tend vers la visée pour tenir l'arme (unités).")] public float bowPawReach = 0.16f;
        [Tooltip("Distance écran (pixels) au-delà de laquelle les iris sont au maximum de leur course.")]
        public float irisFullDistance = 90f;
        public float irisSpeed = 30f;

        [Header("Sueur (course rapide / dash)")]
        public float sweatRunDrops = 7f;
        public float sweatDashDrops = 35f;

        [Header("Naturel (anticipation, irrégularité, repos vivant)")]
        [Tooltip("Variation aléatoire de chaque foulée (0.15 = ±15 % de longueur, un peu plus sur la hauteur).")]
        [Range(0f, 0.4f)] public float stepVariation = 0.15f;
        [Tooltip("Rebond asymétrique : la montée est plus lente que la chute (0 = sinus parfait).")]
        [Range(0f, 0.4f)] public float stepAsymmetry = 0.18f;
        [Tooltip("Temps (s) avant le tir pendant lequel le perso se contracte (tir maintenu).")]
        public float windupTime = 0.1f;
        [Tooltip("Recul du corps pendant l'anticipation du tir (unités).")]
        public float windupPull = 0.035f;
        [Tooltip("Durée (s) de la contraction au début du dash.")]
        public float dashAnticipation = 0.07f;
        [Tooltip("Délai aléatoire entre deux petits événements au repos (tête penchée, coup de queue...).")]
        public float idleEventMinDelay = 2.5f;
        public float idleEventMaxDelay = 6f;

        [Header("Demi-tour")]
        public float turnDuration = 0.12f;
        public float turnStretch = 0.15f;
        [Tooltip("Zone morte horizontale de la visée avant de se retourner (0..1).")]
        [Range(0f, 0.9f)] public float facingDeadZone = 0.15f;
    }
}
