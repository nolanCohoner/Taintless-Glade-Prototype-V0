using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Tools > Taintless Glade > Activer les ombres des torches :
    /// active les ombres temps réel sur toutes les lumières ponctuelles dont le nom commence par "TorchLight" (y compris "TorchLight (3)") des scènes du projet
    /// (piliers, murs et props projettent alors leur ombre sur le sol), et agrandit l'atlas d'ombres des lumières
    /// supplémentaires de l'asset URP PC pour qu'il contienne toutes les faces des lumières ponctuelles.
    /// Ne reconstruit pas la salle de test.
    /// </summary>
    public static class TorchShadowInstaller
    {
        const string TorchLightName = "TorchLight";
        const string PcRenderPipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
        const int AdditionalShadowAtlasSize = 4096;

        // Réglages appliqués à chaque lumière de torche (aussi utilisés par TestRoomBuilder).
        public static void Configure(Light light)
        {
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.4f;
            light.shadowNearPlane = 0.2f;
            light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier =
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
        }

        [MenuItem("Tools/Taintless Glade/Activer les ombres des torches")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnlargeShadowAtlas();

            int count = 0;
            string previousScene = SceneManager.GetActiveScene().path;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                int n = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    {
                        if (light.type != UnityEngine.LightType.Point || !light.name.StartsWith(TorchLightName)) continue;
                        Configure(light);
                        EditorUtility.SetDirty(light);
                        n++;
                    }
                }

                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    count += n;
                }
            }

            if (!string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Torch Shadows] Ombres activées sur {count} torche(s). Atlas d'ombres des lumières supplémentaires : {AdditionalShadowAtlasSize}.");
        }

        static void EnlargeShadowAtlas()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcRenderPipelineAssetPath);
            if (asset == null)
            {
                Debug.LogWarning("[Torch Shadows] " + PcRenderPipelineAssetPath + " introuvable : augmente à la main l'atlas d'ombres des lumières supplémentaires.");
                return;
            }

            var so = new SerializedObject(asset);
            SerializedProperty resolution = so.FindProperty("m_AdditionalLightsShadowmapResolution");
            if (resolution != null && resolution.intValue < AdditionalShadowAtlasSize)
            {
                resolution.intValue = AdditionalShadowAtlasSize;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
