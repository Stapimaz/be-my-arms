using System.Collections.Generic;
using System.IO;
using BeMyArms.M7;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>Builds the player-facing main menu scene (scene 0 of the game build).</summary>
    public static class M7MenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M7MainMenu.unity";

        [MenuItem("Be My Arms/M7/Build Menu Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var app = new GameObject("App");
            app.AddComponent<M7AppBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            MoveSceneToFrontInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[M7] built {ScenePath}");
        }

        static void MoveSceneToFrontInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
