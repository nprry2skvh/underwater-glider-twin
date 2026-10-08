using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class PredictionWindow
    {
        public PredictionWindow(
            int windowStartIndex,
            int windowEndIndex,
            int futureStartIndex,
            int futureEndIndex,
            IReadOnlyList<TelemetryFrame> windowFrames,
            IReadOnlyList<TelemetryFrame> futureFrames,
            float horizonSeconds = 0f)
        {
            WindowStartIndex = windowStartIndex;
            WindowEndIndex = windowEndIndex;
            FutureStartIndex = futureStartIndex;
            FutureEndIndex = futureEndIndex;
            WindowFrames = windowFrames;
            FutureFrames = futureFrames;
            HorizonSeconds = horizonSeconds;
            var targets = new List<float>();
            if (IsSupportedHorizon(horizonSeconds) && windowFrames.Count > 0)
            {
                var origin = windowFrames[windowFrames.Count - 1].ElapsedSeconds;
                for (var seconds = 10f; seconds <= horizonSeconds; seconds += 10f)
                    targets.Add(origin + seconds);
            }
            TargetElapsedSeconds = targets.AsReadOnly();
        }

        public int WindowStartIndex { get; }
        public int WindowEndIndex { get; }
        public int FutureStartIndex { get; }
        public int FutureEndIndex { get; }
        public IReadOnlyList<TelemetryFrame> WindowFrames { get; }
        public IReadOnlyList<TelemetryFrame> FutureFrames { get; }
        public float HorizonSeconds { get; }
        public IReadOnlyList<float> TargetElapsedSeconds { get; }

        public static bool IsSupportedHorizon(float seconds)
        {
            return seconds == 30f || seconds == 60f || seconds == 300f || seconds == 900f;
        }
    }
}
