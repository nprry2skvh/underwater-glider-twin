using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnderwaterGliderTwin.Editor
{
    public static class BuildWindows
    {
        public static void Build()
        {
            var outputDirectory = Path.GetFullPath(Path.Combine("..", "Builds", "UnderwaterGliderTwin"));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "UnderwaterGliderTwin.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Build failed: {report.summary.result}");
            }
        }
    }
}
