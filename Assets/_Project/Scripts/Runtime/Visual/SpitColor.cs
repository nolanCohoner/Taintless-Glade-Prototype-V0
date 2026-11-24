using UnityEngine;

namespace TaintlessGlade
{
    public enum SpitColorMode
    {
        [Tooltip("Bleu cyan fixe.")] Cyan,
        [Tooltip("Arc-en-ciel : la teinte glisse en continu avec le temps.")] Rainbow,
    }

    /// <summary>
    /// Couleur courante du crachat de l'Axolotl (tir, éclat de bouche, impact). PlayerAttack choisit le mode ;
    /// tout le reste lit Current, qui est soit le cyan, soit une teinte qui défile avec le temps.
    /// </summary>
    public static class SpitColor
    {
        public static readonly Color Cyan = new Color(0.2f, 0.95f, 1f, 1f);

        public static SpitColorMode Mode = SpitColorMode.Rainbow;
        /// <summary>Tours complets de la roue des couleurs par seconde, en mode arc-en-ciel.</summary>
        public static float RainbowCyclesPerSecond = 0.35f;

        public static Color Current => Mode == SpitColorMode.Cyan
            ? Cyan
            : Color.HSVToRGB(Mathf.Repeat(Time.time * RainbowCyclesPerSecond, 1f), 0.85f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Mode = SpitColorMode.Rainbow;
    }
}
