using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace UnderwaterGliderTwin.Telemetry
{
    public interface IOceanCurrentFileConverter
    {
        void Convert(string inputPath, string outputPath, DateTime referenceTimeUtc, Action onCompleted, Action<string> onFailure, Action<string> onProgress);
    }

    public static class OceanCurrentFileLoader
    {
        public static IOceanCurrentFileConverter Converter { get; set; } = new PythonOceanCurrentFileConverter();
        public static float ConverterTimeoutSeconds { get; set; } = CopernicusCurrentClient.RequestTimeoutSeconds;

        public static bool TryLoad(string path, DateTime? referenceTimeUtc, out CopernicusCurrentResult result, out string error)
        {
            result = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "LocalFile input path is missing.";
                return false;
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path);
                if (!File.Exists(normalizedPath))
                {
                    error = "LocalFile file does not exist: " + normalizedPath;
                    return false;
                }

                if (string.Equals(Path.GetExtension(normalizedPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    result = CopernicusCurrentResponseParser.Parse(File.ReadAllText(normalizedPath));
                    return HasSpatialField(result, out error);
                }

                if (NetCdfClassicCurrentReader.TryRead(normalizedPath, referenceTimeUtc ?? DateTime.UtcNow, out result, out error))
                {
                    return HasSpatialField(result, out error);
                }

                if (!NetCdfClassicCurrentReader.IsUnsupportedFormat(error))
                {
                    error = "LocalFile parse failed for '" + normalizedPath + "': " + error;
                    return false;
                }

                if (Converter == null)
                {
                    error = "LocalFile NetCDF conversion is unavailable for '" + normalizedPath + "': " + error;
                    return false;
                }

                return false;
            }
            catch (Exception exception)
            {
                error = "LocalFile parse failed for '" + path + "': " + exception.Message;
                return false;
            }
        }

        public static System.Collections.IEnumerator Load(string path, DateTime referenceTimeUtc, Action<CopernicusCurrentResult> onSuccess, Action<string> onFailure, Action<string> onProgress = null)
        {
            if (TryLoad(path, referenceTimeUtc, out var directResult, out var directError)) { onSuccess?.Invoke(directResult); yield break; }
            if (!NetCdfClassicCurrentReader.IsUnsupportedFormat(directError)) { onFailure?.Invoke(directError); yield break; }
            var directory = Path.Combine(UnityEngine.Application.temporaryCachePath, "OceanCurrentConvert", Guid.NewGuid().ToString("N"));
            var stagingPath = Path.Combine(directory, "converted.staging.json");
            var responsePath = Path.Combine(directory, "converted.json");
            var completed = false;
            string conversionError = null;
            try
            {
                Directory.CreateDirectory(directory);
                // Converter callbacks may originate on a worker thread; only this coroutine calls Unity-facing callbacks.
                onProgress?.Invoke("Converting local NetCDF current file...");
                Converter.Convert(Path.GetFullPath(path), stagingPath, referenceTimeUtc, () => completed = true, value => conversionError = value, null);
                var deadline = DateTime.UtcNow.AddSeconds(ConverterTimeoutSeconds);
                while (!completed && string.IsNullOrWhiteSpace(conversionError) && DateTime.UtcNow < deadline) yield return null;
                if (!completed || !string.IsNullOrWhiteSpace(conversionError) || !File.Exists(stagingPath))
                {
                    onFailure?.Invoke("LocalFile converter failed for '" + path + "': " + (conversionError ?? "timed out or produced no output."));
                    yield break;
                }
                File.Move(stagingPath, responsePath);
                CopernicusCurrentResult converted;
                try { converted = CopernicusCurrentResponseParser.Parse(File.ReadAllText(responsePath)); }
                catch (Exception exception)
                {
                    onFailure?.Invoke("LocalFile converter produced invalid JSON: " + exception.Message);
                    yield break;
                }
                if (!HasSpatialField(converted, out var fieldError)) { onFailure?.Invoke(fieldError); yield break; }
                onSuccess?.Invoke(converted);
            }
            finally { CleanupDirectory(directory); }
        }

        private static bool HasSpatialField(CopernicusCurrentResult result, out string error)
        {
            if (result != null && result.Field != null && result.Field.Samples.Count > 0) { error = string.Empty; return true; }
            error = "LocalFile acquisition requires a non-empty spatial current field.";
            return false;
        }

        private static void CleanupDirectory(string directory)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!Directory.Exists(directory)) return;
                    Directory.Delete(directory, true);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
            }
            UnityEngine.Debug.LogWarning("Ocean current converter temporary directory could not be removed: " + directory);
        }

        public static OceanCurrentSourceIdentity CreateIdentity(string path, int schemaVersion)
        {
            var normalizedPath = Path.GetFullPath(path);
            using (var stream = File.OpenRead(normalizedPath))
            using (var algorithm = SHA256.Create())
            {
                var hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                return OceanCurrentSourceIdentity.ForLocalFile(normalizedPath, hash, schemaVersion);
            }
        }
    }

    internal sealed class PythonOceanCurrentFileConverter : IOceanCurrentFileConverter
    {
        public void Convert(string inputPath, string outputPath, DateTime referenceTimeUtc, Action onCompleted, Action<string> onFailure, Action<string> onProgress)
        {
            var scriptPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "CopernicusCurrentFetcher.py");
            if (!File.Exists(scriptPath)) { onFailure?.Invoke("Python converter script was not found."); return; }
            try
            {
                var configuredPython = Environment.GetEnvironmentVariable("COPERNICUS_PYTHON");
                var process = new Process { StartInfo = new ProcessStartInfo { FileName = string.IsNullOrWhiteSpace(configuredPython) ? "python" : configuredPython, Arguments = Quote(scriptPath) + " --convert " + Quote(inputPath) + " --output " + Quote(outputPath) + " --reference-time " + Quote(referenceTimeUtc.ToUniversalTime().ToString("o")), CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true } };
                process.Start();
                Task.Run(() =>
                {
                    try
                    {
                        if (!process.WaitForExit((int)(CopernicusCurrentClient.RequestTimeoutSeconds * 1000f)))
                        {
                            try { process.Kill(); } catch (InvalidOperationException) { }
                            onFailure?.Invoke("Python converter timed out.");
                            return;
                        }
                        var standardError = process.StandardError.ReadToEnd();
                        var standardOutput = process.StandardOutput.ReadToEnd();
                        if (process.ExitCode != 0 || !File.Exists(outputPath)) onFailure?.Invoke(CopernicusCurrentClient.SelectFailureReason(standardError, standardOutput));
                        else onCompleted?.Invoke();
                    }
                    finally { process.Dispose(); }
                });
            }
            catch (Exception exception) { onFailure?.Invoke("Unable to start Python converter: " + exception.Message); }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }
}
