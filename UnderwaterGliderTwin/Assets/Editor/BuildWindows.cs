using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnderwaterGliderTwin.Editor
{
    public static class BuildWindows
    {
        public static void Build()
        {
            var projectRoot = Path.GetFullPath(".");
            var workspaceRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            var outputDirectory = Path.GetFullPath(Path.Combine(workspaceRoot, "Builds", "UnderwaterGliderTwin"));
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

            CopyModelArtifacts(workspaceRoot, outputDirectory);
        }

        private static void CopyModelArtifacts(string workspaceRoot, string outputDirectory)
        {
            var sourceRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "Models"));
            if (!Directory.Exists(sourceRoot))
            {
                return;
            }

            var destinationRoot = Path.Combine(outputDirectory, "Models");
            CopyDirectoryIfPresent(Path.Combine(sourceRoot, "XGBoost"), Path.Combine(destinationRoot, "XGBoost"));
            CopyFileIfPresent(Path.Combine(sourceRoot, "training_summary.json"), Path.Combine(destinationRoot, "training_summary.json"));
        }

        private static void CopyDirectoryIfPresent(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            Directory.CreateDirectory(destinationDirectory);
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                CopyFileIfPresent(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
            }

            foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectoryIfPresent(subDirectory, Path.Combine(destinationDirectory, Path.GetFileName(subDirectory)));
            }
        }

        private static void CopyFileIfPresent(string sourceFile, string destinationFile)
        {
            if (!File.Exists(sourceFile))
            {
                return;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFile, destinationFile, true);
        }
    }
}
