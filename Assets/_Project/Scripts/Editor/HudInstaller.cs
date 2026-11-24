using System.IO;
using UnityEditor;
using UnityEngine;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Crée / complète Resources/HudConfig.asset avec le sprite HUDFrame du dossier Art/HUD.
    /// Se lance tout seul au chargement de l'éditeur ; le menu force la réassignation.
    /// </summary>
    [InitializeOnLoad]
    public static class HudInstaller
    {
        const string FramePath = "Assets/_Project/Art/HUD/HUDFrame.png";
        const string HeartPath = "Assets/_Project/Art/HUD/heartFrameNormal.png";
        const string ReflectionDir = "Assets/_Project/Art/HUD/HeartFrameEffectFrames";
        const string AssetDir = "Assets/_Project/Resources";
        const string AssetPath = AssetDir + "/HudConfig.asset";

        static HudInstaller()
        {
            EditorApplication.delayCall += () => Install(false);
        }

        [MenuItem("Taintless Glade/Réassigner la config du HUD")]
        static void Reinstall() => Install(true);

        /// <summary>heartFrameReflectionF1..F9, triées par numéro.</summary>
        static Sprite[] LoadReflection()
        {
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { ReflectionDir }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null) list.Add(sprite);
            }
            list.Sort((a, b) => FrameNumber(a.name).CompareTo(FrameNumber(b.name)));
            return list.ToArray();
        }

        static int FrameNumber(string name)
        {
            int end = name.Length;
            int start = end;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            return start < end && int.TryParse(name.Substring(start), out int n) ? n : 0;
        }

        static bool SameSprites(Sprite[] a, Sprite[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void Install(bool force)
        {
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            if (frame == null)
            {
                if (force) Debug.LogWarning("[Taintless Glade] HUDFrame introuvable ou pas importé en Sprite : " + FramePath);
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<HudConfig>(AssetPath);
            bool created = config == null;
            if (created)
            {
                Directory.CreateDirectory(AssetDir);
                config = ScriptableObject.CreateInstance<HudConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
            }

            var heart = AssetDatabase.LoadAssetAtPath<Sprite>(HeartPath);
            Sprite[] reflection = LoadReflection();
            bool heartUpToDate = config.heartFrame == heart && SameSprites(config.reflectionFrames, reflection);
            if (!created && !force && config.frame == frame && heartUpToDate) return;

            config.frame = frame;
            config.heartFrame = heart;
            config.reflectionFrames = reflection;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[Taintless Glade] Config du HUD " + (created ? "créée" : "mise à jour") + " : " + AssetPath);
        }
    }
}
