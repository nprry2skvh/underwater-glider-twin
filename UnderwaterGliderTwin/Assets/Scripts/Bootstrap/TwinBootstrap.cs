using System;
using UnityEngine;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;
using UnderwaterGliderTwin.Visualization;

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

            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            LoadResult = new CsvTelemetrySource(csvPath).Load();
            loadTimer.Stop();
            Logger.AppendLoad($"CSV load completed in {loadTimer.Elapsed.TotalSeconds:0.00} seconds.");
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

            var glider = GliderVisualBuilder.Build();
            var driver = glider.AddComponent<GliderTransformDriver>();
            driver.Initialize(PlaybackController, Mapper);

            var trajectory = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();
            trajectory.Initialize(LoadResult.Frames, Mapper, PlaybackController);

            var environment = new GameObject("UnderwaterEnvironment").AddComponent<UnderwaterEnvironmentBuilder>();
            environment.Build();

            var camera = Camera.main != null ? Camera.main : CreateMainCamera();
            var cameraController = camera.gameObject.AddComponent<TwinCameraController>();
            cameraController.Initialize(glider.transform, trajectory.FullTrajectoryPoints);

            var canvasRoot = new GameObject("RuntimeUI");
            canvasRoot.AddComponent<DashboardView>().Initialize(PlaybackController);
            canvasRoot.AddComponent<StatusPanelView>().Initialize(PlaybackController, AlarmEvaluator, Logger);
            canvasRoot.AddComponent<PlaybackControlsView>().Initialize(PlaybackController, cameraController, environment, trajectory);
        }

        private static Camera CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, -20f);
            cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();
            return cameraObject.AddComponent<Camera>();
        }
    }
}
