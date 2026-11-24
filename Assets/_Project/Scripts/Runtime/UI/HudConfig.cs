using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Références et réglages du HUD en jeu. Asset créé par HudInstaller dans Resources/,
    /// lu par HudBootstrap qui construit le Canvas au lancement de la scène.
    /// </summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Hud Config")]
    public class HudConfig : ScriptableObject
    {
        [Header("Fond de l'interface (Art/HUD/HUDFrame)")]
        public Sprite frame;
        [Tooltip("Largeur du fond, en pixels de la résolution de référence (1920x1080). La hauteur suit les proportions du sprite.")]
        public float frameWidth = 750f;
        [Tooltip("Décalage vers le haut depuis le bord bas de l'écran (pixels de référence).")]
        public float bottomMargin = 0f;
        [Tooltip("Décalage horizontal depuis le centre (pixels de référence).")]
        public float horizontalOffset = 0f;

        [Header("Vie : cœur-récipient (Art/HUD)")]
        [Tooltip("Cadre du cœur (heartFrameNormal) : le récipient qui contient le liquide.")]
        public Sprite heartFrame;
        [Tooltip("Frames du reflet sur le métal (heartFrameReflectionF1..F9), jouées dans l'ordre à la place du cadre normal.")]
        public Sprite[] reflectionFrames;
        [Tooltip("Taille du cœur (carré), en pixels de la résolution de référence.")]
        public float heartSize = 230f;
        [Tooltip("Position du cœur par rapport au centre bas du HUD.")]
        public Vector2 heartOffset = new Vector2(0f, -6f);
        [Tooltip("Délai entre deux reflets (s).")]
        public float reflectionInterval = 10f;
        [Tooltip("Images par seconde de l'animation du reflet.")]
        public float reflectionFps = 14f;
    }
}
