using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Prediction;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class XGBoostArtifactValidatorTests
    {
        private readonly List<string> directoriesToDelete = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var directory in directoriesToDelete)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

            directoriesToDelete.Clear();
        }

        [Test]
        public void ValidatorType_IsAvailableToRuntimeCode()
        {
            var validatorType = typeof(XGBoostArtifact).Assembly.GetType("UnderwaterGliderTwin.Prediction.XGBoostArtifactValidator");

            Assert.That(validatorType, Is.Not.Null);
        }

        [Test]
        public void Validator_ExposesStructuralValidationContract()
        {
            var method = typeof(XGBoostArtifactValidator).GetMethod("TryValidateStructure");

            Assert.That(method, Is.Not.Null);
        }

        [Test]
        public void Validator_RejectsMissingManifest()
        {
            var root = CreateTempModelsRoot(includeManifest: false, accepted: true, schemaVersion: 1);

            Assert.That(XGBoostArtifactValidator.TryValidateStructure(root, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("manifest"));
        }

        [Test]
        public void Validator_RejectsRejectedArtifact()
        {
            var root = CreateTempModelsRoot(includeManifest: true, accepted: false, schemaVersion: 1);

            Assert.That(XGBoostArtifactValidator.TryValidateStructure(root, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("accepted"));
        }

        [Test]
        public void Validator_RejectsMissingModelFile()
        {
            var root = CreateTempModelsRoot(includeManifest: true, accepted: true, schemaVersion: 1);
            File.Delete(Path.Combine(root, "models", "east_displacement_m.json"));

            Assert.That(XGBoostArtifactValidator.TryValidateStructure(root, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("missing"));
        }

        [Test]
        public void Validator_RejectsCorruptManifestJson()
        {
            var root = CreateTempModelsRoot(includeManifest: true, accepted: true, schemaVersion: 1);
            File.WriteAllText(Path.Combine(root, "manifest.json"), "{ this is not valid JSON }");

            Assert.That(XGBoostArtifactValidator.TryValidateStructure(root, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("JSON"));
        }

        [Test]
        public void Validator_RejectsUnsupportedSchemaVersion()
        {
            var root = CreateTempModelsRoot(includeManifest: true, accepted: true, schemaVersion: 2);

            Assert.That(XGBoostArtifactValidator.TryValidateStructure(root, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("schema version"));
        }

        [Test]
        public void Validator_RejectsFeatureSchemaHashMismatch()
        {
            var root = CreateTempModelsRoot(includeManifest: true, accepted: true, schemaVersion: 1);
            File.WriteAllText(Path.Combine(root, "feature_schema.json"), "{\"feature_names\":[\"changed\"],\"mean\":[0],\"scale\":[1]}");

            Assert.That(XGBoostArtifactValidator.TryValidateStructure(root, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("hash"));
        }

        [Test]
        public void Validator_AcceptsCompleteArtifactWithMatchingHashes()
        {
            var root = CreateTempModelsRoot(includeManifest: true, accepted: true, schemaVersion: 1);

            var valid = XGBoostArtifactValidator.TryValidateStructure(root, out var report, out var error);

            Assert.That(valid, Is.True, error);
            Assert.That(report.training_run_id, Is.EqualTo("test-run-42"));
            Assert.That(report.files, Has.Length.EqualTo(2));
        }

        [Test]
        public void TryCreatePredictor_LoadsRepositoryKnownGoodArtifact()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Models", "XGBoost"));

            var loaded = XGBoostArtifactValidator.TryCreatePredictor(root, out var predictor, out var error);

            try
            {
                Assert.That(loaded, Is.True, error);
                Assert.That(predictor, Is.Not.Null);
            }
            finally
            {
                predictor?.Release();
            }
        }

        private string CreateTempModelsRoot(bool includeManifest, bool accepted, int schemaVersion)
        {
            var root = Path.Combine(Path.GetTempPath(), "xgboost-validator-" + Guid.NewGuid().ToString("N"));
            var models = Path.Combine(root, "models");
            Directory.CreateDirectory(models);
            directoriesToDelete.Add(root);
            var schemaPath = Path.Combine(root, "feature_schema.json");
            var modelPath = Path.Combine(models, "east_displacement_m.json");
            File.WriteAllText(schemaPath, "{\"feature_names\":[\"depth_m\"],\"mean\":[0],\"scale\":[1]}");
            File.WriteAllText(modelPath, "{\"tree_count\":1,\"trees\":[]}");

            if (includeManifest)
            {
                File.WriteAllText(Path.Combine(root, "manifest.json"), string.Format(
                    "{{\"artifact_schema_version\":{0},\"validation_status\":\"{1}\",\"training_run_id\":\"test-run-42\",\"files\":[{{\"path\":\"feature_schema.json\",\"sha256\":\"{2}\",\"size_bytes\":{3}}},{{\"path\":\"models/east_displacement_m.json\",\"sha256\":\"{4}\",\"size_bytes\":{5}}}]}}",
                    schemaVersion,
                    accepted ? "accepted" : "rejected",
                    ComputeSha256(schemaPath),
                    new FileInfo(schemaPath).Length,
                    ComputeSha256(modelPath),
                    new FileInfo(modelPath).Length));
            }

            return root;
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
