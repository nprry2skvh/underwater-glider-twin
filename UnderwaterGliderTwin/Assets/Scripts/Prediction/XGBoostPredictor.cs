using System;
using System.Diagnostics;
using System.IO;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class XGBoostPredictor : IPredictor
    {
        private const float AnchorSeconds = 30f;
        private const float MetersPerDegreeLatitude = 111320f;
        private static readonly string[] Targets =
        {
            "east_displacement_m", "north_displacement_m", "depth_delta_m",
            "heading_delta_deg", "pitch_delta_deg", "roll_delta_deg",
        };

        private XGBoostArtifact artifact;
        private string loadError = "XGBoost artifact has not been loaded.";

        public bool IsReady => artifact != null;

        public string GetName()
        {
            return "XGBoost";
        }

        public void LoadModel(string modelDirectory)
        {
            if (!XGBoostArtifact.TryLoad(modelDirectory, out artifact, out loadError))
            {
                artifact = null;
                return;
            }

            if (artifact.Schema == null)
            {
                artifact = null;
                loadError = "XGBoost feature_schema.json is missing.";
            }
        }

        public PredictionResult Predict(PredictionContext context)
        {
            if (artifact == null)
            {
                return EmptyResult(context, loadError);
            }

            var timer = Stopwatch.StartNew();
            if (!PredictionWindow.IsSupportedHorizon(context.Window.HorizonSeconds))
            {
                return EmptyResult(context, "XGBoost unsupported horizon; use 30, 60, 300 or 900 seconds.");
            }

            var targetTimes = context.Window.TargetElapsedSeconds;
            var requestedSeconds = context.Window.HorizonSeconds;
            var anchorCount = Mathf.CeilToInt(requestedSeconds / AnchorSeconds);
            var anchors = new ForecastAnchor[anchorCount];
            for (var index = 0; index < anchorCount; index++)
            {
                var horizonSeconds = (index + 1) * AnchorSeconds;
                if (!XGBoostFeatureBuilder.TryBuild(context, artifact.Schema, horizonSeconds, out var features, out var error))
                {
                    return EmptyResult(context, error);
                }

                if (!TryEvaluateAnchor(features, out anchors[index], out error))
                {
                    return EmptyResult(context, error);
                }
            }

            var predictedPoints = new Vector3[targetTimes.Count];
            var forecastFrames = new TelemetryFrame[targetTimes.Count];
            var times = new float[targetTimes.Count];
            for (var index = 0; index < targetTimes.Count; index++)
            {
                var seconds = targetTimes[index] - context.CurrentFrame.ElapsedSeconds;
                var anchor = InterpolateAnchor(anchors, seconds);
                times[index] = targetTimes[index];
                forecastFrames[index] = CreateForecastFrame(context.CurrentFrame, anchor, times[index], index + 1);
                predictedPoints[index] = context.Mapper.Map(forecastFrames[index]);
            }

            timer.Stop();
            // No unseen observations or retrospective error-derived confidence at publication.
            var metrics = new PredictionMetrics(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, (float)timer.Elapsed.TotalMilliseconds);
            return new PredictionResult(
                GetName(),
                "XGBoost deployed + hold fallback; awaiting observations",
                predictedPoints,
                Array.Empty<Vector3>(),
                context.Window.WindowEndIndex + 1,
                context.Window.WindowEndIndex + targetTimes.Count,
                metrics,
                forecastFrames,
                times);
        }

        public void Release()
        {
            artifact = null;
            loadError = "XGBoost artifact has been released.";
        }

        private bool TryEvaluateAnchor(float[] features, out ForecastAnchor anchor, out string error)
        {
            var values = new float[Targets.Length];
            for (var index = 0; index < Targets.Length; index++)
            {
                var target = Targets[index];
                if (artifact.GetOutputSource(target) == "stable")
                {
                    values[index] = 0f;
                    continue;
                }

                var value = XGBoostTreeEvaluator.Evaluate(artifact.GetModel(target), features);
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    anchor = default;
                    error = "XGBoost model is invalid for " + target + ".";
                    return false;
                }

                values[index] = value;
            }

            anchor = new ForecastAnchor(values[0], values[1], values[2], values[3], values[4], values[5]);
            error = string.Empty;
            return true;
        }

        private static ForecastAnchor InterpolateAnchor(ForecastAnchor[] anchors, float seconds)
        {
            if (seconds <= 0f)
            {
                return default;
            }

            var lowerAnchor = Mathf.FloorToInt(seconds / AnchorSeconds);
            var upperAnchor = Mathf.CeilToInt(seconds / AnchorSeconds);
            lowerAnchor = Mathf.Clamp(lowerAnchor, 0, anchors.Length);
            upperAnchor = Mathf.Clamp(upperAnchor, 1, anchors.Length);
            var lower = lowerAnchor == 0 ? default : anchors[lowerAnchor - 1];
            var upper = anchors[upperAnchor - 1];
            var lowerSeconds = lowerAnchor * AnchorSeconds;
            var upperSeconds = upperAnchor * AnchorSeconds;
            var interpolation = Mathf.Approximately(lowerSeconds, upperSeconds)
                ? 1f
                : Mathf.InverseLerp(lowerSeconds, upperSeconds, seconds);
            return ForecastAnchor.Lerp(lower, upper, interpolation);
        }

        private static TelemetryFrame CreateForecastFrame(TelemetryFrame current, ForecastAnchor anchor, float targetSeconds, int rowOffset)
        {
            var longitudeScale = MetersPerDegreeLatitude * Mathf.Cos((float)(current.LatitudeDeg * Math.PI / 180.0));
            var longitude = current.LongitudeDeg + anchor.EastMeters / longitudeScale;
            var latitude = current.LatitudeDeg + anchor.NorthMeters / MetersPerDegreeLatitude;
            return new TelemetryFrame(
                current.RowIndex + rowOffset,
                "forecast",
                targetSeconds,
                longitude,
                latitude,
                current.DepthM + anchor.DepthDeltaM,
                current.AltitudeM,
                Mathf.Repeat(current.HeadingDeg + anchor.HeadingDeltaDeg, 360f),
                current.PitchDeg + anchor.PitchDeltaDeg,
                current.RollDeg + anchor.RollDeltaDeg,
                current.Voltage24V,
                current.Current24A,
                current.BatteryPercent,
                current.WorkMode,
                current.RunState,
                current.TargetSegment,
                current.TargetHeadingDeg,
                current.TargetDepthM,
                current.TargetAltitudeM,
                current.PropellerRpm,
                current.PistonMm,
                current.TurnAngleDeg,
                current.Diagnostics,
                current.PlannedLongitudeDeg,
                current.PlannedLatitudeDeg);
        }

        private PredictionResult EmptyResult(PredictionContext context, string status)
        {
            return new PredictionResult(
                GetName(),
                status,
                Array.Empty<Vector3>(),
                Array.Empty<Vector3>(),
                context.Window.FutureStartIndex,
                context.Window.FutureEndIndex,
                new PredictionMetrics(0f, 0f, 0f, 0f, 0f, 0f));
        }

        private readonly struct ForecastAnchor
        {
            public ForecastAnchor(float eastMeters, float northMeters, float depthDeltaM, float headingDeltaDeg, float pitchDeltaDeg, float rollDeltaDeg)
            {
                EastMeters = eastMeters;
                NorthMeters = northMeters;
                DepthDeltaM = depthDeltaM;
                HeadingDeltaDeg = headingDeltaDeg;
                PitchDeltaDeg = pitchDeltaDeg;
                RollDeltaDeg = rollDeltaDeg;
            }

            public float EastMeters { get; }
            public float NorthMeters { get; }
            public float DepthDeltaM { get; }
            public float HeadingDeltaDeg { get; }
            public float PitchDeltaDeg { get; }
            public float RollDeltaDeg { get; }

            public static ForecastAnchor Lerp(ForecastAnchor lower, ForecastAnchor upper, float amount)
            {
                return new ForecastAnchor(
                    Mathf.Lerp(lower.EastMeters, upper.EastMeters, amount),
                    Mathf.Lerp(lower.NorthMeters, upper.NorthMeters, amount),
                    Mathf.Lerp(lower.DepthDeltaM, upper.DepthDeltaM, amount),
                    Mathf.Lerp(lower.HeadingDeltaDeg, upper.HeadingDeltaDeg, amount),
                    Mathf.Lerp(lower.PitchDeltaDeg, upper.PitchDeltaDeg, amount),
                    Mathf.Lerp(lower.RollDeltaDeg, upper.RollDeltaDeg, amount));
            }
        }
    }
}
