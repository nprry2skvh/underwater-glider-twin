using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class RuntimeScreenshotOptions
    {
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;

        private RuntimeScreenshotOptions(
            string outputPath,
            bool quitAfterCapture,
            bool autoPlay,
            bool reversePlayback,
            float startProgress01,
            bool closeTopView,
            bool disableWake,
            float playbackSpeed,
            float captureDelaySeconds)
        {
            OutputPath = outputPath;
            QuitAfterCapture = quitAfterCapture;
            AutoPlay = autoPlay;
            ReversePlayback = reversePlayback;
            StartProgress01 = startProgress01;
            CloseTopView = closeTopView;
            DisableWake = disableWake;
            PlaybackSpeed = playbackSpeed;
            CaptureDelaySeconds = captureDelaySeconds;
        }

        public string OutputPath { get; }
        public bool QuitAfterCapture { get; }
        public bool AutoPlay { get; }
        public bool ReversePlayback { get; }
        public float StartProgress01 { get; }
        public bool CloseTopView { get; }
        public bool DisableWake { get; }
        public float PlaybackSpeed { get; }
        public float CaptureDelaySeconds { get; }
        public bool IsCaptureRequested => !string.IsNullOrWhiteSpace(OutputPath);
        public bool UsesFixedCaptureResolution => IsCaptureRequested;

        public static RuntimeScreenshotOptions Parse(IReadOnlyList<string> args)
        {
            string outputPath = null;
            var quitAfterCapture = false;
            var autoPlay = false;
            var reversePlayback = false;
            var startProgress01 = 0f;
            var closeTopView = false;
            var disableWake = false;
            var playbackSpeed = 1f;
            var captureDelaySeconds = 0f;

            if (args != null)
            {
                for (var i = 0; i < args.Count; i++)
                {
                    if (string.Equals(args[i], "--quit-after-screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        quitAfterCapture = true;
                        continue;
                    }

                    if (string.Equals(args[i], "--autoplay-screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        autoPlay = true;
                        continue;
                    }

                    if (string.Equals(args[i], "--reverse-screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        reversePlayback = true;
                        continue;
                    }

                    if (string.Equals(args[i], "--close-top-screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        closeTopView = true;
                        continue;
                    }

                    if (string.Equals(args[i], "--no-wake-screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        disableWake = true;
                        continue;
                    }

                    if (TryReadFloatArgument(args, ref i, "--screenshot-speed", out var requestedSpeed))
                    {
                        playbackSpeed = Math.Max(0.1f, Math.Min(20f, requestedSpeed));
                        continue;
                    }

                    if (TryReadFloatArgument(args, ref i, "--screenshot-progress", out var requestedProgress))
                    {
                        startProgress01 = Math.Max(0f, Math.Min(1f, requestedProgress));
                        continue;
                    }

                    if (TryReadFloatArgument(args, ref i, "--screenshot-delay", out var requestedDelay))
                    {
                        captureDelaySeconds = Math.Max(0f, Math.Min(30f, requestedDelay));
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
                quitAfterCapture && !string.IsNullOrWhiteSpace(outputPath),
                autoPlay && !string.IsNullOrWhiteSpace(outputPath),
                reversePlayback && autoPlay && !string.IsNullOrWhiteSpace(outputPath),
                startProgress01,
                closeTopView && autoPlay && !string.IsNullOrWhiteSpace(outputPath),
                disableWake && autoPlay && !string.IsNullOrWhiteSpace(outputPath),
                playbackSpeed,
                captureDelaySeconds);
        }

        private static bool TryReadFloatArgument(
            IReadOnlyList<string> args,
            ref int index,
            string argumentName,
            out float value)
        {
            value = 0f;
            if (!string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase)
                || index + 1 >= args.Count
                || !float.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value)
                || float.IsInfinity(value))
            {
                return false;
            }

            index++;
            return true;
        }
    }
}
