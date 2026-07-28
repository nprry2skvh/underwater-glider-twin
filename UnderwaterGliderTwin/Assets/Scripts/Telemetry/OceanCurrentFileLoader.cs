using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace UnderwaterGliderTwin.Telemetry
{
    public interface IOceanCurrentFileConverter
    {
        void Convert(string inputPath, string outputPath, Action onCompleted, Action<string> onFailure, Action<string> onProgress);
    }

    public static class OceanCurrentFileLoader
    {
        public static IOceanCurrentFileConverter Converter { get; set; } = new PythonOceanCurrentFileConverter();

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
                    return true;
                }

                if (NetCdfClassicCurrentReader.TryRead(normalizedPath, referenceTimeUtc ?? DateTime.UtcNow, out result, out error))
                {
                    return true;
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

                var directory = Path.Combine(UnityEngine.Application.temporaryCachePath, "OceanCurrentConvert", Guid.NewGuid().ToString("N"));
                var outputPath = Path.Combine(directory, "converted.json");
                Directory.CreateDirectory(directory);
                var completed = false;
                string conversionError = null;
                Converter.Convert(normalizedPath, outputPath, () => completed = true, value => conversionError = value, null);
                if (!completed || !string.IsNullOrWhiteSpace(conversionError) || !File.Exists(outputPath))
                {
                    error = "LocalFile converter failed for '" + normalizedPath + "': " + (conversionError ?? "no usable output was produced.");
                    return false;
                }

                result = CopernicusCurrentResponseParser.Parse(File.ReadAllText(outputPath));
                return true;
            }
            catch (Exception exception)
            {
                error = "LocalFile parse failed for '" + path + "': " + exception.Message;
                return false;
            }
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
        public void Convert(string inputPath, string outputPath, Action onCompleted, Action<string> onFailure, Action<string> onProgress)
        {
            var scriptPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "CopernicusCurrentFetcher.py");
            if (!File.Exists(scriptPath)) { onFailure?.Invoke("Python converter script was not found."); return; }
            try
            {
                var configuredPython = Environment.GetEnvironmentVariable("COPERNICUS_PYTHON");
                var process = new Process { StartInfo = new ProcessStartInfo { FileName = string.IsNullOrWhiteSpace(configuredPython) ? "python" : configuredPython, Arguments = Quote(scriptPath) + " --convert " + Quote(inputPath) + " --output " + Quote(outputPath), CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true } };
                process.Start();
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
                process.Dispose();
            }
            catch (Exception exception) { onFailure?.Invoke("Unable to start Python converter: " + exception.Message); }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }
}
