using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Écran de sélection de personnage : fait tourner la roulette 3D (MenuStage) et affiche les stats, l'atout et l'arme
    /// du personnage au premier plan. Les personnages « bientôt disponibles » s'affichent en silhouette, non sélectionnables.
    /// </summary>
    public class CharacterSelectView : MonoBehaviour
    {
        public MenuController controller;
        public MenuStage stage;
        public RectTransform statsRoot;
        public MenuButton leftArrow;
        public MenuButton rightArrow;
        public MenuButton confirmButton;
        public TMP_Text pageHint;

        struct StatDef
        {
            public string label;
            public float max;
            public System.Func<CharacterStats, float> get;
        }

        static readonly StatDef[] Stats =
        {
            new StatDef { label = "Vitesse de dash", max = 4f, get = s => s.dashSpeed },
            new StatDef { label = "Vitesse", max = 4f, get = s => s.speed },
            new StatDef { label = "Vitesse du tir", max = 4f, get = s => s.shotSpeed },
            new StatDef { label = "Portée du tir", max = 4f, get = s => s.shotLength },
            new StatDef { label = "Attaque", max = 6f, get = s => s.attack },
            new StatDef { label = "Chance", max = 4f, get = s => s.luck },
        };

        class Row
        {
            public TMP_Text label, value;
            public Image back, fill;
            public Spring amount;
        }

        TMP_Text _name, _status, _hp, _perk, _perkText, _weapon, _weaponText;
        readonly Row[] _rows = new Row[Stats.Length];
        CanvasGroup _statsGroup;
        CharacterStats _shown;
        bool _built;
        float _fade = 1f;

        void Awake() => Build();

        void Start()
        {
            var none = new Navigation { mode = Navigation.Mode.None };
            if (leftArrow != null) { leftArrow.Clicked += () => Move(-1); leftArrow.navigation = none; }
            if (rightArrow != null) { rightArrow.Clicked += () => Move(1); rightArrow.navigation = none; }
            if (confirmButton != null) confirmButton.Clicked += Confirm;
        }

        void Build()
        {
            if (_built || statsRoot == null) return;
            _built = true;
            MenuTheme theme = MenuTheme.Instance;
            _statsGroup = MenuUi.Group(statsRoot.gameObject);

            float w = statsRoot.rect.width > 10f ? statsRoot.rect.width : 560f;
            float x = 36f, inner = w - 72f;
            float y = -30f;

            _name = Line(statsRoot, "Name", 54f, theme.text, FontStyles.Bold, x, ref y, inner, 66f);
            _status = Line(statsRoot, "Status", 28f, theme.accent, FontStyles.Normal, x, ref y, inner, 36f);
            y -= 6f;
            Divider(statsRoot, x, y, inner);
            y -= 18f;
            _hp = Line(statsRoot, "Hp", 32f, theme.text, FontStyles.Normal, x, ref y, inner, 42f);
            y -= 4f;

            for (int i = 0; i < Stats.Length; i++)
            {
                var row = new Row();
                RectTransform rt = MenuUi.Rect("Stat_" + i, statsRoot);
                MenuUi.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(inner, 38f));
                row.label = MenuUi.Text("Label", rt, Stats[i].label, 24f, theme.text, TextAlignmentOptions.Left);
                MenuUi.Place(row.label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(230f, 38f));
                row.back = MenuUi.Image("Back", rt, theme.pixel, new Color(1f, 1f, 1f, 0.12f));
                MenuUi.Place(row.back.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(inner - 240f - 70f, 14f));
                row.fill = MenuUi.Image("Fill", row.back.rectTransform, theme.pixel, theme.accent);
                row.fill.rectTransform.anchorMin = Vector2.zero;
                row.fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                row.fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                row.fill.rectTransform.offsetMin = row.fill.rectTransform.offsetMax = Vector2.zero;
                row.value = MenuUi.Text("Value", rt, "0", 24f, theme.text, TextAlignmentOptions.Right);
                MenuUi.Place(row.value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(64f, 38f));
                _rows[i] = row;
                y -= 44f;
            }

            y -= 8f;
            Divider(statsRoot, x, y, inner);
            y -= 18f;
            _perk = Line(statsRoot, "PerkTitle", 28f, theme.accent, FontStyles.Normal, x, ref y, inner, 36f);
            _perkText = Line(statsRoot, "PerkText", 24f, theme.text, FontStyles.Normal, x, ref y, inner, 70f);
            _weapon = Line(statsRoot, "WeaponTitle", 28f, theme.accent, FontStyles.Normal, x, ref y, inner, 36f);
            _weaponText = Line(statsRoot, "WeaponText", 24f, theme.text, FontStyles.Normal, x, ref y, inner, 80f);
            foreach (TMP_Text t in new[] { _perkText, _weaponText })
            {
                t.alignment = TextAlignmentOptions.TopLeft;
                t.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        static TMP_Text Line(RectTransform parent, string name, float size, Color color, FontStyles style, float x, ref float y, float width, float height)
        {
            TextMeshProUGUI t = MenuUi.Text(name, parent, "", size, color, TextAlignmentOptions.TopLeft, style);
            MenuUi.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            y -= height;
            return t;
        }

        static void Divider(RectTransform parent, float x, float y, float width)
        {
            Image line = MenuUi.Divider("Divider", parent, width);
            MenuUi.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), line.rectTransform.sizeDelta);
        }

        void OnEnable()
        {
            Build();
            _shown = null;
            Refresh(true);
        }

        void Move(int direction)
        {
            stage.Step(direction);
            _fade = 0f;
            Refresh(false);
            if (UiParticles.Instance != null && confirmButton != null)
                UiParticles.Instance.BurstAt(confirmButton.transform as RectTransform, 6, new Color(0.9f, 1f, 0.7f, 1f));
        }

        void Confirm()
        {
            MenuStage.Slot slot = stage.Focus;
            if (slot == null || slot.stats == null || !slot.stats.playable) return;
            GameSession.SelectedCharacterId = slot.stats.id;
            stage.Punch(1.6f);
            Refresh(false);
            controller.OnCharacterConfirmed();
        }

        void Update()
        {
            if (!controller.IsScreen(MenuScreen.Characters) || controller.IsBusy) return;

            int direction = 0;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.pageDownKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) direction = 1;
                if (kb.pageUpKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) direction = -1;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.rightShoulder.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame) direction = 1;
                if (pad.leftShoulder.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame) direction = -1;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll < -0.5f) direction = 1;
                else if (scroll > 0.5f) direction = -1;
            }
            if (direction != 0) Move(direction);

            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime * 5f);
            if (_statsGroup != null) _statsGroup.alpha = Mathf.SmoothStep(0.15f, 1f, _fade);

            MenuStage.Slot focus = stage.Focus;
            if (focus != null && focus.stats != _shown) Refresh(false);

            foreach (Row row in _rows)
            {
                if (row == null) continue;
                row.amount.Step(Time.unscaledDeltaTime, 260f, 17f);
                RectTransform fill = row.fill.rectTransform;
                fill.anchorMax = new Vector2(Mathf.Clamp01(row.amount.value), 1f);
            }
        }

        void Refresh(bool instant)
        {
            if (!_built) return;
            MenuStage.Slot slot = stage != null ? stage.Focus : null;
            if (slot == null || slot.stats == null) return;
            CharacterStats s = slot.stats;
            _shown = s;
            MenuTheme theme = MenuTheme.Instance;
            bool playable = s.playable;
            bool current = playable && s == GameSession.SelectedStats;
            Color main = playable ? theme.text : Color.Lerp(theme.text, theme.danger, 0.55f);
            main.a = playable ? 1f : 0.7f;

            _name.text = s.displayName;
            _name.color = playable ? theme.text : theme.danger;
            _status.text = !playable ? "Bientôt disponible" : current ? "Personnage actuel" : "Disponible";
            _status.color = playable ? theme.accent : theme.danger;
            _hp.text = (s.maxHp > 0 ? "PV  " + s.maxHp + " / " + s.maxHp : "PV  —") + (string.IsNullOrEmpty(s.bonusHp) ? "" : "   +  " + s.bonusHp);
            _hp.color = main;

            for (int i = 0; i < Stats.Length; i++)
            {
                float v = Stats[i].get(s);
                Row row = _rows[i];
                row.value.text = v.ToString("0.##", CultureInfo.InvariantCulture);
                row.amount.target = Mathf.Clamp01(v / Stats[i].max);
                if (instant) row.amount.Snap(row.amount.target);
                row.label.color = main;
                row.value.color = main;
                row.fill.color = playable ? theme.accent : theme.danger * new Color(1f, 1f, 1f, 0.7f);
            }

            _perk.text = "Atout : " + (string.IsNullOrEmpty(s.perkTitle) ? "—" : s.perkTitle);
            _perkText.text = s.perkText;
            _weapon.text = "Arme : " + (string.IsNullOrEmpty(s.weaponName) ? "—" : s.weaponName);
            _weaponText.text = s.weaponText;
            _perk.color = _weapon.color = playable ? theme.accent : theme.danger;
            _perkText.color = _weaponText.color = main;

            if (confirmButton != null)
            {
                confirmButton.Locked = !playable;
                confirmButton.Text = !playable ? "Bientôt disponible" : current ? "Sélectionné" : "Choisir";
                if (playable && !current) confirmButton.Pop(10f);
            }
        }
    }
}
