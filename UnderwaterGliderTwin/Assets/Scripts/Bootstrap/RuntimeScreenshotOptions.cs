using System;
using System.Collections.Generic;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class RuntimeScreenshotOptions
    {
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;

        private RuntimeScreenshotOptions(string outputPath, bool quitAfterCapture)
        {
            OutputPath = outputPath;
            QuitAfterCapture = quitAfterCapture;
        }

        public string OutputPath { get; }
        public bool QuitAfterCapture { get; }
        public bool IsCaptureRequested => !string.IsNullOrWhiteSpace(OutputPath);
        public bool UsesFixedCaptureResolution => IsCaptureRequested;

        public static RuntimeScreenshotOptions Parse(IReadOnlyList<string> args)
        {
            string outputPath = null;
            var quitAfterCapture = false;

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
                        || i + 1 >= args.Count)
                    {
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
                quitAfterCapture && !string.IsNullOrWhiteSpace(outputPath));
        }
    }
}
