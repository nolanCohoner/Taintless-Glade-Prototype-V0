using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Quelle que soit la scène ouverte dans l'éditeur, le mode Play démarre toujours sur le menu de démarrage (MainMenu),
    /// comme dans le jeu final. Désactivable avec Tools > Taintless Glade > Menu > Toujours lancer le jeu depuis le menu.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        public const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        const string MenuPath = "Tools/Taintless Glade/Menu/Toujours lancer le jeu depuis le menu";
        const string PrefKey = "TaintlessGlade.AlwaysStartFromMenu";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayModeStartScene()
        {
            EditorApplication.delayCall += Apply;
        }

        /// <summary>(Re)configure la scène de départ du mode Play ; à appeler après avoir créé la scène du menu.</summary>
        public static void Apply()
        {
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
            EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            Debug.Log(Enabled ? "[Menu] Le mode Play démarre maintenant toujours sur le menu de démarrage." : "[Menu] Le mode Play démarre sur la scène ouverte.");
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
