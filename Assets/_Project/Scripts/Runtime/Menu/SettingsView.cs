using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Menu des paramètres : contrôles (réaffectation clavier / souris / manette), affichage, audio et jeu.
    /// Les changements s'appliquent en direct ; « Enregistrer » les conserve, « Retour » les annule, « Réinitialiser » remet l'onglet par défaut.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        public MenuController controller;
        public RectTransform panel;

        const float RowH = 56f;
        static readonly string[] TabNames = { "Contrôles", "Affichage", "Audio", "Jeu" };

        class BindCell
        {
            public MenuButton button;
            public Image icon;
            public ControlRow row;
            public BindSlot slot;
        }

        readonly List<MenuButton> _tabs = new List<MenuButton>();
        readonly List<BindCell> _cells = new List<BindCell>();
        RectTransform _content;
        TMP_Text _toast;
        float _toastTimer;
        int _tab;
        bool _built;
        PlayerInputSet _input;
        InputActionRebindingExtensions.RebindingOperation _op;
        BindCell _rebindingCell;
        bool _clearRequested;
        float _rebindCooldownUntil;
        bool _savedNavigation;

        public bool IsBusy => _op != null || Time.unscaledTime < _rebindCooldownUntil;
        public MenuButton FirstButton => _tabs.Count > 0 ? _tabs[0] : null;

        void Awake() => Build();

        void OnEnable()
        {
            Build();
            _input?.Dispose();
            _input = new PlayerInputSet();
            ShowTab(_tab);
        }

        void OnDisable()
        {
            CancelRebind(false);
            _input?.Dispose();
            _input = null;
        }

        // ------------------------------------------------------------------ Construction

        void Build()
        {
            if (_built || panel == null) return;
            _built = true;
            MenuTheme theme = MenuTheme.Instance;
            Vector2 size = panel.sizeDelta;

            TextMeshProUGUI title = MenuUi.Text("Title", panel, "Paramètres", 56f, theme.text, TextAlignmentOptions.Left, FontStyles.Bold);
            MenuUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -26f), new Vector2(700f, 72f));

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                MenuButton tab = MenuButton.Create("Tab_" + i, panel, TabNames[i], new Vector2(290f, 58f), 32f, false, TextAlignmentOptions.Center);
                tab.slide = 0f;
                MenuUi.Place(tab.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f + i * 304f, -112f), new Vector2(290f, 58f));
                tab.label.rectTransform.offsetMin = new Vector2(8f, 0f);
                tab.Clicked += () => ShowTab(index);
                _tabs.Add(tab);
            }

            Image line = MenuUi.Divider("TabLine", panel, size.x - 112f);
            MenuUi.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -186f), line.rectTransform.sizeDelta);

            _content = MenuUi.Rect("Content", panel);
            MenuUi.Place(_content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -206f), new Vector2(size.x - 112f, size.y - 206f - 120f));

            string[] names = { "Enregistrer", "Réinitialiser", "Retour" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                MenuButton b = MenuButton.Create("Action_" + i, panel, names[i], new Vector2(300f, 62f), 32f, true, TextAlignmentOptions.Center);
                b.slide = 0f;
                MenuUi.Place(b.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(56f + i * 320f, 28f), new Vector2(300f, 62f));
                b.label.rectTransform.offsetMin = new Vector2(8f, 0f);
                b.Clicked += () => { if (index == 0) Save(); else if (index == 1) ResetTab(); else controller.Back(); };
            }

            _toast = MenuUi.Text("Toast", panel, "", 28f, theme.accent, TextAlignmentOptions.Right);
            MenuUi.Place(_toast.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-56f, 40f), new Vector2(520f, 44f));
        }

        // ------------------------------------------------------------------ Onglets

        void ShowTab(int index)
        {
            if (!_built) return;
            CancelRebind(false);
            _tab = index;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].SetAlwaysBorder(i == index);
                _tabs[i].SetTextColor(i == index ? MenuTheme.Instance.accent : MenuTheme.Instance.text);
            }
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            _cells.Clear();

            switch (index)
            {
                case 0: BuildControls(); break;
                case 1: BuildDisplay(); break;
                case 2: BuildAudio(); break;
                default: BuildGameplay(); break;
            }
            controller.Rustle(0.5f);
        }

        void Save()
        {
            GameSettings.Save();
            _input?.Save();
            Toast("Réglages enregistrés");
            controller.Rustle(0.8f);
        }

        void ResetTab()
        {
            switch (_tab)
            {
                case 0: _input?.ResetToDefaults(); break;
                case 1: GameSettings.ResetDisplay(); break;
                case 2: GameSettings.ResetAudio(); break;
                default: GameSettings.ResetGameplay(); break;
            }
            ShowTab(_tab);
            Toast("Valeurs par défaut (pense à enregistrer)");
        }

        /// <summary>Annule tout ce qui n'a pas été enregistré (appelé en quittant l'écran).</summary>
        public void Discard()
        {
            CancelRebind(false);
            GameSettings.Revert();
            _input?.Revert();
        }

        void Toast(string message)
        {
            _toast.text = message;
            _toastTimer = 2.4f;
        }

        void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                Color c = _toast.color;
                c.a = Mathf.Clamp01(_toastTimer * 2f);
                _toast.color = c;
            }
            if (_rebindingCell != null)
            {
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                _rebindingCell.button.Text = blink ? "Appuie sur une touche…" : "";
            }
        }

        // ------------------------------------------------------------------ Contrôles

        void BuildControls()
        {
            MenuTheme theme = MenuTheme.Instance;
            float[] columns = { 470f, 740f, 1010f };
            string[] heads = { "Principal", "Secondaire", "Manette" };
            for (int i = 0; i < heads.Length; i++)
            {
                TextMeshProUGUI h = MenuUi.Text("Head_" + i, _content, heads[i], 26f, theme.textDim, TextAlignmentOptions.Center);
                MenuUi.Place(h.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(columns[i], 0f), new Vector2(250f, 36f));
            }

            float y = -40f;
            foreach (ControlRow row in _input.Rows)
            {
                TextMeshProUGUI label = MenuUi.Text("Label", _content, row.label, 30f, theme.text, TextAlignmentOptions.Left);
                MenuUi.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y - 4f), new Vector2(460f, RowH - 8f));
                label.verticalAlignment = VerticalAlignmentOptions.Middle;

                for (int s = 0; s < 3; s++)
                {
                    var slot = (BindSlot)s;
                    var cell = new BindCell { row = row, slot = slot };
                    MenuButton b = MenuButton.Create("Bind_" + s, _content, "", new Vector2(250f, RowH - 8f), 28f, true, TextAlignmentOptions.Center);
                    b.slide = 0f;
                    b.hoverScale = 0.04f;
                    MenuUi.Place(b.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(columns[s], y - 4f), new Vector2(250f, RowH - 8f));
                    b.label.rectTransform.offsetMin = new Vector2(8f, 0f);
                    cell.button = b;

                    cell.icon = MenuUi.Image("Icon", b.content, null, Color.white);
                    MenuUi.Place(cell.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(40f, 40f));
                    cell.icon.preserveAspect = true;

                    bool locked = row.bindingIndex[s] < 0 || (slot == BindSlot.Gamepad && !string.IsNullOrEmpty(row.lockedGamepadLabel));
                    if (locked) b.Locked = true;
                    BindCell captured = cell;
                    b.Clicked += () => BeginRebind(captured);
                    _cells.Add(cell);
                    RefreshCell(cell);
                }
                y -= RowH;
            }

            TextMeshProUGUI hint = MenuUi.Text("Hint", _content, "Clique sur une case puis appuie sur la nouvelle touche.   Retour arrière : effacer   ·   Échap : annuler", 22f, theme.textDim, TextAlignmentOptions.Left);
            MenuUi.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, -4f), new Vector2(1300f, 32f));
        }

        void RefreshCell(BindCell cell)
        {
            if (cell.row.bindingIndex[(int)cell.slot] < 0 || (cell.slot == BindSlot.Gamepad && !string.IsNullOrEmpty(cell.row.lockedGamepadLabel)))
            {
                cell.icon.enabled = false;
                cell.button.Text = cell.row.bindingIndex[(int)cell.slot] < 0 && string.IsNullOrEmpty(cell.row.lockedGamepadLabel) ? "—" : cell.row.lockedGamepadLabel ?? "—";
                cell.button.label.rectTransform.offsetMin = new Vector2(8f, 0f);
                return;
            }

            Glyph g = InputGlyphs.Describe(PlayerInputSet.EffectivePath(cell.row, cell.slot));
            cell.icon.sprite = g.icon;
            cell.icon.enabled = g.icon != null && !g.empty;
            cell.button.Text = g.label;
            cell.button.label.rectTransform.offsetMin = new Vector2(cell.icon.enabled ? 58f : 8f, 0f);
            cell.button.label.alignment = TextAlignmentOptions.Center;
            cell.button.label.color = _input != null && _input.IsOverridden(cell.row, cell.slot) ? MenuTheme.Instance.accent : MenuTheme.Instance.text;
            cell.button.SetTextColor(cell.button.label.color);
        }

        void BeginRebind(BindCell cell)
        {
            if (_op != null || Time.unscaledTime < _rebindCooldownUntil) return;
            int index = cell.row.bindingIndex[(int)cell.slot];
            if (index < 0) return;

            _rebindingCell = cell;
            _clearRequested = false;
            cell.icon.enabled = false;
            cell.button.label.rectTransform.offsetMin = new Vector2(8f, 0f);

            EventSystem es = EventSystem.current;
            if (es != null) { _savedNavigation = es.sendNavigationEvents; es.sendNavigationEvents = false; }

            _op = cell.row.action.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnPotentialMatch(op =>
                {
                    if (op.selectedControl is KeyControl key && key.keyCode == Key.Backspace)
                    {
                        _clearRequested = true;
                        op.Cancel();
                    }
                })
                .OnComplete(op => EndRebind(true))
                .OnCancel(op => EndRebind(false));

            if (cell.slot == BindSlot.Gamepad) _op.WithControlsHavingToMatchPath("<Gamepad>");
            else
            {
                _op.WithControlsHavingToMatchPath("<Keyboard>");
                _op.WithControlsHavingToMatchPath("<Mouse>");
            }
            _op.Start();
        }

        void CancelRebind(bool refresh)
        {
            if (_op == null) return;
            _clearRequested = false;
            _op.Cancel();
            if (refresh && _rebindingCell != null) RefreshCell(_rebindingCell);
        }

        void EndRebind(bool completed)
        {
            BindCell cell = _rebindingCell;
            _op?.Dispose();
            _op = null;
            _rebindingCell = null;
            _rebindCooldownUntil = Time.unscaledTime + 0.3f;

            EventSystem es = EventSystem.current;
            if (es != null) es.sendNavigationEvents = true;

            if (cell == null) return;
            if (_clearRequested && _input != null) _input.SetPath(cell.row, cell.slot, "");
            _clearRequested = false;
            RefreshCell(cell);
            if (completed) controller.Rustle(0.5f);
        }

        // ------------------------------------------------------------------ Affichage / audio / jeu

        void BuildDisplay()
        {
            List<Vector2Int> resolutions = GameSettings.AvailableResolutions();
            var resNames = new string[resolutions.Count];
            int resIndex = 0;
            for (int i = 0; i < resolutions.Count; i++)
            {
                resNames[i] = resolutions[i].x + " × " + resolutions[i].y;
                if (resolutions[i].x == GameSettings.ResolutionWidth && resolutions[i].y == GameSettings.ResolutionHeight) resIndex = i;
            }

            float y = 0f;
            Selector(ref y, "Résolution", resNames, resIndex, i =>
            {
                GameSettings.ResolutionWidth = resolutions[i].x;
                GameSettings.ResolutionHeight = resolutions[i].y;
                GameSettings.Apply();
            });
            Selector(ref y, "Mode d'affichage", new[] { "Fenêtré", "Plein écran fenêtré", "Plein écran" }, (int)GameSettings.Mode, i =>
            {
                GameSettings.Mode = (GameSettings.WindowMode)i;
                GameSettings.Apply();
            });
            Selector(ref y, "Synchronisation verticale", new[] { "Désactivée", "Activée" }, GameSettings.VSync ? 1 : 0, i =>
            {
                GameSettings.VSync = i == 1;
                GameSettings.Apply();
            });
            Selector(ref y, "Qualité graphique", QualitySettings.names, GameSettings.QualityLevel, i =>
            {
                GameSettings.QualityLevel = i;
                GameSettings.Apply();
            });

            if (Application.isEditor)
                Hint(ref y, "La résolution et le mode d'affichage n'ont d'effet que dans le jeu compilé.");
        }

        void BuildAudio()
        {
            float y = 0f;
            Slider(ref y, "Volume général", GameSettings.MasterVolume, v => { GameSettings.MasterVolume = v; GameSettings.Apply(); });
            Slider(ref y, "Musique", GameSettings.MusicVolume, v => { GameSettings.MusicVolume = v; GameSettings.Apply(); });
            Slider(ref y, "Effets sonores", GameSettings.SfxVolume, v => { GameSettings.SfxVolume = v; GameSettings.Apply(); });
            Slider(ref y, "Interface", GameSettings.UiVolume, v => { GameSettings.UiVolume = v; GameSettings.Apply(); });
            Hint(ref y, "Les sons arriveront plus tard : seul le volume général est actif pour l'instant.");
        }

        void BuildGameplay()
        {
            float y = 0f;
            Slider(ref y, "Secousses de l'écran", GameSettings.ScreenShake, v => { GameSettings.ScreenShake = v; GameSettings.Apply(); });
            Selector(ref y, "Nombres de dégâts", new[] { "Masqués", "Affichés" }, GameSettings.DamageNumbers ? 1 : 0, i =>
            {
                GameSettings.DamageNumbers = i == 1;
                GameSettings.Apply();
            });
        }

        void Hint(ref float y, string message)
        {
            TextMeshProUGUI hint = MenuUi.Text("Hint", _content, message, 22f, MenuTheme.Instance.textDim, TextAlignmentOptions.Left);
            MenuUi.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y - 14f), new Vector2(1300f, 32f));
            y -= 46f;
        }

        TMP_Text RowLabel(string text, float y)
        {
            TextMeshProUGUI label = MenuUi.Text("Label", _content, text, 32f, MenuTheme.Instance.text, TextAlignmentOptions.Left);
            MenuUi.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y - 6f), new Vector2(560f, 60f));
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            return label;
        }

        void Selector(ref float y, string label, string[] options, int start, Action<int> onChanged)
        {
            MenuTheme theme = MenuTheme.Instance;
            RowLabel(label, y);
            int index = Mathf.Clamp(start, 0, options.Length - 1);

            MenuButton left = ArrowButton("Prev", theme.arrowLeft, 620f, y);
            TextMeshProUGUI value = MenuUi.Text("Value", _content, options[index], 30f, theme.text, TextAlignmentOptions.Center);
            MenuUi.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, y - 6f), new Vector2(440f, 60f));
            value.verticalAlignment = VerticalAlignmentOptions.Middle;
            MenuButton right = ArrowButton("Next", theme.arrowRight, 1150f, y);

            void Change(int direction)
            {
                index = (index + direction + options.Length) % options.Length;
                value.text = options[index];
                onChanged(index);
                controller.Rustle(0.3f);
            }
            left.Clicked += () => Change(-1);
            right.Clicked += () => Change(1);
            y -= 70f;
        }

        MenuButton ArrowButton(string name, Sprite icon, float x, float y)
        {
            MenuButton b = MenuButton.Create(name, _content, "", new Vector2(64f, 60f), 28f, true, TextAlignmentOptions.Center);
            b.slide = 0f;
            MenuUi.Place(b.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - 6f), new Vector2(64f, 60f));
            Image img = MenuUi.Image("Icon", b.content, icon, Color.white);
            MenuUi.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            img.preserveAspect = true;
            return b;
        }

        void Slider(ref float y, string label, float start, Action<float> onChanged)
        {
            MenuTheme theme = MenuTheme.Instance;
            RowLabel(label, y);

            RectTransform root = MenuUi.Rect("Slider", _content);
            MenuUi.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, y - 6f), new Vector2(540f, 60f));

            Image bg = MenuUi.Image("Background", root, theme.pixel, new Color(1f, 1f, 1f, 0.2f), raycast: true);
            StretchBar(bg.rectTransform, 0f, 16f);

            RectTransform fillArea = MenuUi.Rect("Fill Area", root);
            StretchBar(fillArea, 0f, 16f);
            Image fill = MenuUi.Image("Fill", fillArea, theme.pixel, theme.accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            // Le Slider étire la poignée sur toute la hauteur de sa zone : la zone fait donc la hauteur voulue de la poignée.
            RectTransform handleArea = MenuUi.Rect("Handle Slide Area", root);
            StretchBar(handleArea, 12f, 44f);
            Image handle = MenuUi.Image("Handle", handleArea, theme.pixel, Color.white, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(22f, 0f);

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            ColorBlock colors = slider.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.9f);
            colors.highlightedColor = theme.accent;
            colors.selectedColor = theme.accent;
            colors.pressedColor = Color.white;
            colors.fadeDuration = 0.08f;
            slider.colors = colors;

            TextMeshProUGUI value = MenuUi.Text("Value", _content, "", 30f, theme.text, TextAlignmentOptions.Right);
            MenuUi.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1180f, y - 6f), new Vector2(160f, 60f));
            value.verticalAlignment = VerticalAlignmentOptions.Middle;

            slider.SetValueWithoutNotify(Mathf.Clamp01(start));
            value.text = Mathf.RoundToInt(start * 100f) + " %";
            slider.onValueChanged.AddListener(v =>
            {
                value.text = Mathf.RoundToInt(v * 100f) + " %";
                onChanged(v);
            });
            y -= 70f;
        }

        /// <summary>Barre horizontale centrée verticalement dans son parent, étirée en largeur (marge horizontale en px).</summary>
        static void StretchBar(RectTransform rect, float margin, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(margin, -height * 0.5f);
            rect.offsetMax = new Vector2(-margin, height * 0.5f);
        }
    }
}
