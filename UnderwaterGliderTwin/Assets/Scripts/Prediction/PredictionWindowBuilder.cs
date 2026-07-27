using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public static class PredictionWindowBuilder
    {
        public static PredictionWindow Build(IReadOnlyList<TelemetryFrame> frames, PredictionRequest request)
        {
            if (frames == null || frames.Count == 0)
            {
                throw new ArgumentException("Prediction window requires telemetry frames.", nameof(frames));
            }

            var currentIndex = Math.Max(0, Math.Min(frames.Count - 1, request.CurrentIndex));
            var windowSize = Math.Max(1, request.WindowSize);
            var horizonPoints = Math.Max(1, request.HorizonPoints);
            var horizonSeconds = Math.Max(0f, request.HorizonSeconds);

            var windowStart = Math.Max(0, currentIndex - windowSize + 1);
            var windowFrames = new List<TelemetryFrame>(currentIndex - windowStart + 1);
            for (var i = windowStart; i <= currentIndex; i++)
            {
                windowFrames.Add(frames[i]);
            }

            var futureStart = Math.Min(frames.Count - 1, currentIndex + 1);
            var futureFrames = new List<TelemetryFrame>(horizonPoints);
            var futureEnd = currentIndex;
            var baseTime = frames[currentIndex].ElapsedSeconds;
            for (var i = futureStart; i < frames.Count && futureFrames.Count < horizonPoints; i++)
            {
                if (horizonSeconds > 0f && frames[i].ElapsedSeconds - baseTime > horizonSeconds)
                {
                    break;
                }

                futureFrames.Add(frames[i]);
                futureEnd = i;
            }

            return new PredictionWindow(
                windowStart,
                currentIndex,
                futureFrames.Count > 0 ? futureStart : currentIndex,
                futureFrames.Count > 0 ? futureEnd : currentIndex,
                windowFrames,
                futureFrames);
        }
    }
}
