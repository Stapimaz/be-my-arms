var type = System.AppDomain.CurrentDomain.GetAssemblies()
    .Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Build.BuildCommand"))
    .First(t => t != null);
return type.GetMethod("Build", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .Invoke(null, new object[]
    {
        "StandaloneWindows64", "Builds/Pass2Redo/BeMyArms.exe", "",
        new[] { "Development", "DetailedBuildReport" },
        new[] { "Assets/Scenes/M7MainMenu.unity", "Assets/Scenes/M7DuelArena.unity", "Assets/Scenes/M7TwoVsTwoArena.unity" },
        true, false
    });
