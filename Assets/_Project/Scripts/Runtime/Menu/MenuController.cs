using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TaintlessGlade
{
    public enum MenuScreen { Main, Characters, Rooms, Settings }

    /// <summary>
    /// Chef d'orchestre du menu de démarrage : enchaîne les écrans (principal, personnage, salles, paramètres),
    /// pilote le feuillage 2D, la caméra de la scène 3D et lance la partie. Échap / B revient en arrière.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [Header("Scène")]
        public MenuStage stage;
        public FoliageFrame foliage;
        public CanvasGroup fade;

        [Header("Titre")]
        public MenuPanel titlePanel;
        public RectTransform titleRect;

        [Header("Écrans")]
        public MenuPanel mainPanel;
        public MenuPanel charactersPanel;
        public MenuPanel roomsPanel;
        public MenuPanel settingsPanel;
        public CharacterSelectView characterView;
        public RoomSelectView roomView;
        public SettingsView settingsView;

        [Header("Boutons du menu principal")]
        public MenuButton playButton;
        public MenuButton charactersButton;
        public MenuButton settingsButton;
        public MenuButton quitButton;
        [Tooltip("Boutons « Retour » des sous-menus.")]
        public MenuButton[] backButtons;

        [Header("Feuillage : retrait par écran (0 = fermé, 1 = très dégagé)")]
        [Range(0f, 1f)] public float foliageMain = 0f;
        [Range(0f, 1f)] public float foliageCharacters = 0.65f;
        [Range(0f, 1f)] public float foliageRooms = 0.5f;
        [Range(0f, 1f)] public float foliageSettings = 0.6f;

        MenuScreen _screen = MenuScreen.Main;
        bool _loading;
        Spring _fadeSpring;
        float _titleSeed;

        public bool IsBusy => _loading || (settingsView != null && settingsView.IsBusy);
        public bool IsScreen(MenuScreen screen) => _screen == screen;

        void Awake()
        {
            Time.timeScale = 1f;
            _titleSeed = Random.value * 10f;
        }

        void Start()
        {
            EnsureEventSystem();
            GameCursor.Set(GameCursor.Kind.Menu);

            playButton.Clicked += () => Go(MenuScreen.Rooms);
            charactersButton.Clicked += () => Go(MenuScreen.Characters);
            settingsButton.Clicked += () => Go(MenuScreen.Settings);
            quitButton.Clicked += Quit;
            foreach (MenuButton b in backButtons) if (b != null) b.Clicked += Back;

            charactersPanel.HideInstant();
            roomsPanel.HideInstant();
            settingsPanel.HideInstant();
            _fadeSpring.Snap(1f);
            _fadeSpring.target = 0f;

            _screen = MenuScreen.Main;
            mainPanel.Show(0.35f);
            titlePanel.Show(0.15f);
            foliage.SetOpen(foliageMain);
            StartCoroutine(SelectNextFrame(playButton));
        }

        static void EnsureEventSystem()
        {
            EventSystem es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es = go.GetComponent<EventSystem>();
            }
            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
        }

        IEnumerator SelectNextFrame(Selectable target)
        {
            yield return null;
            if (target != null && EventSystem.current != null && target.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        // ------------------------------------------------------------------ Navigation entre écrans

        public void Go(MenuScreen screen)
        {
            if (_loading || screen == _screen) return;
            MenuScreen previous = _screen;
            _screen = screen;

            if (previous == MenuScreen.Settings) settingsView.Discard();

            PanelFor(previous).Hide();
            MenuPanel next = PanelFor(screen);
            next.Show(0.12f);

            if (screen == MenuScreen.Main) titlePanel.Show(0.2f); else titlePanel.Hide();

            stage.OpenSelection(screen == MenuScreen.Characters);
            if (screen == MenuScreen.Main) stage.ShowCharacter(stage.IndexOfSelected());
            stage.Punch(1f);

            foliage.SetOpen(OpenFor(screen));
            foliage.Rustle(1f);
            if (UiParticles.Instance != null)
                UiParticles.Instance.BurstScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 18, new Color(0.7f, 1f, 0.6f, 1f));

            StartCoroutine(SelectNextFrame(FirstFor(screen)));
        }

        MenuPanel PanelFor(MenuScreen s)
        {
            switch (s)
            {
                case MenuScreen.Characters: return charactersPanel;
                case MenuScreen.Rooms: return roomsPanel;
                case MenuScreen.Settings: return settingsPanel;
                default: return mainPanel;
            }
        }

        float OpenFor(MenuScreen s)
        {
            switch (s)
            {
                case MenuScreen.Characters: return foliageCharacters;
                case MenuScreen.Rooms: return foliageRooms;
                case MenuScreen.Settings: return foliageSettings;
                default: return foliageMain;
            }
        }

        Selectable FirstFor(MenuScreen s)
        {
            switch (s)
            {
                case MenuScreen.Characters: return characterView.confirmButton;
                case MenuScreen.Rooms: return roomView.FirstButton;
                case MenuScreen.Settings: return settingsView.FirstButton;
                default: return playButton;
            }
        }

        public void Back()
        {
            if (IsBusy) return;
            if (_screen != MenuScreen.Main) Go(MenuScreen.Main);
        }

        public void Rustle(float strength) { if (foliage != null) foliage.Rustle(strength); }

        public void OnCharacterConfirmed() => StartCoroutine(ReturnAfterConfirm());

        IEnumerator ReturnAfterConfirm()
        {
            if (UiParticles.Instance != null)
                UiParticles.Instance.BurstScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), 30, new Color(0.8f, 1f, 0.5f, 1f));
            foliage.Rustle(1.6f);
            yield return new WaitForSecondsRealtime(0.55f);
            if (_screen == MenuScreen.Characters && !_loading) Go(MenuScreen.Main);
        }

        // ------------------------------------------------------------------ Lancer la partie / quitter

        public void StartGame(RoomCatalog.Room room)
        {
            if (_loading || room == null || string.IsNullOrEmpty(room.sceneName)) return;
            StartCoroutine(StartRoutine(room));
        }

        IEnumerator StartRoutine(RoomCatalog.Room room)
        {
            _loading = true;
            GameSession.SelectedRoomId = room.id;
            GameCursor.Set(GameCursor.Kind.Busy);
            PanelFor(_screen).Hide();
            titlePanel.Hide();
            stage.Punch(2f);
            foliage.SetOpen(0f);
            foliage.Close();

            yield return new WaitForSecondsRealtime(0.55f);
            _fadeSpring.target = 1f;
            yield return new WaitForSecondsRealtime(0.4f);

            AsyncOperation op = SceneManager.LoadSceneAsync(room.sceneName);
            while (op != null && !op.isDone) yield return null;
        }

        void Quit()
        {
            if (_loading) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Boucle

        void Update()
        {
            bool cancel = false;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) cancel = true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) cancel = true;
            if (cancel) Back();

            // Fondu d'entrée / de sortie (ressort : léger rebond évité en bornant l'alpha).
            if (fade != null)
            {
                _fadeSpring.Step(Time.unscaledDeltaTime, 90f, 18f);
                fade.alpha = Mathf.Clamp01(_fadeSpring.value);
                fade.blocksRaycasts = fade.alpha > 0.02f && _loading;
            }

            // Si la sélection est perdue (clic dans le vide), on la redonne au clavier / à la manette dès qu'on les utilise.
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && !_loading)
            {
                bool nav = (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame))
                           || (Gamepad.current != null && Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f);
                if (nav)
                {
                    Selectable first = FirstFor(_screen);
                    if (first != null) es.SetSelectedGameObject(first.gameObject);
                }
            }

            if (titleRect != null)
            {
                float t = Time.unscaledTime;
                float rot = Mathf.Sin(t * 0.9f + _titleSeed) * 1.1f + (Mathf.PerlinNoise(t * 0.8f, _titleSeed) - 0.5f) * 1.6f;
                float s = 1f + Mathf.Sin(t * 1.7f + _titleSeed) * 0.015f;
                titleRect.localRotation = Quaternion.Euler(0f, 0f, rot);
                titleRect.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
