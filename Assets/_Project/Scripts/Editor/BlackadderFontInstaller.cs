using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Tools > Taintless Glade > Appliquer la police Blackadder :
    /// crée l'asset TMP "Blackadder ITC SDF", le définit comme police par défaut de TextMeshPro
    /// puis l'applique (en gras) à tous les textes TMP existants des prefabs et des scènes du projet.
    /// Ne reconstruit pas la salle de test.
    /// </summary>
    public static class BlackadderFontInstaller
    {
        const string FontPath = "Assets/_Project/Fonts/BlackadderITC.ttf";
        const string FontAssetPath = "Assets/_Project/Fonts/Blackadder ITC SDF.asset";
        const string TmpSettingsSearch = "t:TMP_Settings";

        [MenuItem("Tools/Taintless Glade/Appliquer la police Blackadder")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            TMP_FontAsset fontAsset = GetOrCreateFontAsset();
            if (fontAsset == null) return;

            SetAsDefaultFont(fontAsset);

            int count = 0;
            count += ApplyToPrefabs(fontAsset);
            count += ApplyToScenes(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Blackadder] Police appliquée en gras à {count} texte(s). Police TMP par défaut mise à jour.");
        }

        static TMP_FontAsset GetOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            AssetDatabase.ImportAsset(FontPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                EditorUtility.DisplayDialog("Police introuvable", "Le fichier " + FontPath + " est introuvable.", "OK");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Erreur", "Impossible de créer l'asset de police TextMeshPro.", "OK");
                return null;
            }
            asset.name = "Blackadder ITC SDF";

            AssetDatabase.CreateAsset(asset, FontAssetPath);
            if (asset.material != null)
            {
                asset.material.name = "Blackadder ITC SDF Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            if (asset.atlasTextures != null)
            {
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    asset.atlasTextures[i].name = "Blackadder ITC SDF Atlas " + i;
                    AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
                }
            }

            // Caractères absents de Blackadder : retombe sur la police TMP d'origine.
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (fallback != null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void SetAsDefaultFont(TMP_FontAsset fontAsset)
        {
            string[] guids = AssetDatabase.FindAssets(TmpSettingsSearch);
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Blackadder] TMP Settings introuvable : importe les TMP Essential Resources pour changer la police par défaut.");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }

        static int ApplyToPrefabs(TMP_FontAsset fontAsset)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/TextMesh Pro")) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int n = ApplyToTexts(root.GetComponentsInChildren<TMP_Text>(true), fontAsset);
                if (n > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count += n;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            return count;
        }

        static int ApplyToScenes(TMP_FontAsset fontAsset)
        {
            int count = 0;
            string previousScene = SceneManager.GetActiveScene().path;

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var texts = new List<TMP_Text>();
                foreach (GameObject root in scene.GetRootGameObjects())
                    texts.AddRange(root.GetComponentsInChildren<TMP_Text>(true));

                int n = ApplyToTexts(texts.ToArray(), fontAsset);
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    count += n;
                }
            }

            if (!string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            return count;
        }

        static int ApplyToTexts(TMP_Text[] texts, TMP_FontAsset fontAsset)
        {
            int count = 0;
            foreach (TMP_Text tmp in texts)
            {
                if (tmp == null) continue;

                Material previous = tmp.fontSharedMaterial;
                bool customMaterial = previous != null && tmp.font != null && previous != tmp.font.material;

                tmp.font = fontAsset;
                if (customMaterial)
                {
                    // Matériau personnalisé (contour, overlay...) : on garde ses réglages mais on le branche sur l'atlas Blackadder.
                    RetargetMaterial(previous, fontAsset.material);
                    tmp.fontSharedMaterial = previous;
                }

                tmp.fontStyle |= FontStyles.Bold;
                EditorUtility.SetDirty(tmp);
                count++;
            }
            return count;
        }

        static readonly int[] FontFloatProps =
        {
            Shader.PropertyToID("_GradientScale"), Shader.PropertyToID("_TextureWidth"), Shader.PropertyToID("_TextureHeight"),
            Shader.PropertyToID("_WeightNormal"), Shader.PropertyToID("_WeightBold"),
            Shader.PropertyToID("_ScaleRatioA"), Shader.PropertyToID("_ScaleRatioB"), Shader.PropertyToID("_ScaleRatioC"),
        };

        static void RetargetMaterial(Material target, Material fontMaterial)
        {
            if (target == null || fontMaterial == null) return;

            target.SetTexture(ShaderUtilities.ID_MainTex, fontMaterial.GetTexture(ShaderUtilities.ID_MainTex));
            foreach (int id in FontFloatProps)
                if (target.HasProperty(id) && fontMaterial.HasProperty(id))
                    target.SetFloat(id, fontMaterial.GetFloat(id));
            EditorUtility.SetDirty(target);
        }
    }
}
