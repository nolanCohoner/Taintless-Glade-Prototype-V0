using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TaintlessGlade.Tests.EditMode
{
    public class ProjectIntegrityTests
    {
        const string ProjectFolder = "Assets/_Project";

        static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        }

        [Test]
        public void Build_settings_contain_at_least_one_enabled_scene()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled));
        }

        [Test]
        public void Every_scene_in_build_settings_exists_on_disk()
        {
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
                Assert.IsTrue(File.Exists(scene.path), $"Scène du build introuvable : {scene.path}");
        }

        [Test]
        public void Project_prefabs_have_no_missing_scripts()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProjectFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                Assert.AreEqual(0, CountMissingScripts(prefab), $"Script manquant dans le prefab {path}");
            }
        }

        [Test]
        public void Build_scenes_have_no_missing_scripts()
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        Assert.AreEqual(0, CountMissingScripts(root), $"Script manquant dans {entry.path} ({root.name})");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
