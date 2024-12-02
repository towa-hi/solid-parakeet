using UnityEditor;

public class BuildScript
{
    public static void PerformBuild()
    {
        string[] scenes = { "Assets/Scenes/MainScene.unity" }; // Add your scenes here.
        string buildPath = "Builds/MyGame.exe"; // Default path.
        BuildTarget target = BuildTarget.StandaloneWindows; // Default target.

        // Read arguments.
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-buildPath" && i + 1 < args.Length)
                buildPath = args[i + 1];
            if (args[i] == "-buildTarget" && i + 1 < args.Length)
                target = (BuildTarget)System.Enum.Parse(typeof(BuildTarget), args[i + 1]);
        }

        // Build.
        BuildPipeline.BuildPlayer(scenes, buildPath, target, BuildOptions.None);
    }
}
