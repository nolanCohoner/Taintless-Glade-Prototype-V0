using UnityEngine;
using UnityEngine.SceneManagement;

namespace TaintlessGlade
{
    /// <summary>
    /// Choix du joueur d'une scène à l'autre : personnage et salle (le personnage est mémorisé entre deux lancements).
    /// Au chargement d'une salle, applique le personnage choisi (stats, et visuel / arme s'ils sont renseignés) au joueur.
    /// </summary>
    public static class GameSession
    {
        public const string MenuSceneName = "MainMenu";
        const string CharacterKey = "session.character";
        const string RoomKey = "session.room";

        static CharacterRoster _roster;
        static RoomCatalog _rooms;

        public static CharacterRoster Roster => _roster != null ? _roster : (_roster = Resources.Load<CharacterRoster>("CharacterRoster"));
        public static RoomCatalog Rooms => _rooms != null ? _rooms : (_rooms = Resources.Load<RoomCatalog>("RoomCatalog"));

        public static string SelectedRoomId
        {
            get => PlayerPrefs.GetString(RoomKey, "");
            set => PlayerPrefs.SetString(RoomKey, value ?? "");
        }

        public static string SelectedCharacterId
        {
            get => PlayerPrefs.GetString(CharacterKey, "");
            set
            {
                PlayerPrefs.SetString(CharacterKey, value ?? "");
                PlayerPrefs.Save();
                SelectionChanged?.Invoke();
            }
        }

        public static event System.Action SelectionChanged;

        /// <summary>Personnage choisi (toujours jouable ; retombe sur le premier jouable).</summary>
        public static CharacterStats SelectedStats
        {
            get
            {
                CharacterRoster roster = Roster;
                if (roster == null) return null;
                CharacterStats stats = roster.Find(SelectedCharacterId);
                return stats != null && stats.playable ? stats : roster.FirstPlayable();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _roster = null;
            _rooms = null;
            SelectionChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == MenuSceneName) return;
            GameCursor.Set(GameCursor.Kind.Game);
            Time.timeScale = 1f;

            var player = Object.FindFirstObjectByType<PlayerController>();
            CharacterStats stats = SelectedStats;
            if (player == null || stats == null) return;
            ApplyTo(player, stats);
        }

        /// <summary>Applique les stats du personnage ; remplace le visuel / le projectile si le personnage en définit.</summary>
        public static void ApplyTo(PlayerController player, CharacterStats stats)
        {
            player.stats = stats;

            if (stats.projectilePrefab != null)
            {
                var attack = player.GetComponent<PlayerAttack>();
                if (attack != null) attack.projectilePrefab = stats.projectilePrefab;
            }

            if (stats.visualPrefab != null)
            {
                Transform old = player.transform.Find("Visual");
                if (old != null) Object.Destroy(old.gameObject);
                GameObject visual = Object.Instantiate(stats.visualPrefab, player.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                player.spritePunch = visual.GetComponent<ScalePunch>();
                var driver = player.GetComponent<PlayerAnimationDriver>();
                if (driver != null) driver.characterAnimator = visual.GetComponentInChildren<CharacterAnimator>();
            }
        }
    }
}
