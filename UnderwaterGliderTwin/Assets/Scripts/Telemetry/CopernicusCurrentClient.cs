using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class CopernicusCurrentClient
    {
        private const string PythonExecutableEnvironmentVariable = "COPERNICUS_PYTHON";
        private const string FetcherFileName = "CopernicusCurrentFetcher.py";
        public const float RequestTimeoutSeconds = 180f;

        public IEnumerator Fetch(
            CopernicusCurrentRequest request,
            Action<CopernicusCurrentResult> onSuccess,
            Action<string> onFailure,
            Action<string> onProgress = null)
        {
            if (request == null)
            {
                onFailure?.Invoke("Current request is missing.");
                yield break;
            }

            if (!request.TryValidate(out var validationError))
            {
                onFailure?.Invoke(validationError);
                yield break;
            }

            if (CopernicusCurrentCache.TryLoad(request, out var cachedResult))
            {
                onProgress?.Invoke("已从本地缓存载入海流数据。");
                onSuccess?.Invoke(cachedResult);
                yield break;
            }

            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
            {
                onFailure?.Invoke("Copernicus current lookup is currently available in the Windows build only.");
                yield break;
            }

            var scriptPath = Path.Combine(Application.streamingAssetsPath, FetcherFileName);
            if (!File.Exists(scriptPath))
            {
                onFailure?.Invoke("Copernicus current fetcher was not found in StreamingAssets.");
                yield break;
            }

            var workingDirectory = Path.Combine(Application.temporaryCachePath, "CopernicusCurrent", CopernicusCurrentCache.BuildCacheKey(request));
            var requestPath = Path.Combine(workingDirectory, "request.json");
            var responsePath = Path.Combine(workingDirectory, "response.json");
            try
            {
                Directory.CreateDirectory(workingDirectory);
                File.WriteAllText(requestPath, JsonUtility.ToJson(request));
                if (File.Exists(responsePath))
                {
                    File.Delete(responsePath);
                }
            }
            catch (Exception exception)
            {
                onFailure?.Invoke($"Unable to prepare Copernicus request: {exception.Message}");
                yield break;
            }

            Process process;
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ResolvePythonExecutable(),
                        Arguments = $"{Quote(scriptPath)} --request {Quote(requestPath)} --output {Quote(responsePath)}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WorkingDirectory = workingDirectory
                    }
                };
                process.Start();
                onProgress?.Invoke(BuildProgressMessage(0f));
            }
            catch (Exception exception)
            {
                DeleteTemporaryFiles(requestPath, responsePath);
                onFailure?.Invoke($"Unable to start Copernicus current fetcher: {exception.Message}");
                yield break;
            }

            var startedAtUtc = DateTime.UtcNow;
            var deadline = startedAtUtc.AddSeconds(RequestTimeoutSeconds);
            var lastProgressSeconds = -10f;
            while (!process.HasExited)
            {
                if (TryReadCompletedResponse(responsePath, out var completedResult))
                {
                    CompleteSuccessfulResponse(process, request, responsePath, completedResult, onSuccess);
                    DeleteTemporaryFiles(requestPath, responsePath);
                    yield break;
                }

                var elapsedSeconds = (float)(DateTime.UtcNow - startedAtUtc).TotalSeconds;
                if (elapsedSeconds - lastProgressSeconds >= 10f)
                {
                    lastProgressSeconds = elapsedSeconds;
                    onProgress?.Invoke(BuildProgressMessage(elapsedSeconds));
                }

                if (DateTime.UtcNow >= deadline)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    process.Dispose();
                    DeleteTemporaryFiles(requestPath, responsePath);
                    onFailure?.Invoke($"Copernicus 海流请求超过 {RequestTimeoutSeconds:0} 秒仍未返回。请检查网络或稍后重试。");
                    yield break;
                }

                yield return null;
            }

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            var exitCode = process.ExitCode;
            process.Dispose();

            if (TryReadCompletedResponse(responsePath, out var result))
            {
                CompleteSuccessfulResponse(null, request, responsePath, result, onSuccess);
                DeleteTemporaryFiles(requestPath, responsePath);
                yield break;
            }

            if (exitCode != 0 || !File.Exists(responsePath))
            {
                DeleteTemporaryFiles(requestPath, responsePath);
                onFailure?.Invoke(SelectFailureReason(standardError, standardOutput));
                yield break;
            }

            DeleteTemporaryFiles(requestPath, responsePath);
            onFailure?.Invoke("Copernicus current response could not be read.");
        }

        public static bool TryReadCompletedResponse(string responsePath, out CopernicusCurrentResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(responsePath) || !File.Exists(responsePath))
            {
                return false;
            }

            try
            {
                result = CopernicusCurrentResponseParser.Parse(File.ReadAllText(responsePath));
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string SelectFailureReason(string standardError, string standardOutput)
        {
            var combinedOutput = string.IsNullOrWhiteSpace(standardError)
                ? standardOutput
                : string.IsNullOrWhiteSpace(standardOutput)
                    ? standardError
                    : standardError + "\n" + standardOutput;
            if (string.IsNullOrWhiteSpace(combinedOutput))
            {
                return "Copernicus current lookup failed.";
            }

            var lines = combinedOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                var line = lines[index].Trim();
                if (!line.StartsWith("INFO", StringComparison.OrdinalIgnoreCase)
                    && !line.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
                {
                    return line;
                }
            }

            return "Copernicus current fetcher did not return a usable response. Check credentials or network and retry.";
        }

        private static void CompleteSuccessfulResponse(
            Process process,
            CopernicusCurrentRequest request,
            string responsePath,
            CopernicusCurrentResult result,
            Action<CopernicusCurrentResult> onSuccess)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process?.Dispose();
            }

            CopernicusCurrentCache.Store(request, File.ReadAllText(responsePath));
            onSuccess?.Invoke(result);
        }

        private static string ResolvePythonExecutable()
        {
            var configuredPath = Environment.GetEnvironmentVariable(PythonExecutableEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configuredPath) ? "python" : configuredPath;
        }

        public static string BuildProgressMessage(float elapsedSeconds)
        {
            if (elapsedSeconds < 10f)
            {
                return "正在连接 Copernicus 海流服务...";
            }

            return $"正在下载 Copernicus 海流数据，已等待 {Mathf.CeilToInt(elapsedSeconds)} 秒...";
        }

        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        private static void DeleteTemporaryFiles(string requestPath, string responsePath)
        {
            try
            {
                if (File.Exists(requestPath))
                {
                    File.Delete(requestPath);
                }

                if (File.Exists(responsePath))
                {
                    File.Delete(responsePath);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
