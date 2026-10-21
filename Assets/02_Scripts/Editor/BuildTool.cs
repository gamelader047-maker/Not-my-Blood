using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Build: fertige Spiel-Apps für Windows (Surface) und Mac.
    /// Die Builds landen im Projektordner unter "Builds/" (wird nicht auf GitHub hochgeladen).
    /// Mitspieler brauchen kein Unity – nur den Ordner bzw. die App.
    /// </summary>
    public static class BuildTool
    {
        private const string BuildRoot = "Builds";

        [MenuItem("Tools/Not My Blood/Build/Windows (Surface)")]
        public static void BuildWindows()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                EditorUtility.DisplayDialog("Build",
                    "Windows Build Support fehlt.\nUnity Hub → Installs → Zahnrad → Add modules → \"Windows Build Support (Mono)\".", "OK");
                return;
            }
            Build(BuildTarget.StandaloneWindows64, Path.Combine(BuildRoot, "Windows", "NotMyBlood.exe"));
        }

        [MenuItem("Tools/Not My Blood/Build/Mac")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, Path.Combine(BuildRoot, "Mac", "Not My Blood.app"));
        }

        private static void Build(BuildTarget target, string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            NetworkSetupBuilder.ApplyPlayerSettings();
            // Mono: Windows-Builds lassen sich so auch auf dem Mac erstellen
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }
            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Build", "Keine Szene in den Build Settings. Erst \"Multiplayer einrichten\" ausführen.", "OK");
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] {target} fertig ({summary.totalSize / (1024 * 1024)} MB): {Path.GetFullPath(path)}");
                EditorUtility.RevealInFinder(Path.GetFullPath(path));
            }
            else
            {
                Debug.LogError($"[Build] {target} fehlgeschlagen: {summary.result}, {summary.totalErrors} Fehler. Details in der Console.");
            }
        }
    }
}
