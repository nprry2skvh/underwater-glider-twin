using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class RuntimeScreenshotCapture : MonoBehaviour
    {
        public const int WarmupFrameCount = 2;

        private RuntimeScreenshotOptions options;
        private Transform closeTopTarget;
        private Camera closeTopCamera;

        public void Initialize(RuntimeScreenshotOptions screenshotOptions)
        {
            options = screenshotOptions;
            if (options == null || !options.IsCaptureRequested)
            {
                return;
            }

            if (options.CloseTopView)
            {
                var waterSurface = FindObjectOfType<UnderwaterGliderTwin.Visualization.WaterSurfaceView>();
                closeTopTarget = waterSurface != null ? waterSurface.InteractionTarget : null;
                closeTopCamera = Camera.main;
            }

            Screen.SetResolution(RuntimeScreenshotOptions.CaptureWidth, RuntimeScreenshotOptions.CaptureHeight, FullScreenMode.Windowed);
            StartCoroutine(CaptureAtEndOfFrame(
                options.OutputPath,
                options.QuitAfterCapture,
                options.CaptureDelaySeconds));
        }

        private void Update()
        {
            if (closeTopTarget != null && closeTopCamera != null)
            {
                closeTopCamera.transform.position = closeTopTarget.position + Vector3.up * 20f;
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                CaptureManual();
            }
        }

        public string CaptureManual()
        {
            var path = Path.Combine(RuntimePathResolver.ResolveExportDirectory(),
                $"glider-shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            StartCoroutine(CaptureAtEndOfFrame(path, quitAfterCapture: false, captureDelaySeconds: 0f));
            return path;
        }

        public void CaptureAsync(string path, Action<bool, string> completed)
        {
            StartCoroutine(CaptureAsyncCoroutine(path, completed));
        }

        private IEnumerator CaptureAsyncCoroutine(string path, Action<bool, string> completed)
        {
            var success = false;
            string error = null;
            yield return new WaitForEndOfFrame();
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                ScreenCapture.CaptureScreenshot(path, 1);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            if (string.IsNullOrEmpty(error))
            {
                var deadline = Time.realtimeSinceStartup + 10f;
                while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
                success = File.Exists(path);
                if (!success) error = "Timed out waiting for screenshot capture.";
            }
            completed?.Invoke(success, error);
        }

        private IEnumerator CaptureAtEndOfFrame(string path, bool quitAfterCapture, float captureDelaySeconds)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            for (var frame = 0; frame < WarmupFrameCount; frame++)
            {
                yield return null;
            }

            if (captureDelaySeconds > 0f)
            {
                var captureAt = Time.realtimeSinceStartup + captureDelaySeconds;
                while (Time.realtimeSinceStartup < captureAt)
                {
                    yield return null;
                }
            }

            yield return new WaitForEndOfFrame();
            if (options != null && options.AutoPlay)
            {
                var waterSurface = FindObjectOfType<UnderwaterGliderTwin.Visualization.WaterSurfaceView>();
                if (waterSurface != null)
                {
                    var playback = FindObjectOfType<UnderwaterGliderTwin.Playback.PlaybackController>();
                    var isPlaying = playback != null && playback.Model != null && playback.Model.IsPlaying;
                    var velocity = waterSurface.InteractionTarget != null && waterSurface.InteractionTarget.GetComponent<UnderwaterGliderTwin.Visualization.GliderTransformDriver>() is UnderwaterGliderTwin.Visualization.GliderTransformDriver driver
                        ? driver.PlaybackVelocity
                        : Vector3.zero;
                    Debug.Log($"Water interaction capture: playing={isPlaying}, speed={waterSurface.InteractionSpeedMps:0.000} m/s, depth={waterSurface.InteractionDepthM:0.000} Unity m, velocity=({velocity.x:0.000},{velocity.y:0.000},{velocity.z:0.000}) Unity m/s.", waterSurface);
                    if (options.CloseTopView && Camera.main != null && waterSurface.InteractionTarget != null)
                    {
                        var diagnosticCamera = Camera.main;
                        var targetViewport = diagnosticCamera.WorldToViewportPoint(waterSurface.InteractionTarget.position);
                        Debug.Log($"Close-top screenshot camera: orthographic={diagnosticCamera.orthographic}, position={diagnosticCamera.transform.position}, target={waterSurface.InteractionTarget.position}, targetViewport={targetViewport}.", diagnosticCamera);
                    }
                }
            }

            ScreenCapture.CaptureScreenshot(path, 1);

            if (!quitAfterCapture)
            {
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning($"Timed out waiting for screenshot capture: {path}", this);
            }

            Application.Quit();
        }
    }
}
