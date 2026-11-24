using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Construit le Canvas du HUD au lancement de la scène : une racine "HUD" (résolution de référence 1920x1080)
    /// avec le fond de l'interface centré en bas. Les prochains éléments (cœurs, stats...) s'ajouteront sous Root.
    /// </summary>
    public static class HudBootstrap
    {
        public const string RootName = "HUD";

        /// <summary>Racine du HUD, ou null s'il n'a pas été construit.</summary>
        public static RectTransform Root { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Root = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Build()
        {
            var config = Resources.Load<HudConfig>("HudConfig");
            if (config == null || config.frame == null || GameObject.Find(RootName) != null) return;

            var canvasGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Root = canvasGo.GetComponent<RectTransform>();

            var frameGo = new GameObject("HUD Frame", typeof(RectTransform), typeof(Image));
            var rect = frameGo.GetComponent<RectTransform>();
            rect.SetParent(Root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);   // centre bas
            rect.pivot = new Vector2(0.5f, 0f);
            Rect size = config.frame.rect;
            rect.sizeDelta = new Vector2(config.frameWidth, config.frameWidth * size.height / size.width);
            rect.anchoredPosition = new Vector2(config.horizontalOffset, config.bottomMargin);

            var image = frameGo.GetComponent<Image>();
            image.sprite = config.frame;
            image.preserveAspect = true;
            image.raycastTarget = false;

            if (config.heartFrame != null) BuildHeart(config);
        }

        /// <summary>Cœur-récipient au centre du HUD, de bas en haut : liquide arrière, liquide, cadre (+ reflet animé), texte de vie.</summary>
        static void BuildHeart(HudConfig config)
        {
            var heart = new GameObject("Heart", typeof(RectTransform), typeof(HeartHud));
            var rect = heart.GetComponent<RectTransform>();
            rect.SetParent(Root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(config.heartSize, config.heartSize);
            rect.anchoredPosition = new Vector2(config.horizontalOffset + config.heartOffset.x, config.bottomMargin + config.heartOffset.y);

            HeartLiquidGraphic back = AddLiquid(rect, "LiquidBack");
            back.bottomColor = new Color(0.25f, 0.0f, 0.04f, 0.9f);
            back.midColor = new Color(0.5f, 0.03f, 0.07f, 0.9f);
            back.brightColor = new Color(0.68f, 0.08f, 0.12f, 0.9f);
            back.surfaceColor = new Color(0.85f, 0.25f, 0.28f, 0.9f);
            HeartLiquidGraphic front = AddLiquid(rect, "Liquid");

            Image frame = AddStretched(rect, "Frame", typeof(Image)).GetComponent<Image>();
            frame.sprite = config.heartFrame;
            frame.raycastTarget = false;

            var textRect = AddStretched(rect, "Health", typeof(TextMeshProUGUI));
            var text = textRect.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = config.heartSize * 0.3f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(40, 0, 10, 255);
            text.raycastTarget = false;

            heart.GetComponent<HeartHud>().Setup(config, frame, text, back, front);
        }

        static HeartLiquidGraphic AddLiquid(RectTransform parent, string name)
        {
            var liquid = AddStretched(parent, name, typeof(HeartLiquidGraphic)).GetComponent<HeartLiquidGraphic>();
            liquid.raycastTarget = false;
            return liquid;
        }

        static RectTransform AddStretched(RectTransform parent, string name, System.Type component)
        {
            var go = new GameObject(name, typeof(RectTransform), component);
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
