using System;
using System.Collections.Generic;
using System.IO;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class PredictionController : MonoBehaviour
    {
        private const int DefaultWindowSize = 30;
        private readonly Dictionary<PredictionModelKind, IPredictor> predictors = new Dictionary<PredictionModelKind, IPredictor>();
        private readonly Dictionary<PredictionModelKind, string> modelErrors = new Dictionary<PredictionModelKind, string>();
        private IReadOnlyList<TelemetryFrame> frames;
        private GeoCoordinateMapper mapper;
        private PlaybackController playback;

        public event Action<PredictionSnapshot> SnapshotUpdated;

        public PredictionSnapshot CurrentSnapshot { get; private set; } = PredictionSnapshot.Empty;
        public PredictionModelKind ModelKind { get; private set; } = RuntimePredictionState.ModelKind;
        public float HorizonSeconds { get; private set; } = RuntimePredictionState.HorizonSeconds;
        public bool PredictionEnabled { get; private set; } = RuntimePredictionState.PredictionEnabled;
        public IPredictorFactory PredictorFactory { get; set; } = new XGBoostPredictorFactory();

        public bool IsModelRuntimeAvailable(PredictionModelKind modelKind)
        {
            return predictors.TryGetValue(modelKind, out var predictor) && predictor != null;
        }

        public void Initialize(IReadOnlyList<TelemetryFrame> telemetryFrames, GeoCoordinateMapper coordinateMapper, PlaybackController playbackController)
        {
            frames = telemetryFrames ?? throw new ArgumentNullException(nameof(telemetryFrames));
            mapper = coordinateMapper ?? throw new ArgumentNullException(nameof(coordinateMapper));
            playback = playbackController ?? throw new ArgumentNullException(nameof(playbackController));
            BuildPredictorRegistry();
            if (!IsModelRuntimeAvailable(ModelKind))
            {
                PredictionEnabled = false;
                RuntimePredictionState.SetEnabled(false);
            }
            playback.FrameChangedWithReason += OnFrameChanged;
            playback.Model.FramesReplaced += OnFramesReplaced;
            Recompute(playback.Model.CurrentIndex);
        }

        public void SetModelKind(PredictionModelKind modelKind)
        {
            TrySetModelKind(modelKind, out _);
        }

        public bool TrySetModelKind(PredictionModelKind modelKind, out string error)
        {
            if (!IsModelRuntimeAvailable(modelKind))
            {
                error = GetModelUnavailableError(modelKind);
                return false;
            }

            error = string.Empty;
            ModelKind = modelKind;
            RuntimePredictionState.SetModelKind(modelKind);
            Recompute(playback != null ? playback.Model.CurrentIndex : 0);
            return true;
        }

        public void SetHorizonSeconds(float seconds)
        {
            HorizonSeconds = Mathf.Clamp(seconds, 30f, 7200f);
            RuntimePredictionState.SetHorizonSeconds(HorizonSeconds);
            Recompute(playback != null ? playback.Model.CurrentIndex : 0);
        }

        public void SetPredictionEnabled(bool enabled)
        {
            PredictionEnabled = enabled;
            RuntimePredictionState.SetEnabled(enabled);
            Recompute(playback != null ? playback.Model.CurrentIndex : 0);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
                playback.Model.FramesReplaced -= OnFramesReplaced;
            }

            foreach (var predictor in predictors.Values)
            {
                predictor.Release();
            }
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            Recompute(index);
        }

        private void OnFramesReplaced(IReadOnlyList<TelemetryFrame> replacement, int preservedIndex)
        {
            frames = replacement;
            Recompute(preservedIndex);
        }

        private void Recompute(int currentIndex)
        {
            if (!PredictionEnabled || frames == null || mapper == null || playback == null || frames.Count < 3)
            {
                Publish(PredictionSnapshot.Empty);
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, frames.Count - 1);
            var currentFrame = frames[currentIndex];
            if (!TelemetryPositionUtility.HasUsableCoordinates(currentFrame))
            {
                Publish(new PredictionSnapshot(Vector3.zero, Array.Empty<Vector3>(), Array.Empty<Vector3>(), 0f, 0f, 0f, 0f, 0f, currentIndex, currentIndex, "Awaiting valid telemetry position", 0f));
                return;
            }

            var averageDeltaSeconds = EstimateAverageDeltaSeconds(currentIndex);
            var horizonPoints = Mathf.Max(10, Mathf.RoundToInt(HorizonSeconds / Mathf.Max(averageDeltaSeconds, 1f)));
            var window = PredictionWindowBuilder.Build(frames, new PredictionRequest(currentIndex, DefaultWindowSize, horizonPoints, HorizonSeconds));
            if (window.FutureFrames.Count == 0)
            {
                Publish(new PredictionSnapshot(mapper.Map(currentFrame), Array.Empty<Vector3>(), Array.Empty<Vector3>(), 0f, 0f, 0f, 0f, 0f, currentIndex, currentIndex, "Prediction window exhausted", 0f));
                return;
            }

            if (!predictors.TryGetValue(ModelKind, out var predictor))
            {
                Publish(new PredictionSnapshot(mapper.Map(currentFrame), Array.Empty<Vector3>(), Array.Empty<Vector3>(), 0f, 0f, 0f, 0f, 0f, currentIndex, currentIndex, "Predictor not registered", 0f));
                return;
            }

            var result = predictor.Predict(new PredictionContext(frames, mapper, window));
            Publish(ToSnapshot(mapper.Map(currentFrame), result));
        }

        private void BuildPredictorRegistry()
        {
            predictors.Clear();
            modelErrors.Clear();
            var modelRoot = RuntimePathResolver.ResolveModelsDirectory();
            RegisterPredictor(PredictionModelKind.XGBoost, Path.Combine(modelRoot, "XGBoost"));
        }

        private void RegisterPredictor(PredictionModelKind modelKind, string root)
        {
            var factory = PredictorFactory ?? new XGBoostPredictorFactory();
            if (factory.TryCreate(root, out var predictor, out var error))
            {
                predictors[modelKind] = predictor;
                return;
            }

            modelErrors[modelKind] = string.IsNullOrWhiteSpace(error)
                ? $"{modelKind} predictor is unavailable."
                : error;
        }

        private string GetModelUnavailableError(PredictionModelKind modelKind)
        {
            return modelErrors.TryGetValue(modelKind, out var error) && !string.IsNullOrWhiteSpace(error)
                ? error
                : $"{modelKind} prediction model is unavailable.";
        }

        private void Publish(PredictionSnapshot snapshot)
        {
            CurrentSnapshot = snapshot ?? PredictionSnapshot.Empty;
            SnapshotUpdated?.Invoke(CurrentSnapshot);
        }

        private static PredictionSnapshot ToSnapshot(Vector3 originPoint, PredictionResult result)
        {
            if (result == null)
            {
                return PredictionSnapshot.Empty;
            }

            return new PredictionSnapshot(
                originPoint,
                result.PredictedPoints,
                result.ActualPoints,
                result.Metrics.RmseMeters,
                result.Metrics.MaeMeters,
                result.Metrics.CurrentErrorMeters,
                result.Metrics.MaximumErrorMeters,
                result.Metrics.Confidence01,
                result.StartIndex,
                result.EndIndex,
                result.Status,
                result.Metrics.ComputeMilliseconds);
        }

        private float EstimateAverageDeltaSeconds(int currentIndex)
        {
            var samples = 0;
            var seconds = 0f;
            for (var i = Mathf.Max(1, currentIndex - 10); i <= currentIndex; i++)
            {
                seconds += Mathf.Max(0f, frames[i].ElapsedSeconds - frames[i - 1].ElapsedSeconds);
                samples++;
            }

            return samples > 0 ? seconds / samples : 1f;
        }
    }
}
