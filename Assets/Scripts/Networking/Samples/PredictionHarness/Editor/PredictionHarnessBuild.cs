using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.Networking.EditorTools
{
    /// <summary>Builds a development Windows player for the Networking dedicated-server/client runs.</summary>
    public static class PredictionHarnessBuild
    {
        const string ScenePath = "Assets/Scenes/Samples/PredictionHarness.unity";
        const string OutputPath = "Builds/Networking/Networking.exe";

        [MenuItem("Be My Arms/Networking/Build Networking Player")]
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
            Debug.Log($"[PredictionHarnessBuild] result={report.summary.result} size={report.summary.totalSize} output={OutputPath}");
        }
    }
}
