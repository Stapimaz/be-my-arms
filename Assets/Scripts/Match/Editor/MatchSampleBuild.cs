using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.Match.EditorTools
{
    /// <summary>Builds a development Windows player for the Match dedicated-server/4-client Duel run.</summary>
    public static class MatchSampleBuild
    {
        const string ScenePath = "Assets/Scenes/Samples/MatchHarness.unity";
        const string OutputPath = "Builds/Match/Match.exe";

        [MenuItem("Be My Arms/Match/Build Match Player")]
        public static void BuildWindowsPlayer()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] result={report.summary.result} size={report.summary.totalSize} output={OutputPath}");
        }
    }
}
