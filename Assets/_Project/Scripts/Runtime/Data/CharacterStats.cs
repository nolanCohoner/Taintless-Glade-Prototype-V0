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
        [Header("Présentation (menu de sélection)")]
        [Tooltip("Identifiant stable (sauvegardé dans les préférences).")]
        public string id = "axolotl";
        public string displayName = "Axolotl";
        [Tooltip("Faux = « Bientôt disponible » : silhouette noire, non sélectionnable.")]
        public bool playable = true;
        public string perkTitle;
        [TextArea] public string perkText;
        public string weaponName;
        [TextArea] public string weaponText;
        [Tooltip("PV spéciaux affichés en plus des PV (ex. « 2/2 pierre »). Vide = aucun.")]
        public string bonusHp;
        [Tooltip("Silhouette noire affichée tant que le personnage n'est pas jouable.")]
        public Sprite silhouette;

        [Header("Remplacements en jeu (optionnels : vide = ceux du joueur de base)")]
        [Tooltip("Remplace le visuel (objet « Visual », avec son CharacterAnimator) du joueur.")]
        public GameObject visualPrefab;
        [Tooltip("Remplace le projectile de l'arme du joueur.")]
        public Projectile projectilePrefab;

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
