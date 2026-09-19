using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.M2.EditorTools
{
    /// <summary>Builds a development Windows player for the M2 dedicated-server/client runs.</summary>
    public static class M2Build
    {
        const string ScenePath = "Assets/Scenes/M2NetworkingSpike.unity";
        const string OutputPath = "Builds/M2/M2.exe";

        [MenuItem("Be My Arms/M2/Build M2 Player")]
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
            Debug.Log($"[M2Build] result={report.summary.result} size={report.summary.totalSize} output={OutputPath}");
        }
    }
}
