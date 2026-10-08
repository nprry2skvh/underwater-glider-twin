using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public static class PredictionWindowBuilder
    {
        public static PredictionWindow Build(IReadOnlyList<TelemetryFrame> frames, PredictionRequest request)
        {
            return Build(frames, request, includeFutureObservations: true);
        }

        public static PredictionWindow BuildForecast(IReadOnlyList<TelemetryFrame> frames, PredictionRequest request)
        {
            return Build(frames, request, includeFutureObservations: false);
        }

        private static PredictionWindow Build(IReadOnlyList<TelemetryFrame> frames, PredictionRequest request, bool includeFutureObservations)
        {
            if (frames == null || frames.Count == 0)
            {
                throw new ArgumentException("Prediction window requires telemetry frames.", nameof(frames));
            }

            var currentIndex = Math.Max(0, Math.Min(frames.Count - 1, request.CurrentIndex));
            var windowSize = Math.Max(1, request.WindowSize);
            var horizonPoints = Math.Max(1, request.HorizonPoints);
            var horizonSeconds = request.HorizonSeconds;
            var baseTime = frames[currentIndex].ElapsedSeconds;

            var windowStart = Math.Max(0, currentIndex - windowSize + 1);
            var windowFrames = new List<TelemetryFrame>(currentIndex - windowStart + 1);
            for (var i = windowStart; i <= currentIndex; i++)
            {
                windowFrames.Add(frames[i]);
            }
            if (!includeFutureObservations)
            {
                // Same model input contract as offline causal_history: thirty
                // issue-anchored 10s samples, past hold, never future interpolation.
                windowFrames.Clear();
                var cursor = 0;
                var firstGrid = baseTime - (windowSize - 1) * 10f;
                for (var grid = firstGrid; grid <= baseTime; grid += 10f)
                {
                    if (grid < frames[0].ElapsedSeconds) continue;
                    while (cursor < currentIndex && frames[cursor + 1].ElapsedSeconds <= grid) cursor++;
                    if (windowFrames.Count == 0) windowStart = cursor;
                    windowFrames.Add(HoldAtTime(frames[cursor], grid));
                }
            }

            var futureStart = currentIndex + 1;
            var futureFrames = new List<TelemetryFrame>(horizonPoints);
            var futureEnd = currentIndex;
            for (var i = futureStart; includeFutureObservations && i < frames.Count && futureFrames.Count < horizonPoints; i++)
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
                futureFrames,
                horizonSeconds);
        }

        private static TelemetryFrame HoldAtTime(TelemetryFrame frame, float time)
        {
            return new TelemetryFrame(frame.RowIndex, frame.RawTime, time, frame.LongitudeDeg, frame.LatitudeDeg,
                frame.DepthM, frame.AltitudeM, frame.HeadingDeg, frame.PitchDeg, frame.RollDeg,
                frame.Voltage24V, frame.Current24A, frame.BatteryPercent, frame.WorkMode, frame.RunState,
                frame.TargetSegment, frame.TargetHeadingDeg, frame.TargetDepthM, frame.TargetAltitudeM,
                frame.PropellerRpm, frame.PistonMm, frame.TurnAngleDeg, frame.Diagnostics,
                frame.PlannedLongitudeDeg, frame.PlannedLatitudeDeg, frame.MissionState, frame.ProfileSequence);
        }
    }
}
