using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds the normal playable game: main menu (scene 0) + both production arenas. The same build
    /// acts as the client and, launched with server args, as the local dedicated server.
    /// </summary>
    public static class M7GameBuild
    {
        public const string OutputPath = "Builds/M7/BeMyArms.exe";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/M7MainMenu.unity",
            "Assets/Scenes/M7DuelArena.unity",
            "Assets/Scenes/M7TwoVsTwoArena.unity"
        };

        [MenuItem("Be My Arms/M7/Build Playable Game")]
        public static void BuildWindowsPlayer()
        {
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[M7Build] game result={report.summary.result} size={report.summary.totalSize} output={OutputPath}");
        }
    }
}
