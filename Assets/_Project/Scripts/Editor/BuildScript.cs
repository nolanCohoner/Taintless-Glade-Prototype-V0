using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Build Windows 64 bits du jeu (scènes cochées dans les Build Settings). Utilisé par la CI :
    /// Unity.exe -quit -batchmode -executeMethod TaintlessGlade.EditorTools.BuildScript.BuildWindows -buildOutput dossier -buildVersion 1.2.3
    /// Le menu Tools > Taintless Glade > Build > Windows 64 bits fait la même chose en local (dossier Builds/).
    /// </summary>
    public static class BuildScript
    {
        const string ExecutableName = "TaintlessGlade.exe";

        /// <summary>Point d'entrée de la CI : le code de sortie du processus indique le résultat.</summary>
        public static void BuildWindows()
        {
            string output = GetArgument("-buildOutput") ?? "Builds/TaintlessGlade";
            string version = GetArgument("-buildVersion");
            bool ok = Build(output, version);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Tools/Taintless Glade/Build/Windows 64 bits")]
        static void BuildFromMenu()
        {
            if (Build("Builds/TaintlessGlade", null))
                EditorUtility.RevealInFinder(Path.GetFullPath("Builds/TaintlessGlade/" + ExecutableName));
        }

        static bool Build(string outputFolder, string version)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] Aucune scène cochée dans les Build Settings.");
                return false;
            }

            AlignUrpAssetVersions();

            string previousVersion = PlayerSettings.bundleVersion;
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputFolder, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            PlayerSettings.bundleVersion = previousVersion;

            BuildSummary summary = report.summary;
            Debug.LogWarning($"[Build] {summary.result} : {summary.totalSize / (1024 * 1024)} Mo, {summary.totalErrors} erreur(s), {summary.totalWarnings} avertissement(s) -> {outputFolder}");
            return summary.result == BuildResult.Succeeded;
        }

        /// <summary>
        /// Unity refuse de construire un exe si les assets URP ne sont pas « à la dernière version » de l'éditeur
        /// (ex. réglages enregistrés par une version d'Unity plus récente). En CI, aucun humain ne peut cliquer sur « Upgrade » :
        /// on aligne le numéro de version en mémoire, sans sauvegarder ni modifier les assets du projet.
        /// </summary>
        static void AlignUrpAssetVersions()
        {
            const string assembly = "Unity.RenderPipelines.Universal.Runtime";
            AlignVersion(Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, " + assembly), "t:UniversalRenderPipelineGlobalSettings");
            AlignVersion(Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " + assembly), "t:UniversalRenderPipelineAsset");
        }

        static void AlignVersion(Type type, string filter)
        {
            if (type == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo last = type.GetField("k_LastVersion", flags);
            FieldInfo current = type.GetField("m_AssetVersion", flags);
            if (last == null || current == null) return;
            object lastVersion = last.GetRawConstantValue();

            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                if (asset == null || Equals(current.GetValue(asset), lastVersion)) continue;
                Debug.LogWarning($"[Build] {asset.name} : version {current.GetValue(asset)} alignée sur {lastVersion} (en mémoire).");
                current.SetValue(asset, lastVersion);
            }
        }

        static string GetArgument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
