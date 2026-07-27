using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class PhysicsPredictor : IPredictor
    {
        private const float MinHalfCycleSeconds = 20f;
        private const float DefaultHalfCycleSeconds = 90f;
        private const float MinHorizontalSpeedMetersPerSecond = 0.05f;
        private const float MaxAnalogueAverageScore = 18f;

        public string GetName()
        {
            return "Physics";
        }

        public void LoadModel(string modelDirectory)
        {
        }

        public void Release()
        {
        }

        public PredictionResult Predict(PredictionContext context)
        {
            var timer = Stopwatch.StartNew();
            var futureFrames = context.Window.FutureFrames;
            if (futureFrames.Count == 0)
            {
                return new PredictionResult(GetName(), "Prediction window exhausted", System.Array.Empty<Vector3>(), System.Array.Empty<Vector3>(), context.Window.FutureStartIndex, context.Window.FutureEndIndex, new PredictionMetrics(0f, 0f, 0f, 0f, 0f, 0f));
            }

            if (TryPredictFromHistoricalAnalogue(context, out var analoguePrediction))
            {
                timer.Stop();
                var analogueMetrics = ErrorEvaluator.Evaluate(analoguePrediction, MapActualPoints(context), (float)timer.Elapsed.TotalMilliseconds);
                return new PredictionResult(GetName(), "Physics analogue prediction", analoguePrediction, MapActualPoints(context), context.Window.FutureStartIndex, context.Window.FutureEndIndex, analogueMetrics);
            }

            var currentFrame = context.CurrentFrame;
            var currentPoint = context.Mapper.Map(currentFrame);
            var horizontalSpeed = EstimateHorizontalSpeed(context);
            var verticalRate = EstimateVerticalRateMagnitude(context);
            var halfCycleSeconds = EstimateHalfCycleSeconds(context);
            var currentHeading = currentFrame.HeadingDeg;
            var headingRate = EstimateHeadingRate(context);
            var pitchAmplitude = EstimatePitchAmplitude(context);
            var descending = ResolveDescendingState(context);
            var phaseElapsedSeconds = EstimateCurrentPhaseElapsedSeconds(context, descending);
            var predictedPoints = new Vector3[futureFrames.Count];
            var actualPoints = new Vector3[futureFrames.Count];
            var simulatedHorizontal = Vector3.zero;
            var simulatedDepth = currentFrame.DepthM;
            var previousSeconds = currentFrame.ElapsedSeconds;

            for (var i = 0; i < futureFrames.Count; i++)
            {
                var futureFrame = futureFrames[i];
                var deltaSeconds = Mathf.Max(0f, futureFrame.ElapsedSeconds - previousSeconds);
                SimulateGlideStep(
                    deltaSeconds,
                    horizontalSpeed,
                    verticalRate,
                    halfCycleSeconds,
                    headingRate,
                    pitchAmplitude,
                    ref currentHeading,
                    ref descending,
                    ref phaseElapsedSeconds,
                    ref simulatedDepth,
                    ref simulatedHorizontal);

                predictedPoints[i] = new Vector3(
                    currentPoint.x + simulatedHorizontal.x,
                    context.Mapper.MapDepth(simulatedDepth),
                    currentPoint.z + simulatedHorizontal.z);
                actualPoints[i] = context.Mapper.Map(futureFrame);
                previousSeconds = futureFrame.ElapsedSeconds;
            }

            timer.Stop();
            var metrics = ErrorEvaluator.Evaluate(predictedPoints, actualPoints, (float)timer.Elapsed.TotalMilliseconds);
            return new PredictionResult(GetName(), "Physics prediction", predictedPoints, actualPoints, context.Window.FutureStartIndex, context.Window.FutureEndIndex, metrics);
        }

        private static bool TryPredictFromHistoricalAnalogue(PredictionContext context, out Vector3[] predictedPoints)
        {
            predictedPoints = null;
            var frames = context.Frames;
            var windowFrames = context.Window.WindowFrames;
            var futureCount = context.Window.FutureFrames.Count;
            var currentIndex = context.Window.WindowEndIndex;
            var windowSize = windowFrames.Count;
            if (windowSize < 12 || currentIndex < windowSize + futureCount + 8)
            {
                return false;
            }

            var bestEndIndex = -1;
            var bestScore = float.MaxValue;
            var currentStartIndex = context.Window.WindowStartIndex;
            for (var candidateEnd = windowSize - 1; candidateEnd <= currentIndex - futureCount - 6; candidateEnd++)
            {
                var candidateStart = candidateEnd - windowSize + 1;
                if (candidateStart < 0)
                {
                    continue;
                }

                var score = ScoreWindowMatch(frames, currentStartIndex, candidateStart, windowSize);
                if (score < bestScore && IsCandidateFutureReasonable(frames, candidateEnd, futureCount))
                {
                    bestScore = score;
                    bestEndIndex = candidateEnd;
                }
            }

            if (bestEndIndex < 0 || bestScore / windowSize > MaxAnalogueAverageScore)
            {
                return false;
            }

            var currentPoint = context.Mapper.Map(frames[currentIndex]);
            var candidatePoint = context.Mapper.Map(frames[bestEndIndex]);
            var headingOffsetDeg = Mathf.DeltaAngle(frames[bestEndIndex].HeadingDeg, frames[currentIndex].HeadingDeg);
            var headingRotation = Quaternion.Euler(0f, headingOffsetDeg, 0f);
            predictedPoints = new Vector3[futureCount];
            for (var i = 0; i < futureCount; i++)
            {
                var candidateFutureIndex = bestEndIndex + 1 + i;
                if (candidateFutureIndex >= frames.Count)
                {
                    return false;
                }

                var candidateFuturePoint = context.Mapper.Map(frames[candidateFutureIndex]);
                var relative = candidateFuturePoint - candidatePoint;
                var rotatedHorizontal = headingRotation * new Vector3(relative.x, 0f, relative.z);
                predictedPoints[i] = new Vector3(
                    currentPoint.x + rotatedHorizontal.x,
                    currentPoint.y + relative.y,
                    currentPoint.z + rotatedHorizontal.z);
            }

            return true;
        }

        private static float ScoreWindowMatch(IReadOnlyList<TelemetryFrame> frames, int currentStartIndex, int candidateStartIndex, int windowSize)
        {
            var currentStart = frames[currentStartIndex];
            var candidateStart = frames[candidateStartIndex];
            var depthError = 0f;
            var pitchError = 0f;
            var rollError = 0f;
            var headingError = 0f;
            var horizontalError = 0f;

            for (var i = 0; i < windowSize; i++)
            {
                var currentFrame = frames[currentStartIndex + i];
                var candidateFrame = frames[candidateStartIndex + i];
                depthError += Mathf.Abs((currentFrame.DepthM - currentStart.DepthM) - (candidateFrame.DepthM - candidateStart.DepthM));
                pitchError += Mathf.Abs(currentFrame.PitchDeg - candidateFrame.PitchDeg);
                rollError += Mathf.Abs(currentFrame.RollDeg - candidateFrame.RollDeg);
                var currentHeadingDelta = Mathf.DeltaAngle(currentStart.HeadingDeg, currentFrame.HeadingDeg);
                var candidateHeadingDelta = Mathf.DeltaAngle(candidateStart.HeadingDeg, candidateFrame.HeadingDeg);
                headingError += Mathf.Abs(Mathf.DeltaAngle(candidateHeadingDelta, currentHeadingDelta));

                if (TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(currentStart, currentFrame, out var currentHorizontal)
                    && TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(candidateStart, candidateFrame, out var candidateHorizontal))
                {
                    var currentLocal = RotateHorizontal(currentHorizontal, -currentStart.HeadingDeg);
                    var candidateLocal = RotateHorizontal(candidateHorizontal, -candidateStart.HeadingDeg);
                    horizontalError += Vector2.Distance(currentLocal, candidateLocal);
                }
            }

            return depthError * 1.8f + pitchError * 0.7f + rollError * 0.35f + headingError * 0.18f + horizontalError * 0.12f;
        }

        private static Vector3[] MapActualPoints(PredictionContext context)
        {
            var points = new Vector3[context.Window.FutureFrames.Count];
            for (var i = 0; i < context.Window.FutureFrames.Count; i++)
            {
                points[i] = context.Mapper.Map(context.Window.FutureFrames[i]);
            }

            return points;
        }

        private static bool IsCandidateFutureReasonable(IReadOnlyList<TelemetryFrame> frames, int candidateEndIndex, int futureCount)
        {
            var maxIndex = Mathf.Min(frames.Count - 1, candidateEndIndex + futureCount);
            for (var i = candidateEndIndex + 1; i <= maxIndex; i++)
            {
                var previous = frames[i - 1];
                var current = frames[i];
                var velocity = TelemetryVelocityEstimator.Estimate(previous, current);
                if (new Vector2(velocity.x, velocity.z).magnitude > TelemetryVelocityEstimator.MaxReasonableHorizontalSpeedMetersPerSecond * 2.5f)
                {
                    return false;
                }
            }

            return true;
        }

        private static float EstimateHorizontalSpeed(PredictionContext context)
        {
            var windowFrames = context.Window.WindowFrames;
            if (windowFrames.Count < 2)
            {
                return 0f;
            }

            var startFrame = windowFrames[0];
            var endFrame = windowFrames[windowFrames.Count - 1];
            if (!TelemetryKinematicsUtility.TryGetHorizontalDisplacementMeters(startFrame, endFrame, out var displacementMeters))
            {
                return MinHorizontalSpeedMetersPerSecond;
            }

            var duration = Mathf.Max(0.001f, endFrame.ElapsedSeconds - startFrame.ElapsedSeconds);
            var speed = displacementMeters.magnitude / duration;
            return Mathf.Clamp(speed, MinHorizontalSpeedMetersPerSecond, TelemetryVelocityEstimator.MaxReasonableHorizontalSpeedMetersPerSecond);
        }

        private static float EstimateVerticalRateMagnitude(PredictionContext context)
        {
            var windowFrames = context.Window.WindowFrames;
            if (windowFrames.Count < 2)
            {
                return 0.05f;
            }

            var rateSum = 0f;
            var sampleCount = 0;
            for (var i = 1; i < windowFrames.Count; i++)
            {
                var previous = windowFrames[i - 1];
                var current = windowFrames[i];
                var deltaSeconds = Mathf.Max(0.001f, current.ElapsedSeconds - previous.ElapsedSeconds);
                rateSum += Mathf.Abs((current.DepthM - previous.DepthM) / deltaSeconds);
                sampleCount++;
            }

            return sampleCount > 0 ? Mathf.Max(0.05f, rateSum / sampleCount) : 0.05f;
        }

        private static float EstimateHalfCycleSeconds(PredictionContext context)
        {
            var windowFrames = context.Window.WindowFrames;
            if (windowFrames.Count < 3)
            {
                return DefaultHalfCycleSeconds;
            }

            var intervals = new System.Collections.Generic.List<float>();
            var lastFlipSeconds = windowFrames[0].ElapsedSeconds;
            var previousSign = ResolvePitchSign(windowFrames[0], windowFrames[1]);
            for (var i = 2; i < windowFrames.Count; i++)
            {
                var sign = ResolvePitchSign(windowFrames[i - 1], windowFrames[i]);
                if (sign != 0 && previousSign != 0 && sign != previousSign)
                {
                    intervals.Add(windowFrames[i].ElapsedSeconds - lastFlipSeconds);
                    lastFlipSeconds = windowFrames[i].ElapsedSeconds;
                }

                if (sign != 0)
                {
                    previousSign = sign;
                }
            }

            if (intervals.Count == 0)
            {
                return DefaultHalfCycleSeconds;
            }

            var sum = 0f;
            for (var i = 0; i < intervals.Count; i++)
            {
                sum += intervals[i];
            }

            return Mathf.Max(MinHalfCycleSeconds, sum / intervals.Count);
        }

        private static float EstimateHeadingRate(PredictionContext context)
        {
            var windowFrames = context.Window.WindowFrames;
            if (windowFrames.Count < 2)
            {
                return 0f;
            }

            var deltaHeading = Mathf.DeltaAngle(windowFrames[0].HeadingDeg, windowFrames[windowFrames.Count - 1].HeadingDeg);
            var deltaSeconds = Mathf.Max(0.001f, windowFrames[windowFrames.Count - 1].ElapsedSeconds - windowFrames[0].ElapsedSeconds);
            return deltaHeading / deltaSeconds;
        }

        private static float EstimatePitchAmplitude(PredictionContext context)
        {
            var windowFrames = context.Window.WindowFrames;
            var pitchSum = 0f;
            for (var i = 0; i < windowFrames.Count; i++)
            {
                pitchSum += Mathf.Abs(windowFrames[i].PitchDeg);
            }

            return windowFrames.Count > 0 ? Mathf.Clamp(pitchSum / windowFrames.Count, 6f, 28f) : 12f;
        }

        private static bool ResolveDescendingState(PredictionContext context)
        {
            var current = context.CurrentFrame;
            if (Mathf.Abs(current.PitchDeg) > 0.5f)
            {
                return current.PitchDeg < 0f;
            }

            var windowFrames = context.Window.WindowFrames;
            if (windowFrames.Count < 2)
            {
                return true;
            }

            return windowFrames[windowFrames.Count - 1].DepthM >= windowFrames[windowFrames.Count - 2].DepthM;
        }

        private static float EstimateCurrentPhaseElapsedSeconds(PredictionContext context, bool descending)
        {
            var windowFrames = context.Window.WindowFrames;
            var elapsed = 0f;
            for (var i = windowFrames.Count - 1; i > 0; i--)
            {
                var previous = windowFrames[i - 1];
                var current = windowFrames[i];
                var sampleDescending = ResolvePitchSign(previous, current) <= 0;
                if (sampleDescending != descending)
                {
                    break;
                }

                elapsed += Mathf.Max(0f, current.ElapsedSeconds - previous.ElapsedSeconds);
            }

            return elapsed;
        }

        private static int ResolvePitchSign(TelemetryFrame previous, TelemetryFrame current)
        {
            if (Mathf.Abs(current.PitchDeg) > 0.5f)
            {
                return current.PitchDeg < 0f ? -1 : 1;
            }

            var depthDelta = current.DepthM - previous.DepthM;
            if (Mathf.Abs(depthDelta) < 0.001f)
            {
                return 0;
            }

            return depthDelta > 0f ? -1 : 1;
        }

        private static void SimulateGlideStep(
            float deltaSeconds,
            float horizontalSpeed,
            float verticalRate,
            float halfCycleSeconds,
            float headingRate,
            float pitchAmplitude,
            ref float headingDeg,
            ref bool descending,
            ref float phaseElapsedSeconds,
            ref float depthM,
            ref Vector3 horizontalOffset)
        {
            var remainingSeconds = deltaSeconds;
            while (remainingSeconds > 0.0001f)
            {
                var secondsToFlip = Mathf.Max(0.0001f, halfCycleSeconds - phaseElapsedSeconds);
                var stepSeconds = Mathf.Min(remainingSeconds, secondsToFlip);
                headingDeg += headingRate * stepSeconds;
                var pitchDeg = descending ? -pitchAmplitude : pitchAmplitude;
                var forward = PoseToDirection(headingDeg, pitchDeg);
                horizontalOffset += forward * (horizontalSpeed * stepSeconds);
                depthM += (descending ? 1f : -1f) * verticalRate * stepSeconds;
                phaseElapsedSeconds += stepSeconds;
                remainingSeconds -= stepSeconds;

                if (phaseElapsedSeconds >= halfCycleSeconds - 0.0001f)
                {
                    phaseElapsedSeconds = 0f;
                    descending = !descending;
                }
            }
        }

        private static Vector3 PoseToDirection(float headingDeg, float pitchDeg)
        {
            var headingRad = headingDeg * Mathf.Deg2Rad;
            var pitchRad = pitchDeg * Mathf.Deg2Rad;
            var x = Mathf.Sin(headingRad) * Mathf.Cos(pitchRad);
            var z = Mathf.Cos(headingRad) * Mathf.Cos(pitchRad);
            return new Vector3(x, 0f, z).normalized;
        }

        private static Vector2 RotateHorizontal(Vector2 vector, float yawDeg)
        {
            var yawRad = yawDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(yawRad);
            var sin = Mathf.Sin(yawRad);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
        }
    }
}
