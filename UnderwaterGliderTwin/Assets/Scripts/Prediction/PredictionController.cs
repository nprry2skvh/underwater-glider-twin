using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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
        private SimulationTrajectoryTimeline timeline;
        private readonly Dictionary<string, PredictionResult> frozenResults = new Dictionary<string, PredictionResult>();
        private readonly Dictionary<int, string> profileBranches = new Dictionary<int, string>();
        private readonly List<ForecastBranch> branchStarts = new List<ForecastBranch>();
        private string branchId = "branch-0";
        private string modelHash = "unavailable";
        private string currentVersion = "not_provided";
        private int branchRevision;
        private int observedThroughIndex = -1;

        public ForecastLedger Ledger { get; } = new ForecastLedger(Guid.NewGuid().ToString("N"));

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
            if (playback.Model.IsTimelineBound)
            {
                BindTimeline(playback.Model.Timeline);
            }
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

        public void BindTimeline(SimulationTrajectoryTimeline replacement)
        {
            timeline = replacement ?? throw new ArgumentNullException(nameof(replacement));
            frames = replacement.CommittedSnapshot.Frames;
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
            HorizonSeconds = seconds;
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
            var next = timeline != null ? timeline.CommittedSnapshot.Frames : replacement;
            var appendOnly = next.Count >= frames.Count;
            for (var index = 0; appendOnly && index < frames.Count; index++)
                appendOnly = frames[index].Equals(next[index]);
            if (!appendOnly)
            {
                branchId = "branch-" + (++branchRevision);
                observedThroughIndex = Math.Min(observedThroughIndex, preservedIndex);
                var sequence = next[Math.Min(preservedIndex + 1, next.Count - 1)].ProfileSequence;
                var profile = timeline != null && timeline.CommittedSnapshot.Segments.Count > 0
                    ? timeline.CommittedSnapshot.Segments[timeline.CommittedSnapshot.Segments.Count - 1].Profile
                    : RuntimeDataSourceState.SimulationProfile;
                currentVersion = ForecastLineage.HashCurrent(profile);
                var startRow = next[preservedIndex].RowIndex;
                branchStarts.RemoveAll(branch => branch.StartRow >= startRow);
                branchStarts.Add(new ForecastBranch(startRow, branchId, sequence, currentVersion));
                profileBranches[sequence] = branchId;
            }
            frames = next;
            Recompute(preservedIndex);
        }

        private void Recompute(int currentIndex)
        {
            if (frames == null || mapper == null || playback == null || frames.Count == 0)
            {
                Publish(PredictionSnapshot.Empty);
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, frames.Count - 1);
            var currentFrame = frames[currentIndex];
            for (var index = observedThroughIndex + 1; index <= currentIndex; index++)
            {
                var observed = frames[index];
                if (!profileBranches.TryGetValue(observed.ProfileSequence, out var observedBranch)) observedBranch = branchId;
                Ledger.Observe(observed, observed.ElapsedSeconds, observedBranch,
                    RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation,
                    TelemetryPositionUtility.HasUsableCoordinates(observed),
                    RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation ? "simulation_branch" : "navigation_record_unverified");
            }
            observedThroughIndex = Math.Max(observedThroughIndex, currentIndex);
            if (!PredictionEnabled || frames.Count < 3)
            {
                Publish(PredictionSnapshot.Empty);
                return;
            }
            if (!TelemetryPositionUtility.HasUsableCoordinates(currentFrame))
            {
                Publish(new PredictionSnapshot(Vector3.zero, Array.Empty<Vector3>(), Array.Empty<Vector3>(), 0f, 0f, 0f, 0f, 0f, currentIndex, currentIndex, "Awaiting valid telemetry position", 0f));
                return;
            }

            var window = PredictionWindowBuilder.BuildForecast(frames, new PredictionRequest(currentIndex, DefaultWindowSize, 90, HorizonSeconds));

            if (!predictors.TryGetValue(ModelKind, out var predictor))
            {
                Publish(new PredictionSnapshot(mapper.Map(currentFrame), Array.Empty<Vector3>(), Array.Empty<Vector3>(), 0f, 0f, 0f, 0f, 0f, currentIndex, currentIndex, "Predictor not registered", 0f));
                return;
            }

            var issueBranch = branchStarts[0];
            foreach (var branch in branchStarts)
                if (branch.StartRow <= currentFrame.RowIndex) issueBranch = branch;
            var key = issueBranch.Id + ":" + ModelKind + ":" + modelHash + ":"
                + currentFrame.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture) + ":"
                + HorizonSeconds.ToString("R", CultureInfo.InvariantCulture);
            if (!frozenResults.TryGetValue(key, out var result))
            {
                FrozenForecast record;
                var digest = ForecastLineage.HashHistory(window.WindowFrames);
                try
                {
                    result = predictor.Predict(new PredictionContext(window.WindowFrames, mapper, window));
                    if (result == null) throw new InvalidOperationException("Predictor returned no result");
                    record = Ledger.Publish(key, currentFrame, result.ForecastFrames, issueBranch.Id,
                        RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation, issueBranch.Sequence,
                        modelHash, digest, issueBranch.CurrentVersion,
                        result.ForecastFrames.Length == 0 ? result.Status : string.Empty);
                }
                catch (Exception exception)
                {
                    var failure = "Prediction failed: " + exception.Message;
                    record = Ledger.Publish(key, currentFrame, Array.Empty<TelemetryFrame>(), issueBranch.Id,
                        RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation, issueBranch.Sequence,
                        modelHash, digest, issueBranch.CurrentVersion, failure);
                    result = new PredictionResult(predictor.GetName(), failure, Array.Empty<Vector3>(),
                        Array.Empty<Vector3>(), currentIndex, currentIndex,
                        new PredictionMetrics(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, 0f));
                }
                // Predictor-owned arrays cannot mutate the frozen display output later.
                result = new PredictionResult(result.PredictorName, result.Status,
                    (Vector3[])result.PredictedPoints.Clone(), Array.Empty<Vector3>(), result.StartIndex,
                    result.EndIndex, result.Metrics, new List<TelemetryFrame>(record.Frames).ToArray(),
                    (float[])result.TargetElapsedSeconds.Clone());
                frozenResults.Add(key, result);
            }
            Publish(ToSnapshot(mapper.Map(currentFrame), result, issueBranch.Id, currentFrame.ElapsedSeconds));
        }

        private void BuildPredictorRegistry()
        {
            predictors.Clear();
            modelErrors.Clear();
            var modelRoot = RuntimePathResolver.ResolveModelsDirectory();
            modelHash = ForecastLineage.HashModelDirectory(Path.Combine(modelRoot, "XGBoost"));
            currentVersion = ForecastLineage.HashCurrent(RuntimeDataSourceState.SimulationProfile);
            profileBranches[frames[0].ProfileSequence] = branchId;
            branchStarts.Add(new ForecastBranch(frames[0].RowIndex, branchId, frames[0].ProfileSequence, currentVersion));
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

        private PredictionSnapshot ToSnapshot(Vector3 originPoint, PredictionResult result, string issueBranch, float now)
        {
            if (result == null)
            {
                return PredictionSnapshot.Empty;
            }

            var metrics = new PredictionMetrics(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, result.Metrics.ComputeMilliseconds);
            var status = result.Status;
            for (var index = Ledger.Forecasts.Count - 1; index >= 0; index--)
            {
                var record = Ledger.Forecasts[index];
                if (record.ModelHash != modelHash || record.BranchId != issueBranch
                    || record.Origin.ElapsedSeconds > now || record.Frames.Count == 0
                    || record.Frames[record.Frames.Count - 1].ElapsedSeconds - record.Origin.ElapsedSeconds != HorizonSeconds) continue;
                var delayed = Ledger.GetMetricsThrough(record.ForecastId, result.Metrics.ComputeMilliseconds, now);
                if (float.IsNaN(delayed.RmseMeters)) continue;
                metrics = delayed;
                status += "; delayed score origin " + record.Origin.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture);
                break;
            }
            return new PredictionSnapshot(
                originPoint,
                result.PredictedPoints,
                result.ActualPoints,
                metrics.RmseMeters,
                metrics.MaeMeters,
                metrics.CurrentErrorMeters,
                metrics.MaximumErrorMeters,
                float.NaN,
                result.StartIndex,
                result.EndIndex,
                status,
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

        private readonly struct ForecastBranch
        {
            public ForecastBranch(int row, string id, int sequence, string current)
            { StartRow = row; Id = id; Sequence = sequence; CurrentVersion = current; }
            public int StartRow { get; }
            public string Id { get; }
            public int Sequence { get; }
            public string CurrentVersion { get; }
        }
    }
}
