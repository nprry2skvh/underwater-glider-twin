using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnderwaterGliderTwin.Editor;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class BuildArtifactValidatorTests
    {
        private string tempDirectory;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(tempDirectory) && Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void BuildValidator_RejectsMissingCopiedModelFile()
        {
            var source = CreateArtifactRoot(accepted: true);
            Assert.That(BuildArtifactValidator.TryValidateSource(source, out var manifest, out var sourceError), Is.True, sourceError);

            var output = CopyDirectoryToTemp(source);
            File.Delete(Path.Combine(output, "models", "east_displacement_m.json"));

            Assert.That(BuildArtifactValidator.TryValidateOutput(output, manifest, out var error), Is.False);
            Assert.That(error, Does.Contain("models/east_displacement_m.json"));
        }

        [Test]
        public void BuildValidator_RejectsRejectedSourceArtifact()
        {
            var source = CreateArtifactRoot(accepted: false);

            Assert.That(BuildArtifactValidator.TryValidateSource(source, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("accepted"));
        }

        [Test]
        public void BuildValidator_AcceptsCopiedOutputWithMatchingHashes()
        {
            var source = CreateArtifactRoot(accepted: true);
            Assert.That(BuildArtifactValidator.TryValidateSource(source, out var manifest, out var sourceError), Is.True, sourceError);
            var output = CopyDirectoryToTemp(source);

            Assert.That(BuildArtifactValidator.TryValidateOutput(output, manifest, out var outputError), Is.True, outputError);
        }

        private string CreateArtifactRoot(bool accepted)
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "build-artifact-validator-" + Guid.NewGuid().ToString("N"));
            var models = Path.Combine(tempDirectory, "models");
            Directory.CreateDirectory(models);
            var schemaPath = Path.Combine(tempDirectory, "feature_schema.json");
            var modelPath = Path.Combine(models, "east_displacement_m.json");
            File.WriteAllText(schemaPath, "{\"feature_names\":[\"depth_m\"],\"mean\":[0],\"scale\":[1]}");
            File.WriteAllText(modelPath, "{\"tree_count\":1,\"trees\":[]}");
            File.WriteAllText(Path.Combine(tempDirectory, "manifest.json"), string.Format(
                "{{\"artifact_schema_version\":1,\"validation_status\":\"{0}\",\"training_run_id\":\"build-test-run\",\"files\":[{{\"path\":\"feature_schema.json\",\"sha256\":\"{1}\",\"size_bytes\":{2}}},{{\"path\":\"models/east_displacement_m.json\",\"sha256\":\"{3}\",\"size_bytes\":{4}}}]}}",
                accepted ? "accepted" : "rejected",
                ComputeSha256(schemaPath),
                new FileInfo(schemaPath).Length,
                ComputeSha256(modelPath),
                new FileInfo(modelPath).Length));
            return tempDirectory;
        }

        private static string CopyDirectoryToTemp(string source)
        {
            var destination = Path.Combine(Path.GetTempPath(), "build-artifact-output-" + Guid.NewGuid().ToString("N"));
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var destinationFile = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(file, destinationFile, overwrite: true);
            }

            return destination;
        }

        private static string ComputeSha256(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var bytes = sha256.ComputeHash(stream);
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
