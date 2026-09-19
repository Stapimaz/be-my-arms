using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds development players for the M7 production arenas so the networked Duel/2v2 loop can be
    /// verified on the real maps (dedicated server + clients).
    /// </summary>
    public static class M7ArenaBuild
    {
        const string DuelScene = "Assets/Scenes/M7DuelArena.unity";
        const string TwoVsTwoScene = "Assets/Scenes/M7TwoVsTwoArena.unity";

        [MenuItem("Be My Arms/M7/Build Arena Players")]
        public static void BuildAll()
        {
            BuildDuel();
            BuildTwoVsTwo();
        }

        public static void BuildDuel() => Build(DuelScene, "Builds/M7/M7DuelArena.exe");

        public static void BuildTwoVsTwo() => Build(TwoVsTwoScene, "Builds/M7/M7TwoVsTwoArena.exe");

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
            Debug.Log($"[M7Build] {scene} result={report.summary.result} size={report.summary.totalSize} output={output}");
        }
    }
}
