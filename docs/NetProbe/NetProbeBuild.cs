using System.Collections.Generic;
using System.IO;
using BeMyArms.NetProbe;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.NetProbe.EditorTools
{
    /// <summary>Builds the minimal NGO/UTP connection-lifecycle probe scene and player.</summary>
    public static class NetProbeBuild
    {
        const string ScenePath = "Assets/Scenes/NetProbe.unity";
        const string OutputPath = "Builds/NetProbe/NetProbe.exe";

        [MenuItem("Be My Arms/NetProbe/Build Probe Player")]
        public static void BuildWindowsPlayer()
        {
            EnsureScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[NetProbeBuild] result={report.summary.result} output={OutputPath}");
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("NetworkManager");
            go.AddComponent<UnityTransport>();
            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig.EnableSceneManagement = false;
            go.AddComponent<NetProbeBootstrap>().Manager = manager;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
