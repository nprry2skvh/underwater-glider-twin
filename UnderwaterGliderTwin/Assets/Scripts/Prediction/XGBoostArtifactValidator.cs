using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    [Serializable]
    public sealed class XGBoostArtifactValidationReport
    {
        public int artifact_schema_version;
        public string validation_status;
        public string training_run_id;
        public XGBoostArtifactValidationFile[] files;
    }

    [Serializable]
    public sealed class XGBoostArtifactValidationFile
    {
        public string path;
        public string sha256;
        public long size_bytes;
    }

    public static class XGBoostArtifactValidator
    {
        private const int SupportedArtifactSchemaVersion = 1;

        public static bool TryValidateStructure(string root, out XGBoostArtifactValidationReport report, out string error)
        {
            report = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                error = "XGBoost artifact root is missing.";
                return false;
            }

            var manifestPath = Path.Combine(root, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                error = "XGBoost manifest.json is missing.";
                return false;
            }

            try
            {
                report = JsonUtility.FromJson<XGBoostArtifactValidationReport>(File.ReadAllText(manifestPath));
            }
            catch (Exception)
            {
                error = "XGBoost manifest JSON is invalid.";
                return false;
            }

            if (report == null || (report.artifact_schema_version == 0
                && string.IsNullOrWhiteSpace(report.validation_status)
                && string.IsNullOrWhiteSpace(report.training_run_id)
                && report.files == null))
            {
                error = "XGBoost manifest JSON is invalid.";
                return false;
            }

            if (report.artifact_schema_version != SupportedArtifactSchemaVersion)
            {
                error = "XGBoost artifact schema version is unsupported.";
                return false;
            }

            if (!string.Equals(report.validation_status, "accepted", StringComparison.OrdinalIgnoreCase))
            {
                error = "XGBoost artifact validation status must be accepted.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(report.training_run_id))
            {
                error = "XGBoost artifact training_run_id is missing.";
                return false;
            }

            if (report.files == null || report.files.Length == 0)
            {
                error = "XGBoost artifact file set is missing.";
                return false;
            }

            var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in report.files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.path) || !IsSafeRelativePath(file.path))
                {
                    error = "XGBoost artifact file path is invalid.";
                    return false;
                }

                if (!declaredPaths.Add(file.path))
                {
                    error = "XGBoost artifact file set contains a duplicate path.";
                    return false;
                }

                if (file.size_bytes < 0 || !IsSha256(file.sha256))
                {
                    error = "XGBoost artifact file metadata is invalid.";
                    return false;
                }

                var path = Path.Combine(root, file.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    error = "XGBoost artifact file is missing: " + file.path;
                    return false;
                }

                if (new FileInfo(path).Length != file.size_bytes)
                {
                    error = "XGBoost artifact file size does not match: " + file.path;
                    return false;
                }

                if (!string.Equals(ComputeSha256(path), file.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    error = "XGBoost artifact file hash does not match: " + file.path;
                    return false;
                }
            }

            return true;
        }

        public static bool TryCreatePredictor(string root, out IPredictor predictor, out string error)
        {
            predictor = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                error = "XGBoost artifact root is missing.";
                return false;
            }

            if (!TryValidateStructure(root, out _, out error))
            {
                return false;
            }

            if (!XGBoostArtifact.TryLoad(root, out _, out error))
            {
                return false;
            }

            var xgboost = new XGBoostPredictor();
            xgboost.LoadModel(root);
            if (!xgboost.IsReady)
            {
                xgboost.Release();
                error = "XGBoost predictor could not load the artifact.";
                return false;
            }

            predictor = xgboost;
            return true;
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (Path.IsPathRooted(path) || path.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return !string.Equals(path.Replace('\\', '/'), "manifest.json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ComputeSha256(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
