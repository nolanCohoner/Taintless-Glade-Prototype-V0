using TMPro;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Style commun de toute l'interface : police, cadres Kenney, couleurs, curseurs.
    /// Asset : Resources/MenuTheme (rempli par Tools > Taintless Glade > Menu > Construire le menu principal).
    /// Tout nouveau texte / panneau d'UI doit puiser ici pour rester cohérent.
    /// </summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Menu Theme", fileName = "MenuTheme")]
    public class MenuTheme : ScriptableObject
    {
        static MenuTheme _instance;
        public static MenuTheme Instance => _instance != null ? _instance : (_instance = Resources.Load<MenuTheme>("MenuTheme"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [Header("Police")]
        public TMP_FontAsset font;

        [Header("Cadres (9-slice)")]
        public Sprite panelFill;
        public Sprite panelBorder;
        public Sprite buttonBorder;
        public Sprite divider;
        public Sprite dividerFade;
        public Sprite softCircle;
        public Sprite pixel;

        [Header("Icônes")]
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite lockIcon;
        public Sprite doorIcon;

        [Header("Couleurs")]
        public Color text = Color.white;
        public Color textDim = new Color(1f, 1f, 1f, 0.55f);
        public Color accent = new Color(0.62f, 0.78f, 0.42f);
        public Color danger = new Color(0.92f, 0.2f, 0.22f);
        public Color panelTint = new Color(0.04f, 0.06f, 0.07f, 0.88f);
        public Color borderTint = new Color(1f, 1f, 1f, 0.92f);

        [Header("Curseurs (importés en type Cursor)")]
        public Texture2D cursorMenu;
        public Vector2 menuHotspot = new Vector2(8f, 6f);
        public Texture2D cursorHover;
        public Vector2 hoverHotspot = new Vector2(10f, 4f);
        public Texture2D cursorGame;
        public Vector2 gameHotspot = new Vector2(16f, 16f);
        public Texture2D cursorBusy;
        public Vector2 busyHotspot = new Vector2(16f, 16f);
        public Texture2D cursorForbidden;
        public Vector2 forbiddenHotspot = new Vector2(16f, 16f);
    }
}
