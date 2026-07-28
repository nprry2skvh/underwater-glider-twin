using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace UnderwaterGliderTwin.Editor
{
    [Serializable]
    public sealed class BuildArtifactManifest
    {
        public int artifact_schema_version;
        public string validation_status;
        public string training_run_id;
        public BuildArtifactManifestFile[] files;
    }

    [Serializable]
    public sealed class BuildArtifactManifestFile
    {
        public string path;
        public string sha256;
        public long size_bytes;
    }

    public static class BuildArtifactValidator
    {
        private const int SupportedArtifactSchemaVersion = 1;

        public static bool TryValidateSource(string sourceRoot, out BuildArtifactManifest manifest, out string error)
        {
            manifest = null;
            error = string.Empty;
            if (!TryReadManifest(sourceRoot, out manifest, out error))
            {
                return false;
            }

            return TryValidateFileSet(sourceRoot, manifest, out error);
        }

        public static bool TryValidateOutput(string outputRoot, BuildArtifactManifest manifest, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot))
            {
                error = "XGBoost output artifact root is missing.";
                return false;
            }

            if (manifest == null)
            {
                error = "XGBoost artifact manifest is missing.";
                return false;
            }

            if (!TryReadManifest(outputRoot, out var outputManifest, out error))
            {
                return false;
            }

            if (!ManifestMatches(manifest, outputManifest))
            {
                error = "XGBoost output manifest does not match the validated source manifest.";
                return false;
            }

            return TryValidateFileSet(outputRoot, manifest, out error);
        }

        private static bool ManifestMatches(BuildArtifactManifest expected, BuildArtifactManifest actual)
        {
            if (expected.artifact_schema_version != actual.artifact_schema_version
                || !string.Equals(expected.validation_status, actual.validation_status, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.training_run_id, actual.training_run_id, StringComparison.Ordinal))
            {
                return false;
            }

            if (expected.files == null || actual.files == null || expected.files.Length != actual.files.Length)
            {
                return false;
            }

            var actualByPath = new Dictionary<string, BuildArtifactManifestFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in actual.files)
            {
                if (file == null || !actualByPath.TryAdd(file.path, file))
                {
                    return false;
                }
            }

            foreach (var file in expected.files)
            {
                if (file == null || !actualByPath.TryGetValue(file.path, out var actualFile)
                    || !string.Equals(file.sha256, actualFile.sha256, StringComparison.OrdinalIgnoreCase)
                    || file.size_bytes != actualFile.size_bytes)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadManifest(string root, out BuildArtifactManifest manifest, out string error)
        {
            manifest = null;
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
                manifest = JsonUtility.FromJson<BuildArtifactManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception)
            {
                error = "XGBoost manifest JSON is invalid.";
                return false;
            }

            if (manifest == null || (manifest.artifact_schema_version == 0
                && string.IsNullOrWhiteSpace(manifest.validation_status)
                && string.IsNullOrWhiteSpace(manifest.training_run_id)
                && manifest.files == null))
            {
                error = "XGBoost manifest JSON is invalid.";
                return false;
            }

            if (manifest.artifact_schema_version != SupportedArtifactSchemaVersion)
            {
                error = "XGBoost artifact schema version is unsupported.";
                return false;
            }

            if (!string.Equals(manifest.validation_status, "accepted", StringComparison.OrdinalIgnoreCase))
            {
                error = "XGBoost artifact validation status must be accepted.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.training_run_id))
            {
                error = "XGBoost artifact training_run_id is missing.";
                return false;
            }

            if (manifest.files == null || manifest.files.Length == 0)
            {
                error = "XGBoost artifact file set is missing.";
                return false;
            }

            return true;
        }

        private static bool TryValidateFileSet(string root, BuildArtifactManifest manifest, out string error)
        {
            error = string.Empty;
            var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.files)
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
