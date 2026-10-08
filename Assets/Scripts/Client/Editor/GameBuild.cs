using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>
    /// Builds the normal playable game: main menu (scene 0) + both production arenas. The same build
    /// acts as the client and, launched with server args, as the local dedicated server.
    /// </summary>
    public static class GameBuild
    {
        public const string OutputPath = "Builds/Windows/BeMyArms.exe";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/DuelArena.unity",
            "Assets/Scenes/TwoVsTwoArena.unity"
        };

        [MenuItem("Be My Arms/Client/Build Playable Game")]
        public static void BuildWindowsPlayer()
        {
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            // The Pipeline runtime build processor only bakes its Resources config when it can tell
            // the build is a development build, and for a *scripted* build (BuildPlayer called
            // directly, not through the pipeline `build` command) its only signal is the
            // EditorUserBuildSettings.development toggle. This build always passes
            // BuildOptions.Development, so mirror that in the toggle for the duration of the build
            // and restore it afterwards — otherwise the player ships with the Pipeline assemblies
            // but no config, and the runtime QA server never starts.
            bool previousDevelopment = EditorUserBuildSettings.development;
            EditorUserBuildSettings.development = true;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                EditorUserBuildSettings.development = previousDevelopment;
            }

            Debug.Log($"[Build] game result={report.summary.result} size={report.summary.totalSize} output={OutputPath}");
        }
    }
}
