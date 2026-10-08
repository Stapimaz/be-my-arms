using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>
    /// Builds development players for the Client production arenas so the networked Duel/2v2 loop can be
    /// verified on the real maps (dedicated server + clients).
    /// </summary>
    public static class ArenaBuild
    {
        const string DuelScene = "Assets/Scenes/DuelArena.unity";
        const string TwoVsTwoScene = "Assets/Scenes/TwoVsTwoArena.unity";

        [MenuItem("Be My Arms/Client/Build Arena Players")]
        public static void BuildAll()
        {
            BuildDuel();
            BuildTwoVsTwo();
        }

        public static void BuildDuel() => Build(DuelScene, "Builds/Windows/DuelArena.exe");

        public static void BuildTwoVsTwo() => Build(TwoVsTwoScene, "Builds/Windows/TwoVsTwoArena.exe");

        static void Build(string scene, string output)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] {scene} result={report.summary.result} size={report.summary.totalSize} output={output}");
        }
    }
}
