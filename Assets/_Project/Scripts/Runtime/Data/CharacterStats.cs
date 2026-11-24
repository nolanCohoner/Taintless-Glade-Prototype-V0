using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Stats d'un personnage jouable, exprimées dans l'échelle du GDD (base 2, max 4),
    /// converties en unités monde via les facteurs de conversion.
    /// </summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Character Stats", fileName = "CharacterStats")]
    public class CharacterStats : ScriptableObject
    {
        public string displayName = "Axolotl";
        [Tooltip("Couleur du carré placeholder en attendant les sprites animés.")]
        public Color placeholderColor = new Color(1f, 0.6f, 0.78f);

        [Header("Stats GDD")]
        public int maxHp = 3;
        public float dashSpeed = 2f;
        public float speed = 2f;
        public float shotSpeed = 2f;
        public float shotLength = 2.75f;
        public float attack = 4f;
        public float luck = 0f;

        [Header("Arme")]
        [Tooltip("Cadence de tir (attaques par seconde).")]
        public float attacksPerSecond = 3f;

        [Header("Conversion GDD -> unités monde")]
        public float speedToMetersPerSecond = 2.5f;
        public float dashToMeters = 1.75f;
        public float shotSpeedToMetersPerSecond = 16f;
        public float shotLengthToMeters = 4.5f;

        public float MoveSpeed => speed * speedToMetersPerSecond;
        public float DashDistance => dashSpeed * dashToMeters;
        public float ProjectileSpeed => shotSpeed * shotSpeedToMetersPerSecond;
        public float ProjectileRange => shotLength * shotLengthToMeters;
        public float AttackCooldown => 1f / Mathf.Max(0.01f, attacksPerSecond);
    }
}
