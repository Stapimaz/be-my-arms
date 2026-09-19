using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeMyArms.M3.EditorTools
{
    /// <summary>Builds a development Windows player for the M3 dedicated-server/4-client Duel run.</summary>
    public static class M3DuelBuild
    {
        const string ScenePath = "Assets/Scenes/M3Duel.unity";
        const string OutputPath = "Builds/M3/M3.exe";

        [MenuItem("Be My Arms/M3/Build M3 Player")]
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
            Debug.Log($"[M3Build] result={report.summary.result} size={report.summary.totalSize} output={OutputPath}");
        }
    }
}
