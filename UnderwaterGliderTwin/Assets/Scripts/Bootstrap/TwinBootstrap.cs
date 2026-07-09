using System;
using UnityEngine;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class TwinBootstrap : MonoBehaviour
    {
        [SerializeField] private float rowsPerSecond = 120f;
        [SerializeField] private float horizontalScale = 0.0025f;
        [SerializeField] private float depthScale = 0.05f;

        public PlaybackController PlaybackController { get; private set; }
        public GeoCoordinateMapper Mapper { get; private set; }
        public TelemetryLoadResult LoadResult { get; private set; }
        public TwinLogger Logger { get; private set; }
        public AlarmEvaluator AlarmEvaluator { get; private set; }

        private void Awake()
        {
            Logger = new TwinLogger(RuntimePathResolver.ResolveLogDirectory());

            try
            {
                InitializeRuntime();
            }
            catch (Exception ex)
            {
                Logger.AppendLoad($"Startup failed: {ex}");
                Debug.LogException(ex, this);
            }
        }

        private void InitializeRuntime()
        {
            var csvPath = RuntimePathResolver.ResolveCsvPath();
            Logger.AppendLoad($"Loading CSV: {csvPath}");

            LoadResult = new CsvTelemetrySource(csvPath).Load();
            Logger.AppendLoad($"Loaded {LoadResult.Frames.Count} frames; skipped {LoadResult.SkippedRows} rows.");
            foreach (var error in LoadResult.Errors)
            {
                Logger.AppendLoad(error);
            }

            if (LoadResult.Frames.Count == 0)
            {
                throw new InvalidOperationException("CSV did not contain any usable telemetry frames.");
            }

            Mapper = new GeoCoordinateMapper(LoadResult.Frames[0], horizontalScale, depthScale);
            AlarmEvaluator = new AlarmEvaluator(maxDepthM: 1000f, minBatteryPercent: 20f, maxAbsAttitudeDeg: 20f);

            PlaybackController = gameObject.AddComponent<PlaybackController>();
            PlaybackController.Initialize(new PlaybackModel(LoadResult.Frames, rowsPerSecond));
        }
    }
}
