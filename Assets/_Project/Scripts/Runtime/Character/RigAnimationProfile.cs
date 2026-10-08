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
        public float walkLegSwing = 28f;
        public float runLegSwing = 50f;
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
        public float dashLegTuck = 55f;
        [Tooltip("Angle relatif de la queue pendant le dash (négatif = vers le bas / tendue).")]
        public float dashTailLift = -15f;
        public float dashBodyAngle = -8f;

        [Header("Demi-tour")]
        public float turnDuration = 0.12f;
        public float turnStretch = 0.15f;
        [Tooltip("Zone morte horizontale de la visée avant de se retourner (0..1).")]
        [Range(0f, 0.9f)] public float facingDeadZone = 0.15f;
    }
}
