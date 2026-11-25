using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Bouton de menu (souris, clavier, manette) : le cadre Kenney apparaît au survol / à la sélection,
    /// avec un rebond nerveux, un glissement du texte, un « punch » au clic et une gerbe de particules.
    /// Un bouton verrouillé tremble au clic et affiche le curseur « interdit ».
    /// </summary>
    public class MenuButton : Selectable, IPointerClickHandler, ISubmitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Clicked;

        public RectTransform content;
        public TMP_Text label;
        public Image border;
        public Image fill;
        [Tooltip("Cadre toujours visible (sinon il n'apparaît qu'au survol).")]
        public bool alwaysBorder;
        [Tooltip("Décalage du texte vers la droite au survol (px).")]
        public float slide = 16f;
        public float hoverScale = 0.06f;
        public bool burstOnClick = true;

        public bool Locked { get; set; }

        Spring _hot, _press, _punch, _shake;
        bool _pointer, _selected, _down;
        Vector2 _labelBase;
        Color _labelColor = Color.white;
        float _borderBase;

        public string Text
        {
            get => label != null ? label.text : "";
            set { if (label != null) label.text = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            var nav = navigation;
            nav.mode = Navigation.Mode.Automatic;
            navigation = nav;
            if (label != null)
            {
                _labelBase = label.rectTransform.anchoredPosition;
                _labelColor = label.color;
            }
            if (border != null) _borderBase = border.color.a;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_pointer) GameCursor.Set(GameCursor.Kind.Menu);
            _pointer = _selected = _down = false;
            _hot.Snap(0f); _press.Snap(0f); _punch.Snap(0f); _shake.Snap(0f);
            Apply();
        }

        bool Hot => _pointer || _selected;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            _pointer = true;
            if (IsActive() && IsInteractable()) Select();
            GameCursor.Set(Locked ? GameCursor.Kind.Forbidden : GameCursor.Kind.Hover);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            _pointer = false;
            GameCursor.Set(GameCursor.Kind.Menu);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            _selected = true;
            _punch.velocity += 5f;
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            _selected = false;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _down = true;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _down = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Activate();
        }

        public void OnSubmit(BaseEventData eventData) => Activate();

        public void Activate()
        {
            if (!IsActive() || !IsInteractable()) return;
            if (Locked)
            {
                _shake.velocity += 900f;
                return;
            }
            _punch.velocity += 9f;
            if (burstOnClick && UiParticles.Instance != null)
                UiParticles.Instance.BurstAt(transform as RectTransform, 14, new Color(0.85f, 1f, 0.7f, 1f));
            Clicked?.Invoke();
        }

        /// <summary>Affiche le cadre en permanence (ex. onglet actif).</summary>
        public void SetAlwaysBorder(bool on)
        {
            alwaysBorder = on;
            _borderBase = on ? 1f : 0f;
        }

        /// <summary>Couleur de base du texte (avant l'atténuation hors survol).</summary>
        public void SetTextColor(Color color) => _labelColor = color;

        /// <summary>Petit rebond attirant le regard (ex. bouton « Choisir » qui devient disponible).</summary>
        public void Pop(float strength = 8f) => _punch.velocity += strength;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _hot.target = Hot ? 1f : 0f;
            _press.target = _down && _pointer ? 1f : 0f;
            _hot.Step(dt, 380f, 17f);
            _press.Step(dt, 600f, 28f);
            _punch.Step(dt, 420f, 11f);
            _shake.Step(dt, 900f, 18f);
            Apply();
        }

        void Apply()
        {
            if (content == null) return;
            float scale = 1f + hoverScale * _hot.value + 0.05f * _punch.value - 0.06f * _press.value;
            content.localScale = new Vector3(scale, scale, 1f);
            content.anchoredPosition = new Vector2(_shake.value * 0.02f, 0f);

            float hot = Mathf.Clamp01(_hot.value);
            if (label != null)
            {
                label.rectTransform.anchoredPosition = _labelBase + new Vector2(slide * _hot.value, 0f);
                Color c = _labelColor;
                c.a *= Locked ? 0.5f : Mathf.Lerp(0.62f, 1f, hot);
                label.color = c;
            }
            if (border != null)
            {
                Color c = border.color;
                c.a = Mathf.Max(alwaysBorder ? _borderBase : 0f, hot * (Locked ? 0.45f : 1f));
                border.color = c;
            }
            if (fill != null)
            {
                Color c = fill.color;
                c.a = Mathf.Max(alwaysBorder ? 0.55f : 0f, hot * 0.7f);
                fill.color = c;
            }
        }

        /// <summary>Construit un bouton complet (zone cliquable, fond, cadre, texte).</summary>
        public static MenuButton Create(string name, Transform parent, string text, Vector2 size, float fontSize = 44f,
            bool alwaysBorder = false, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            MenuTheme theme = MenuTheme.Instance;
            RectTransform root = MenuUi.Rect(name, parent);
            root.sizeDelta = size;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var button = root.gameObject.AddComponent<MenuButton>();
            button.targetGraphic = hit;
            button.alwaysBorder = alwaysBorder;

            RectTransform content = MenuUi.Rect("Content", root);
            MenuUi.Stretch(content);
            button.content = content;

            Image fill = MenuUi.Image("Fill", content, theme.panelFill, new Color(theme.panelTint.r, theme.panelTint.g, theme.panelTint.b, 0f), sliced: true);
            MenuUi.Stretch(fill.rectTransform);
            fill.pixelsPerUnitMultiplier = 0.5f;
            button.fill = fill;

            Image border = MenuUi.Image("Border", content, theme.buttonBorder, new Color(theme.borderTint.r, theme.borderTint.g, theme.borderTint.b, alwaysBorder ? 1f : 0f), sliced: true);
            MenuUi.Stretch(border.rectTransform);
            border.pixelsPerUnitMultiplier = 0.5f;
            button.border = border;

            TextMeshProUGUI label = MenuUi.Text("Label", content, text, fontSize, theme.text, align, FontStyles.Normal);
            MenuUi.Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(align == TextAlignmentOptions.Left ? 40f : 12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            button.label = label;

            button.Awake_Init();
            return button;
        }

        // Awake n'est pas rappelé pour un composant ajouté pendant l'édition : on réinitialise explicitement.
        void Awake_Init()
        {
            if (label != null)
            {
                _labelBase = label.rectTransform.anchoredPosition;
                _labelColor = label.color;
            }
            if (border != null) _borderBase = border.color.a;
        }
    }
}
