using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class TrajectoryView : MonoBehaviour
    {
        private LineRenderer backdropLine;
        private LineRenderer actualLine;
        private LineRenderer futureActualLine;
        private LineRenderer predictedLine;
        private LineRenderer predictedHistoryLine;
        private LineRenderer plannedLine;
        private LineRenderer intendedLine;
        private PlaybackController playback;
        private PredictionController prediction;
        private GeoCoordinateMapper mapper;
        private FittedTrajectory fittedTrajectory;
        private Vector3[] actualBuffer = Array.Empty<Vector3>();
        private Vector3[] futureBuffer = Array.Empty<Vector3>();
        private Vector3[] intendedBuffer = Array.Empty<Vector3>();
        private int replacementPreservedIndex = -1;
        private int replacementHistoryPointCount;
        private int replacementMappedThroughIndex = -1;
        private int lastActualCount = -1;
        private bool isVisible;
        private CameraMode cameraMode = CameraMode.Follow;
        private float widthScale = 1f;
        private Vector3 currentActualWorldPoint;
        private int currentPlaybackIndex;
        private GameObject startMarker;
        private GameObject targetMarker;
        private GameObject currentMarker;
        private readonly SortedDictionary<int, PredictionSnapshot> predictionSnapshots = new SortedDictionary<int, PredictionSnapshot>();
        private readonly List<Vector3> predictedHistoryBuffer = new List<Vector3>();

        public Vector3[] FullTrajectoryPoints { get; private set; } = Array.Empty<Vector3>();
        public FittedTrajectory FittedTrajectory => fittedTrajectory;

        public void Initialize(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, PlaybackController playbackController, PredictionController predictionController)
        {
            playback = playbackController;
            prediction = predictionController;
            this.mapper = mapper;
            predictionSnapshots.Clear();
            predictedHistoryBuffer.Clear();
            lastActualCount = -1;
            currentPlaybackIndex = 0;
            replacementPreservedIndex = -1;
            replacementHistoryPointCount = 0;
            replacementMappedThroughIndex = -1;
            fittedTrajectory = TrajectorySampler.Fit(frames, mapper, 1200);
            FullTrajectoryPoints = new List<Vector3>(fittedTrajectory.Points).ToArray();
            intendedBuffer = BuildIntendedPoints(frames, mapper);
            actualBuffer = new Vector3[FullTrajectoryPoints.Length];
            futureBuffer = new Vector3[FullTrajectoryPoints.Length];
            Array.Copy(FullTrajectoryPoints, actualBuffer, FullTrajectoryPoints.Length);

            backdropLine = CreateLine("ActualBackdropLine", new Color(0.18f, 0.48f, 0.6f, 0.035f), 0.035f);
            backdropLine.positionCount = FullTrajectoryPoints.Length;
            backdropLine.SetPositions(FullTrajectoryPoints);

            futureActualLine = CreateLine("RemainingTrajectoryLine", new Color(0.45f, 0.88f, 1f, 0.32f), 0.08f);
            actualLine = CreateLine("ActualTrajectoryLine", new Color(0.35f, 0.92f, 1f, 1f), 0.17f);
            predictedLine = CreateLine("PredictedTrajectoryLine", new Color(1f, 0.9f, 0.28f, 0.98f), 0.13f);
            predictedHistoryLine = CreateLine("PredictedHistoryLine", new Color(1f, 0.82f, 0.18f, 0.16f), 0.05f);
            plannedLine = CreateLine("PlannedTrajectoryLine", new Color(0.3f, 0.92f, 0.52f, 0.9f), 0.08f);
            intendedLine = CreateLine("IntendedTrajectoryLine", new Color(0.34f, 0.95f, 0.53f, 0.92f), 0.11f);
            intendedLine.positionCount = intendedBuffer.Length;
            if (intendedBuffer.Length > 0)
            {
                intendedLine.SetPositions(intendedBuffer);
            }

            if (FullTrajectoryPoints.Length > 1)
            {
                startMarker = CreateMarker("StartMarker", FullTrajectoryPoints[0], new Color(0.18f, 0.72f, 1f, 1f), 0.42f);
                targetMarker = CreateMarker("TargetMarker", FullTrajectoryPoints[FullTrajectoryPoints.Length - 1], new Color(0.3f, 0.92f, 0.52f, 1f), 0.5f);
                currentMarker = CreateMarker("CurrentPositionMarker", FullTrajectoryPoints[0], new Color(1f, 0.92f, 0.2f, 1f), 0.45f);
            }

            playback.FrameChangedWithReason += OnFrameChangedWithReason;
            playback.ContinuousChanged += OnContinuousChanged;
            if (prediction != null)
            {
                prediction.SnapshotUpdated += OnPredictionUpdated;
            }

            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
            if (prediction != null)
            {
                OnPredictionUpdated(prediction.CurrentSnapshot);
            }

            ApplyWidthScale();
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            RefreshVisibility();
        }

        public void SetCameraMode(CameraMode mode)
        {
            cameraMode = mode;
            if (playback == null)
            {
                return;
            }

            lastActualCount = -1;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
            RefreshVisibility();
        }

        public void SetWidthScale(float scale)
        {
            widthScale = Mathf.Clamp(scale, 0.6f, 2.4f);
            ApplyWidthScale();
        }

        public void ReplaceFutureTrajectory(IReadOnlyList<TelemetryFrame> frames, int preservedIndex)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (preservedIndex < 0 || preservedIndex >= frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(preservedIndex));
            }

            CaptureRenderedHistory(frames, preservedIndex);
            replacementPreservedIndex = preservedIndex;
            replacementMappedThroughIndex = preservedIndex;
            fittedTrajectory = TrajectorySampler.Fit(frames, mapper, 1200);
            FullTrajectoryPoints = new List<Vector3>(fittedTrajectory.Points).ToArray();
            intendedBuffer = BuildIntendedPoints(frames, mapper);
            futureBuffer = new List<Vector3>(TrajectorySampler.Fit(frames.Skip(preservedIndex).ToArray(), mapper, 1200).Points).ToArray();
            UpdateBackdropAndTarget();
            if (intendedLine != null)
            {
                intendedLine.positionCount = intendedBuffer.Length;
                if (intendedBuffer.Length > 0)
                {
                    intendedLine.SetPositions(intendedBuffer);
                }
            }

            futureActualLine.positionCount = futureBuffer.Length;
            if (futureBuffer.Length > 0)
            {
                futureActualLine.SetPositions(futureBuffer);
            }

            UpdatePlannedLine();
            RefreshVisibility();
        }

        public void ReplaceFutureTrajectory(SimulationTimelineSnapshot snapshot, int preservedIndex)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ReplaceFutureTrajectory(snapshot.Frames, preservedIndex);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChangedWithReason;
                playback.ContinuousChanged -= OnContinuousChanged;
            }

            if (prediction != null)
            {
                prediction.SnapshotUpdated -= OnPredictionUpdated;
            }
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01)
        {
            if (actualLine == null || FullTrajectoryPoints.Length == 0)
            {
                return;
            }

            currentPlaybackIndex = index;
            if (replacementPreservedIndex >= 0)
            {
                UpdateActualLineAfterReplacement(frame, index);
                return;
            }

            var count = Mathf.Clamp(Mathf.CeilToInt(progress01 * (FullTrajectoryPoints.Length - 1)) + 1, 1, FullTrajectoryPoints.Length);
            if (count == lastActualCount)
            {
                currentActualWorldPoint = ResolveFittedPosition(frame);
                if (actualLine.positionCount > 0)
                {
                    actualLine.SetPosition(actualLine.positionCount - 1, currentActualWorldPoint);
                }
                if (currentMarker != null)
                {
                    currentMarker.transform.position = currentActualWorldPoint;
                }
                RebuildPredictedHistory();
                return;
            }

            actualLine.positionCount = count;
            var visibleBuffer = new Vector3[count];
            Array.Copy(actualBuffer, visibleBuffer, count);
            actualLine.SetPositions(visibleBuffer);
            currentActualWorldPoint = ResolveFittedPosition(frame);
            actualLine.SetPosition(count - 1, currentActualWorldPoint);
            if (currentMarker != null)
            {
                currentMarker.transform.position = currentActualWorldPoint;
            }
            UpdateFutureActualLine(count);
            RebuildPredictedHistory();
            UpdatePlannedLine();
            lastActualCount = count;
        }

        private Vector3 ResolveFittedPosition(TelemetryFrame frame)
        {
            if (fittedTrajectory != null && playback != null)
            {
                return fittedTrajectory.PositionAt(playback.Model.ContinuousElapsedSeconds);
            }

            return mapper != null && TelemetryPositionUtility.HasUsableCoordinates(frame)
                ? mapper.Map(frame)
                : actualBuffer.Length > 0 ? actualBuffer[Mathf.Max(0, actualBuffer.Length - 1)] : Vector3.zero;
        }

        private void OnFrameChangedWithReason(
            TelemetryFrame frame,
            int index,
            float progress01,
            FrameUpdateReason reason)
        {
            if (reason == FrameUpdateReason.Rebuild)
            {
                return;
            }

            OnFrameChanged(frame, index, progress01);
        }

        private void OnContinuousChanged(float continuousIndex, float progress01, FrameUpdateReason reason)
        {
            if (reason == FrameUpdateReason.Rebuild)
            {
                return;
            }

            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, progress01);
        }

        private void OnPredictionUpdated(PredictionSnapshot snapshot)
        {
            if (predictedLine == null)
            {
                return;
            }

            if (snapshot == null || snapshot.SampleCount < 2)
            {
                predictedLine.positionCount = 0;
                if (snapshot == null || snapshot.SampleCount == 0)
                {
                    predictionSnapshots.Clear();
                    predictedHistoryBuffer.Clear();
                    predictedHistoryLine.positionCount = 0;
                }

                RefreshVisibility();
                return;
            }

            predictionSnapshots[snapshot.StartIndex] = snapshot;
            predictedLine.positionCount = 0;
            RebuildPredictedHistory();
            UpdatePlannedLine();
            RefreshVisibility();
        }

        private void UpdatePlannedLine()
        {
            if (plannedLine == null || targetMarker == null || playback == null)
            {
                return;
            }

            var currentPosition = prediction != null && prediction.CurrentSnapshot.SampleCount > 0
                ? prediction.CurrentSnapshot.OriginPoint
                : currentActualWorldPoint;

            plannedLine.positionCount = 2;
            plannedLine.SetPosition(0, currentPosition);
            plannedLine.SetPosition(1, targetMarker.transform.position);
        }

        private LineRenderer CreateLine(string lineName, Color color, float width)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Tile;
            line.sortingOrder = lineName == "PredictedHistoryLine" ? 6
                : lineName == "ActualTrajectoryLine" ? 5
                : lineName == "PredictedTrajectoryLine" ? 4
                : lineName == "RemainingTrajectoryLine" ? 2
                : 1;
            line.sharedMaterial = RuntimeMaterialFactory.Line(lineName + "Material", color);
            line.sharedMaterial.renderQueue = 3100;
            return line;
        }

        private GameObject CreateMarker(string markerName, Vector3 position, Color color, float scale)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;
            marker.transform.SetParent(transform, false);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * scale;
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterialFactory.Opaque(markerName + "Material", color);
            renderer.sharedMaterial.renderQueue = 3101;
            return marker;
        }

        private void RefreshVisibility()
        {
            if (actualLine == null || predictedLine == null || backdropLine == null || plannedLine == null || futureActualLine == null || predictedHistoryLine == null || intendedLine == null)
            {
                return;
            }

            actualLine.gameObject.SetActive(isVisible);
            futureActualLine.gameObject.SetActive(isVisible && futureActualLine.positionCount > 1);
            predictedLine.gameObject.SetActive(isVisible && predictedLine.positionCount > 1);
            predictedHistoryLine.gameObject.SetActive(isVisible && predictedHistoryLine.positionCount > 1);
            backdropLine.gameObject.SetActive(isVisible && cameraMode != CameraMode.Follow);
            plannedLine.gameObject.SetActive(isVisible && plannedLine.positionCount > 1);
            intendedLine.gameObject.SetActive(isVisible && intendedLine.positionCount > 1);
            if (startMarker != null)
            {
                startMarker.SetActive(isVisible && cameraMode != CameraMode.Follow);
            }

            if (targetMarker != null)
            {
                targetMarker.SetActive(isVisible);
            }

            if (currentMarker != null)
            {
                currentMarker.SetActive(isVisible);
            }
        }

        private void ApplyWidthScale()
        {
            if (actualLine == null || predictedLine == null || backdropLine == null || plannedLine == null || futureActualLine == null || predictedHistoryLine == null || intendedLine == null)
            {
                return;
            }

            actualLine.widthMultiplier = 0.17f * widthScale;
            futureActualLine.widthMultiplier = 0.09f * widthScale;
            predictedLine.widthMultiplier = 0.13f * widthScale;
            predictedHistoryLine.widthMultiplier = 0.08f * widthScale;
            plannedLine.widthMultiplier = 0.08f * widthScale;
            intendedLine.widthMultiplier = 0.11f * widthScale;
            backdropLine.widthMultiplier = 0.05f * widthScale;
        }

        private static Vector3[] BuildIntendedPoints(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper)
        {
            var plannedFrames = new List<TelemetryFrame>();
            foreach (var frame in frames)
            {
                if (!frame.HasPlannedPosition)
                {
                    continue;
                }

                var plannedFrame = new TelemetryFrame(
                    frame.RowIndex, frame.RawTime, frame.ElapsedSeconds, frame.PlannedLongitudeDeg, frame.PlannedLatitudeDeg,
                    frame.DepthM, frame.AltitudeM, frame.HeadingDeg, frame.PitchDeg, frame.RollDeg,
                    frame.Voltage24V, frame.Current24A, frame.BatteryPercent, frame.WorkMode, frame.RunState,
                    frame.TargetSegment, frame.TargetHeadingDeg, frame.TargetDepthM, frame.TargetAltitudeM,
                    frame.PropellerRpm, frame.PistonMm, frame.TurnAngleDeg);
                plannedFrames.Add(plannedFrame);
            }

            return TrajectorySampler.Sample(plannedFrames, mapper, 1200);
        }

        private void UpdateFutureActualLine(int actualCount)
        {
            if (futureActualLine == null)
            {
                return;
            }

            if (replacementPreservedIndex >= 0)
            {
                var remainingFrames = playback != null
                    ? playback.Model.Frames.Skip(Mathf.Clamp(currentPlaybackIndex, 0, playback.Model.FrameCount - 1)).ToArray()
                    : Array.Empty<TelemetryFrame>();
                futureBuffer = new List<Vector3>(TrajectorySampler.Fit(remainingFrames, mapper, 1200).Points).ToArray();
                futureActualLine.positionCount = futureBuffer.Length;
                if (futureBuffer.Length > 0)
                {
                    futureActualLine.SetPositions(futureBuffer);
                }

                return;
            }

            if (FullTrajectoryPoints.Length < 2)
            {
                return;
            }

            var startIndex = Mathf.Clamp(actualCount - 1, 0, FullTrajectoryPoints.Length - 1);
            var remainingCount = FullTrajectoryPoints.Length - startIndex;
            if (remainingCount < 2)
            {
                futureActualLine.positionCount = 0;
                return;
            }

            Array.Copy(actualBuffer, startIndex, futureBuffer, 0, remainingCount);
            futureActualLine.positionCount = remainingCount;
            futureActualLine.SetPositions(futureBuffer);
        }

        private void CaptureRenderedHistory(IReadOnlyList<TelemetryFrame> frames, int preservedIndex)
        {
            var renderedCount = actualLine != null ? actualLine.positionCount : 0;
            if (renderedCount > 0)
            {
                actualBuffer = new Vector3[renderedCount];
                actualLine.GetPositions(actualBuffer);
            }
            else
            {
                actualBuffer = new List<Vector3>(TrajectorySampler.Fit(frames.Take(preservedIndex + 1).ToArray(), mapper, 1200).Points).ToArray();
            }

            replacementHistoryPointCount = actualBuffer.Length;
            lastActualCount = replacementHistoryPointCount;
        }

        private Vector3[] BuildCombinedTrajectory(IReadOnlyList<TelemetryFrame> frames, int preservedIndex)
        {
            var history = new List<Vector3>(TrajectorySampler.Fit(frames.Take(preservedIndex + 1).ToArray(), mapper, 1200).Points).ToArray();
            var future = new List<Vector3>(TrajectorySampler.Fit(frames.Skip(preservedIndex).ToArray(), mapper, 1200).Points).ToArray();
            if (history.Length == 0)
            {
                return future;
            }

            if (future.Length == 0)
            {
                return history;
            }

            var startsWithHistoryEndpoint = future[0] == history[history.Length - 1];
            var combined = new Vector3[history.Length + future.Length - (startsWithHistoryEndpoint ? 1 : 0)];
            Array.Copy(history, combined, history.Length);
            Array.Copy(future, startsWithHistoryEndpoint ? 1 : 0, combined, history.Length, future.Length - (startsWithHistoryEndpoint ? 1 : 0));
            return combined;
        }

        private void UpdateBackdropAndTarget()
        {
            if (backdropLine != null)
            {
                backdropLine.positionCount = FullTrajectoryPoints.Length;
                if (FullTrajectoryPoints.Length > 0)
                {
                    backdropLine.SetPositions(FullTrajectoryPoints);
                }
            }

            if (targetMarker != null && FullTrajectoryPoints.Length > 0)
            {
                targetMarker.transform.position = FullTrajectoryPoints[FullTrajectoryPoints.Length - 1];
            }
        }

        private void UpdateActualLineAfterReplacement(TelemetryFrame frame, int index)
        {
            if (index < replacementPreservedIndex)
            {
                RenderRewoundHistory(frame);
                return;
            }

            var additionalFrames = Mathf.Max(0, index - replacementPreservedIndex);
            var targetCount = replacementHistoryPointCount + additionalFrames;
            if (targetCount > actualBuffer.Length)
            {
                Array.Resize(ref actualBuffer, targetCount);
            }

            var startIndex = Mathf.Max(replacementPreservedIndex + 1, replacementMappedThroughIndex + 1);
            for (var frameIndex = startIndex; frameIndex <= index; frameIndex++)
            {
                var targetIndex = replacementHistoryPointCount + frameIndex - replacementPreservedIndex - 1;
                actualBuffer[targetIndex] = fittedTrajectory != null
                    ? fittedTrajectory.PositionAt(playback.Model.Frames[frameIndex].ElapsedSeconds)
                    : mapper.Map(playback.Model.Frames[frameIndex]);
            }

            replacementMappedThroughIndex = Mathf.Max(replacementMappedThroughIndex, index);
            if (actualLine != null && targetCount > 0)
            {
                var visible = new Vector3[targetCount];
                Array.Copy(actualBuffer, visible, targetCount);
                actualLine.positionCount = targetCount;
                actualLine.SetPositions(visible);
            }

            currentActualWorldPoint = ResolveFittedPosition(frame);
            if (currentMarker != null)
            {
                currentMarker.transform.position = currentActualWorldPoint;
            }

            UpdateFutureActualLine(targetCount);
            RebuildPredictedHistory();
            UpdatePlannedLine();
            lastActualCount = targetCount;
        }

        private void RenderRewoundHistory(TelemetryFrame frame)
        {
            var elapsedSeconds = playback.Model.ContinuousElapsedSeconds;
            var historyFrames = playback.Model.Frames
                .TakeWhile(candidate => candidate.ElapsedSeconds <= elapsedSeconds + 0.0001f)
                .ToArray();
            if (historyFrames.Length == 0)
            {
                historyFrames = new[] { playback.Model.Frames[0] };
            }

            var visible = new List<Vector3>(TrajectorySampler.SampleSmooth(historyFrames, mapper, 1200));
            var currentPoint = mapper != null && TelemetryPositionUtility.HasUsableCoordinates(frame)
                ? mapper.Map(frame)
                : visible.Count > 0 ? visible[visible.Count - 1] : Vector3.zero;
            if (visible.Count == 0)
            {
                visible.Add(currentPoint);
            }
            else if ((visible[visible.Count - 1] - currentPoint).sqrMagnitude > 0.000001f)
            {
                visible.Add(currentPoint);
            }
            else
            {
                visible[visible.Count - 1] = currentPoint;
            }

            actualLine.positionCount = visible.Count;
            actualLine.SetPositions(visible.ToArray());
            currentActualWorldPoint = currentPoint;
            if (currentMarker != null)
            {
                currentMarker.transform.position = currentActualWorldPoint;
            }

            UpdateFutureActualLine(visible.Count);
            RebuildPredictedHistory();
            UpdatePlannedLine();
            lastActualCount = visible.Count;
        }

        private void RebuildPredictedHistory()
        {
            if (predictedHistoryLine == null)
            {
                return;
            }

            predictedHistoryBuffer.Clear();
            foreach (var snapshot in predictionSnapshots.Values.OrderBy(entry => entry.StartIndex))
            {
                if (snapshot.SampleCount == 0 || snapshot.StartIndex > currentPlaybackIndex)
                {
                    continue;
                }

                AppendPredictedHistoryPoint(snapshot.PredictedPoints[0]);
            }

            predictedHistoryLine.positionCount = predictedHistoryBuffer.Count;
            if (predictedHistoryBuffer.Count > 0)
            {
                predictedHistoryLine.SetPositions(predictedHistoryBuffer.ToArray());
            }
        }

        private void AppendPredictedHistoryPoint(Vector3 point)
        {
            if (predictedHistoryBuffer.Count > 0 && Vector3.Distance(predictedHistoryBuffer[predictedHistoryBuffer.Count - 1], point) < 0.05f)
            {
                return;
            }

            predictedHistoryBuffer.Add(point);
        }
    }
}
