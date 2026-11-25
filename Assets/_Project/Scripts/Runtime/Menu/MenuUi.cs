using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>Ressort amorti (animations « nerveuses » des menus, indépendantes du Time.timeScale).</summary>
    public struct Spring
    {
        public float value, velocity, target;

        public Spring(float start) { value = start; target = start; velocity = 0f; }

        public void Snap(float v) { value = target = v; velocity = 0f; }

        /// <param name="stiffness">Raideur (≈ 250..600 : nerveux). damping : amortissement (≈ 14..26 : rebond marqué à léger).</param>
        public float Step(float dt, float stiffness = 420f, float damping = 20f)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            velocity += ((target - value) * stiffness - velocity * damping) * dt;
            value += velocity * dt;
            return value;
        }
    }

    /// <summary>Petits utilitaires de construction d'UI (utilisés par le constructeur de scène et par les vues du menu).</summary>
    public static class MenuUi
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced && sprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string content, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            MenuTheme theme = MenuTheme.Instance;
            if (theme != null && theme.font != null) text.font = theme.font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Panneau sombre semi-transparent avec cadre Kenney (même style que la maquette du jeu).</summary>
        public static RectTransform Panel(string name, Transform parent, Vector2 size, float alpha = 1f)
        {
            MenuTheme theme = MenuTheme.Instance;
            RectTransform root = Rect(name, parent);
            root.sizeDelta = size;
            Color fill = theme.panelTint;
            fill.a *= alpha;
            Image fillImage = Image("Fill", root, theme.panelFill, fill, sliced: true, raycast: true);
            Stretch(fillImage.rectTransform);
            fillImage.pixelsPerUnitMultiplier = 0.5f;
            Image border = Image("Border", root, theme.panelBorder, theme.borderTint, sliced: true);
            Stretch(border.rectTransform);
            border.pixelsPerUnitMultiplier = 0.5f;
            return root;
        }

        public static Image Divider(string name, Transform parent, float width, bool fade = true)
        {
            MenuTheme theme = MenuTheme.Instance;
            if (width > 400f)
            {
                // Trop large pour l'ornement Kenney (il serait déformé) : simple filet fin.
                Image thin = Image(name, parent, theme.pixel, new Color(1f, 1f, 1f, 0.3f));
                thin.rectTransform.sizeDelta = new Vector2(width, 2f);
                return thin;
            }
            Image line = Image(name, parent, fade ? theme.dividerFade : theme.divider, theme.borderTint);
            line.rectTransform.sizeDelta = new Vector2(width, width * 22f / 96f);
            return line;
        }

        /// <summary>Ajoute un CanvasGroup (ou le récupère).</summary>
        public static CanvasGroup Group(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }
    }
}
