using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Ajoute (ou met à jour) un Full Screen Pass Renderer Feature "ToonOutline" sur tous les
    /// Universal Renderer Data du projet (PC et Mobile). Équivaut à "Add Renderer Feature" dans l'inspecteur.
    /// </summary>
    public static class ToonOutlineInstaller
    {
        const string FeatureName = "ToonOutline";
        const string MaterialPath = "Assets/_Project/Art/Materials/M_ToonOutline.mat";

        [MenuItem("Tools/Taintless Glade/Installer le contour cartoon")]
        public static void Install()
        {
            Material material = GetOrCreateMaterial();
            if (material == null) return;

            int installed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                FullScreenPassRendererFeature feature = null;
                foreach (ScriptableRendererFeature existing in data.rendererFeatures)
                {
                    if (existing is FullScreenPassRendererFeature fullScreen && existing.name == FeatureName)
                        feature = fullScreen;
                }

                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
                    AssetDatabase.AddObjectToAsset(feature, data);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

                    var serialized = new SerializedObject(data);
                    SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                    SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                    features.arraySize++;
                    features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    serialized.ApplyModifiedProperties();
                }

                feature.passMaterial = material;
                feature.passIndex = 0;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
                feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
                feature.fetchColorBuffer = true;
                feature.SetActive(true);

                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
                data.SetDirty();
                installed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Taintless Glade] Contour cartoon installé sur {installed} renderer(s). Réglages : {MaterialPath}");
        }

        static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("TaintlessGlade/Toon Outline");
            if (shader == null)
            {
                Debug.LogError("[Taintless Glade] Shader TaintlessGlade/Toon Outline introuvable — vérifie la Console.");
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
