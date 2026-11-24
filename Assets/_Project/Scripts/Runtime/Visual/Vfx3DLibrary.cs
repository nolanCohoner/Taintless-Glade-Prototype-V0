using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Références vers les prefabs d'effets 3D (Free Quick Effects Vol.1). Asset créé par Vfx3DInstaller dans Resources/,
    /// pour que le code runtime n'ait pas besoin de chemins en dur ni de câblage manuel dans la scène.
    /// </summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Vfx 3D Library")]
    public class Vfx3DLibrary : ScriptableObject
    {
        [Header("Tir de base")]
        [Tooltip("Sort du pack Full Opaque Spell joué à la place de la balle (VFX_Arcanic_TrackingShot_Main).")]
        public GameObject spellShot;
        public GameObject muzzle;
        public GameObject impact;

        [Header("Pack Hovl Magic Effects (balles alternatives, cycle avec la touche V)")]
        [Tooltip("Effets qui suivent la balle : Sparks flashing pink (autour de la bulle 2D).")]
        public GameObject[] hovlBullets;
        [Tooltip("Explosions jouées à l'impact (toutes). Le dernier élément sert aussi de dissipation en fin de portée.")]
        public GameObject[] hovlImpacts;

        [Header("Crachat magique (balle 3D, style 0)")]
        [Tooltip("Tête + traînée de la balle (Free Quick Effects : vfx_Projectile_02).")]
        public GameObject boltCore;
        [Tooltip("Étincelles qui entourent la balle (Hovl : Sparks flashing).")]
        public GameObject boltSparks;
        [Tooltip("Bulle de l'arme (Cracha Arc en ciel bulle 1), posée au cœur de la balle.")]
        public Sprite boltBubble;
        [Tooltip("Anneau arc-en-ciel de l'arme (Cracha Arc en ciel Fond), projeté au sol sous le tir chargé.")]
        public Sprite boltRing;
        [Tooltip("Boule lumineuse au cœur de la balle (Hovl : halo 'Glow' de Teleport).")]
        public GameObject boltOrb;
        [Tooltip("Traînée d'étincelles continue (Hovl : Sparks blue).")]
        public GameObject boltSparksExtra;
        [Tooltip("Halo d'étoiles autour de la balle (Hovl : Star aura).")]
        public GameObject boltAura;
        [Tooltip("Aura autour du personnage pendant la charge (Hovl : Star aura + Buff).")]
        public GameObject[] chargeAura;
        [Tooltip("Explosion supplémentaire du tir chargé (Hovl : Red energy explosion, Free Quick Effects : vfx_Explosion_02).")]
        public GameObject[] boltChargedExplosion;
        [Tooltip("Éclat joué au départ du tir.")]
        public GameObject[] boltLaunch;
        [Tooltip("Petites étincelles au rebond sur un mur.")]
        public GameObject[] boltBounce;
        [Tooltip("Explosion finale (Hovl). Jouée à l'impact sur un ennemi ou au 2e contact avec un mur.")]
        public GameObject[] boltExplosion;
        [Tooltip("Flash central de l'explosion (Free Quick Effects : vfx_Impact_01).")]
        public GameObject boltFlash;

        [Header("Dash téléportation")]
        [Tooltip("Éclat au point de départ.")]
        public GameObject[] dashStart;
        [Tooltip("Effets qui suivent le joueur pendant le dash et laissent la traînée cyan.")]
        public GameObject[] dashTrail;
        [Tooltip("Éclat au point d'arrivée.")]
        public GameObject[] dashEnd;

        [Header("Personnage")]
        public GameObject sweat;
        public Material sweatDropMaterial;
    }
}
