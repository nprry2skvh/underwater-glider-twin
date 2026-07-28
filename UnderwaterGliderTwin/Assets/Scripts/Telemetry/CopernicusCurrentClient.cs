using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class CopernicusCurrentClient
    {
        public const float RequestTimeoutSeconds = 180f;
        private readonly ICopernicusCurrentFetchBackend backend;

        public CopernicusCurrentClient() : this(new PythonCurrentFetchBackend()) { }

        public CopernicusCurrentClient(ICopernicusCurrentFetchBackend fetchBackend)
        {
            backend = fetchBackend ?? throw new ArgumentNullException(nameof(fetchBackend));
        }

        public IEnumerator Fetch(CopernicusCurrentRequest request, Action<CopernicusCurrentResult> onSuccess, Action<string> onFailure, Action<string> onProgress = null)
        {
            return Fetch(request, OceanCurrentAcquisitionMode.Online, null, DateTime.UtcNow, onSuccess, onFailure, onProgress);
        }

        public IEnumerator Fetch(CopernicusCurrentRequest request, OceanCurrentAcquisitionMode mode, string localPath, DateTime referenceTimeUtc, Action<CopernicusCurrentResult> onSuccess, Action<string> onFailure, Action<string> onProgress = null)
        {
            if (request == null) { onFailure?.Invoke("Current request is missing."); yield break; }
            if (!request.TryValidate(out var validationError)) { onFailure?.Invoke(validationError); yield break; }

            if (mode == OceanCurrentAcquisitionMode.LocalFile)
            {
                if (OceanCurrentFileLoader.TryLoad(localPath, referenceTimeUtc, out var localResult, out var localError)) onSuccess?.Invoke(localResult);
                else onFailure?.Invoke(localError);
                yield break;
            }

            if (mode == OceanCurrentAcquisitionMode.CacheOnly)
            {
                if (CopernicusCurrentCache.TryLoad(request, out var cached)) onSuccess?.Invoke(cached);
                else onFailure?.Invoke("Current cache has no valid entry for this request.");
                yield break;
            }

            var workingDirectory = Path.Combine(Application.temporaryCachePath, "CopernicusCurrent", Guid.NewGuid().ToString("N"));
            var requestPath = Path.Combine(workingDirectory, "request.json");
            var responsePath = Path.Combine(workingDirectory, "response.json");
            string failure = null;
            string completedPath = null;
            var startedAtUtc = DateTime.UtcNow;
            try
            {
                Directory.CreateDirectory(workingDirectory);
                File.WriteAllText(requestPath, JsonUtility.ToJson(request));
                startedAtUtc = DateTime.UtcNow;
                backend.Run(request, requestPath, responsePath, value => completedPath = string.IsNullOrWhiteSpace(value) ? responsePath : value, value => failure = value, onProgress);
                onProgress?.Invoke(BuildProgressMessage(0f));
            }
            catch (Exception exception)
            {
                failure = "Unable to prepare Copernicus request: " + exception.Message;
            }
            if (!string.IsNullOrWhiteSpace(failure))
            {
                Cleanup(workingDirectory);
                onFailure?.Invoke(failure);
                yield break;
            }
            var deadline = DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds);
            var lastProgress = -10f;
            while (string.IsNullOrWhiteSpace(completedPath) && string.IsNullOrWhiteSpace(failure) && DateTime.UtcNow < deadline)
            {
                var elapsed = (float)(DateTime.UtcNow - startedAtUtc).TotalSeconds;
                if (elapsed - lastProgress >= 10f) { lastProgress = elapsed; onProgress?.Invoke(BuildProgressMessage(elapsed)); }
                yield return null;
            }
            if (string.IsNullOrWhiteSpace(completedPath))
            {
                Cleanup(workingDirectory);
                onFailure?.Invoke(string.IsNullOrWhiteSpace(failure) ? "Copernicus current request timed out." : failure);
                yield break;
            }
            if (!TryReadCompletedResponse(completedPath, out var result))
            {
                Cleanup(workingDirectory);
                onFailure?.Invoke("Copernicus current response could not be read or failed schema validation.");
                yield break;
            }
            try { CopernicusCurrentCache.Store(request, File.ReadAllText(completedPath)); onSuccess?.Invoke(result); }
            finally { Cleanup(workingDirectory); }
        }

        public static bool TryReadCompletedResponse(string responsePath, out CopernicusCurrentResult result)
        {
            result = null;
            try
            {
                if (string.IsNullOrWhiteSpace(responsePath) || !File.Exists(responsePath)) return false;
                result = CopernicusCurrentResponseParser.Parse(File.ReadAllText(responsePath));
                return true;
            }
            catch (Exception) { return false; }
        }

        public static string SelectFailureReason(string standardError, string standardOutput)
        {
            var output = string.IsNullOrWhiteSpace(standardError) ? standardOutput : string.IsNullOrWhiteSpace(standardOutput) ? standardError : standardError + "\n" + standardOutput;
            if (string.IsNullOrWhiteSpace(output)) return "Copernicus current lookup failed.";
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                var line = lines[index].Trim();
                if (!line.StartsWith("INFO", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase)) return line;
            }
            return "Copernicus current fetcher did not return a usable response. Check credentials or network and retry.";
        }

        public static string BuildProgressMessage(float elapsedSeconds)
        {
            return elapsedSeconds < 10f ? "正在连接 Copernicus 海流服务..." : "正在下载 Copernicus 海流数据，已等待 " + Mathf.CeilToInt(elapsedSeconds) + " 秒...";
        }

        private static void Cleanup(string directory)
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    internal sealed class PythonCurrentFetchBackend : ICopernicusCurrentFetchBackend
    {
        public void Run(CopernicusCurrentRequest request, string requestPath, string responsePath, Action<string> onCompleted, Action<string> onFailure, Action<string> onProgress)
        {
            var scriptPath = Path.Combine(Application.streamingAssetsPath, "CopernicusCurrentFetcher.py");
            if (!File.Exists(scriptPath)) { onFailure?.Invoke("Copernicus current fetcher was not found in StreamingAssets."); return; }
            try
            {
                var python = Environment.GetEnvironmentVariable("COPERNICUS_PYTHON");
                var process = new Process { StartInfo = new ProcessStartInfo { FileName = string.IsNullOrWhiteSpace(python) ? "python" : python, Arguments = Quote(scriptPath) + " --request " + Quote(requestPath) + " --output " + Quote(responsePath), CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = Path.GetDirectoryName(requestPath) } };
                process.Start();
                Task.Run(() =>
                {
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    var exitCode = process.ExitCode;
                    process.Dispose();
                    if (exitCode == 0 && File.Exists(responsePath)) onCompleted?.Invoke(responsePath);
                    else onFailure?.Invoke(CopernicusCurrentClient.SelectFailureReason(error, output));
                });
            }
            catch (Exception exception) { onFailure?.Invoke("Unable to start Copernicus current fetcher: " + exception.Message); }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }
}
