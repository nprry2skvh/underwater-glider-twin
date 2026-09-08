using System;
using System.Collections.Generic;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class RuntimeScreenshotOptions
    {
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;

        private RuntimeScreenshotOptions(string outputPath, bool quitAfterCapture, int width, int height)
        {
            OutputPath = outputPath;
            QuitAfterCapture = quitAfterCapture;
            Width = width;
            Height = height;
        }

        public string OutputPath { get; }
        public bool QuitAfterCapture { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsCaptureRequested => !string.IsNullOrWhiteSpace(OutputPath);
        public bool UsesFixedCaptureResolution => IsCaptureRequested;

        public static RuntimeScreenshotOptions Parse(IReadOnlyList<string> args)
        {
            string outputPath = null;
            var quitAfterCapture = false;
            var width = CaptureWidth;
            var height = CaptureHeight;

            if (args != null)
            {
                for (var i = 0; i < args.Count; i++)
                {
                    if (string.Equals(args[i], "--quit-after-screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        quitAfterCapture = true;
                        continue;
                    }

                    if (!string.Equals(args[i], "--screenshot", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(args[i], "--screenshot-width", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(args[i], "--screenshot-height", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (i + 1 >= args.Count)
                    {
                        continue;
                    }

                    if (string.Equals(args[i], "--screenshot-width", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(args[i + 1], out var parsedWidth) && parsedWidth > 0)
                        {
                            width = parsedWidth;
                        }

                        i++;
                        continue;
                    }

                    if (string.Equals(args[i], "--screenshot-height", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(args[i + 1], out var parsedHeight) && parsedHeight > 0)
                        {
                            height = parsedHeight;
                        }

                        i++;
                        continue;
                    }

                    var candidatePath = args[i + 1];
                    if (!string.IsNullOrWhiteSpace(candidatePath)
                        && !candidatePath.StartsWith("--", StringComparison.Ordinal))
                    {
                        outputPath = candidatePath;
                        i++;
                    }
                }
            }

            return new RuntimeScreenshotOptions(
                outputPath,
                quitAfterCapture && !string.IsNullOrWhiteSpace(outputPath),
                width,
                height);
        }
    }
}
