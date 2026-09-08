using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class RuntimeScreenshotCapture : MonoBehaviour
    {
        public const int WarmupFrameCount = 30;

        private RuntimeScreenshotOptions options;

        public void Initialize(RuntimeScreenshotOptions screenshotOptions)
        {
            options = screenshotOptions;
            if (options == null || !options.IsCaptureRequested)
            {
                return;
            }

            Screen.SetResolution(options.Width, options.Height, FullScreenMode.Windowed);
            StartCoroutine(CaptureAtEndOfFrame(options.OutputPath, options.QuitAfterCapture));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12))
            {
                CaptureManual();
            }
        }

        public string CaptureManual()
        {
            var path = Path.Combine(RuntimePathResolver.ResolveExportDirectory(),
                $"glider-shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            StartCoroutine(CaptureAtEndOfFrame(path, quitAfterCapture: false));
            return path;
        }

        private IEnumerator CaptureAtEndOfFrame(string path, bool quitAfterCapture)
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

            yield return new WaitForEndOfFrame();
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
