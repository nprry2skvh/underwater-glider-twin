using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnderwaterGliderTwin.Editor
{
    public static class BuildWindows
    {
        public static readonly string[] BuildScenePaths = { "Assets/Scenes/Main.unity" };

        public static void Build()
        {
            var projectRoot = Path.GetFullPath(".");
            var workspaceRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            var outputDirectory = Path.GetFullPath(Path.Combine(workspaceRoot, "Builds", "UnderwaterGliderTwin"));
            var buildsRoot = Path.GetDirectoryName(outputDirectory);
            Directory.CreateDirectory(buildsRoot);
            var stagingDirectory = Path.Combine(buildsRoot, "UnderwaterGliderTwin.staging-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);

            var sourceArtifactRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "Models", "XGBoost"));
            var outputPath = Path.Combine(stagingDirectory, "UnderwaterGliderTwin.exe");

            var options = new BuildPlayerOptions
            {
                scenes = BuildScenePaths,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            try
            {
                if (!BuildArtifactValidator.TryValidateSource(sourceArtifactRoot, out var manifest, out var sourceError))
                {
                    throw new Exception("Build source artifact validation failed: " + sourceError);
                }

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new Exception($"Build failed: {report.summary.result}");
                }

                CopyModelArtifacts(sourceArtifactRoot, stagingDirectory, manifest);
                var stagedArtifactRoot = Path.Combine(stagingDirectory, "Models", "XGBoost");
                if (!BuildArtifactValidator.TryValidateOutput(stagedArtifactRoot, manifest, out var outputError))
                {
                    throw new Exception("Build output artifact validation failed: " + outputError);
                }

                PublishDirectoryWithRollback(
                    stagingDirectory,
                    outputDirectory,
                    () =>
                    {
                        var finalArtifactRoot = Path.Combine(outputDirectory, "Models", "XGBoost");
                        if (!BuildArtifactValidator.TryValidateOutput(finalArtifactRoot, manifest, out var finalError))
                        {
                            throw new Exception("Published artifact validation failed: " + finalError);
                        }
                    });
            }
            catch
            {
                DeleteDirectoryIfExists(stagingDirectory);
                throw;
            }
        }

        private static void CopyModelArtifacts(string sourceArtifactRoot, string outputDirectory, BuildArtifactManifest manifest)
        {
            var destinationArtifactRoot = Path.Combine(outputDirectory, "Models", "XGBoost");
            Directory.CreateDirectory(destinationArtifactRoot);
            CopyRequiredFile(
                Path.Combine(sourceArtifactRoot, "manifest.json"),
                Path.Combine(destinationArtifactRoot, "manifest.json"));

            foreach (var file in manifest.files)
            {
                CopyRequiredFile(
                    Path.Combine(sourceArtifactRoot, file.path.Replace('/', Path.DirectorySeparatorChar)),
                    Path.Combine(destinationArtifactRoot, file.path.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        private static void CopyRequiredFile(string sourceFile, string destinationFile)
        {
            if (!File.Exists(sourceFile))
            {
                throw new FileNotFoundException("Required build artifact file is missing.", sourceFile);
            }

            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFile, destinationFile, true);
        }

        private static void PublishDirectoryWithRollback(string stagingDirectory, string destinationDirectory, Action validatePublishedDirectory)
        {
            var parent = Path.GetDirectoryName(destinationDirectory);
            Directory.CreateDirectory(parent);
            var backupDirectory = destinationDirectory + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N");
            var hasBackup = false;
            var publishedNewDirectory = false;

            try
            {
                if (Directory.Exists(destinationDirectory))
                {
                    Directory.Move(destinationDirectory, backupDirectory);
                    hasBackup = true;
                }

                Directory.Move(stagingDirectory, destinationDirectory);
                publishedNewDirectory = true;
                validatePublishedDirectory?.Invoke();
            }
            catch
            {
                if (publishedNewDirectory)
                {
                    DeleteDirectoryIfExists(destinationDirectory);
                }

                if (hasBackup && Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, destinationDirectory);
                }

                throw;
            }

            if (hasBackup)
            {
                try
                {
                    DeleteDirectoryIfExists(backupDirectory);
                }
                catch (Exception cleanupError)
                {
                    UnityEngine.Debug.LogWarning(
                        "Windows build published successfully, but the previous output could not be deleted. " +
                        "The backup was preserved at '" + backupDirectory + "'. Details: " + cleanupError.Message);
                }
            }
        }

        private static void DeleteDirectoryIfExists(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
