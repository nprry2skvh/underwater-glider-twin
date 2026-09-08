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
            IReadOnlyList<TelemetryFrame> futureFrames)
        {
            WindowStartIndex = windowStartIndex;
            WindowEndIndex = windowEndIndex;
            FutureStartIndex = futureStartIndex;
            FutureEndIndex = futureEndIndex;
            WindowFrames = windowFrames;
            FutureFrames = futureFrames;
        }

        public int WindowStartIndex { get; }
        public int WindowEndIndex { get; }
        public int FutureStartIndex { get; }
        public int FutureEndIndex { get; }
        public IReadOnlyList<TelemetryFrame> WindowFrames { get; }
        public IReadOnlyList<TelemetryFrame> FutureFrames { get; }
    }
}
