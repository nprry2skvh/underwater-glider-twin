using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
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
        public string CurrentCsvPath { get; private set; }

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
            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            var commandLineArgs = Environment.GetCommandLineArgs();
            RuntimeDataSourceState.ApplyCommandLineArguments(commandLineArgs);
            var screenshotOptions = RuntimeScreenshotOptions.Parse(commandLineArgs);
            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation)
            {
                CurrentCsvPath = RuntimeDataSourceState.LastCsvPath;
                Logger.AppendLoad("Loading telemetry from simulation profile.");
                LoadResult = new SimulationTelemetrySource(RuntimeDataSourceState.SimulationProfile).Load();
            }
            else
            {
                var csvPath = RuntimePathResolver.ResolveCsvPath();
                RuntimeDataSourceState.UseCsvPath(csvPath);
                CurrentCsvPath = csvPath;
                Logger.AppendLoad($"Loading CSV: {csvPath}");
                LoadResult = new CsvTelemetrySource(csvPath).Load();
                Logger.AppendLoad($"Loaded CSV path: {csvPath}");
            }

            loadTimer.Stop();
            Logger.AppendLoad($"Telemetry load completed in {loadTimer.Elapsed.TotalSeconds:0.00} seconds.");
            Logger.AppendLoad($"Loaded {LoadResult.Frames.Count} frames; skipped {LoadResult.SkippedRows} rows.");
            foreach (var error in LoadResult.Errors)
            {
                Logger.AppendLoad(error);
            }

            if (LoadResult.Frames.Count == 0)
            {
                throw new InvalidOperationException("CSV did not contain any usable telemetry frames.");
            }

            var originIndex = TelemetryPositionUtility.FindFirstUsableCoordinateIndex(LoadResult.Frames);
            var originFrame = originIndex >= 0 ? LoadResult.Frames[originIndex] : LoadResult.Frames[0];
            var missionDepthScale = RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                ? OceanVolumeView.CalculateVisualDepthScale(RuntimeDataSourceState.SimulationProfile.TargetDepthM, depthScale)
                : depthScale;
            Mapper = new GeoCoordinateMapper(originFrame, horizontalScale, missionDepthScale);
            var missionValidation = MissionValidationEvaluator.Evaluate(
                RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation
                    ? RuntimeDataSourceState.SimulationProfile
                    : null,
                LoadResult.Frames,
                DateTime.UtcNow);
            AlarmEvaluator = new AlarmEvaluator(
                maxDepthM: missionValidation.AlarmDepthLimitM,
                minBatteryPercent: 20f,
                maxAbsAttitudeDeg: 20f);

            PlaybackController = gameObject.AddComponent<PlaybackController>();
            PlaybackController.Initialize(new PlaybackModel(LoadResult.Frames, rowsPerSecond));

            var prediction = gameObject.AddComponent<PredictionController>();
            prediction.Initialize(LoadResult.Frames, Mapper, PlaybackController);

            var glider = GliderVisualBuilder.Build();
            var driver = glider.AddComponent<GliderTransformDriver>();
            driver.Initialize(PlaybackController, Mapper);
            var visualController = glider.AddComponent<GliderVisualController>();
            visualController.Initialize(PlaybackController);

            var trajectory = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();
            trajectory.Initialize(LoadResult.Frames, Mapper, PlaybackController, prediction);

            var environment = new GameObject("UnderwaterEnvironment").AddComponent<UnderwaterEnvironmentBuilder>();
            environment.Build();
            var missionHorizontalExtents = new Vector2(64f, 64f);
            var missionHorizontalCenter = Vector3.zero;
            OceanVolumeView oceanVolume = null;

            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation)
            {
                environment.SetParticlesEnabled(false);
                missionHorizontalExtents = MissionMapOverlay.ComputeHorizontalExtents(trajectory.FullTrajectoryPoints);
                missionHorizontalCenter = MissionMapOverlay.ComputeHorizontalCenter(trajectory.FullTrajectoryPoints);
                oceanVolume = new GameObject("OceanVolume").AddComponent<OceanVolumeView>();
                oceanVolume.Initialize(
                    RuntimeDataSourceState.SimulationProfile,
                    Mapper,
                    PlaybackController,
                    glider.transform,
                    trajectory.FullTrajectoryPoints,
                    RuntimeDataSourceState.SimulationProfile.TargetDepthM,
                    missionHorizontalExtents,
                    missionDepthScale,
                    missionHorizontalCenter);
                new GameObject("MissionMapOverlay").AddComponent<MissionMapOverlay>().Initialize(LoadResult.Frames, Mapper);
            }

            var camera = Camera.main != null ? Camera.main : CreateMainCamera();
            ConfigureCamera(camera);
            var cameraController = camera.gameObject.AddComponent<TwinCameraController>();
            cameraController.Initialize(glider.transform, trajectory.FullTrajectoryPoints);

            var screenshotCapture = gameObject.AddComponent<RuntimeScreenshotCapture>();
            screenshotCapture.Initialize(screenshotOptions);

            var canvasRoot = new GameObject("RuntimeUI");
            canvasRoot.AddComponent<DataInputView>().Initialize(
                CurrentCsvPath,
                RuntimeDataSourceState.SimulationProfile,
                prediction,
                ReloadFromCsvPath,
                ReloadFromSimulationProfile,
                oceanVolume != null ? oceanVolume.RebuildCurrentSourceAndCandidateCache : null);
            canvasRoot.AddComponent<DashboardView>().Initialize(PlaybackController, prediction);
            canvasRoot.AddComponent<StatusPanelView>().Initialize(PlaybackController, AlarmEvaluator, Logger, prediction);
            canvasRoot.AddComponent<OceanCommandToolbarView>().Initialize(cameraController, trajectory);
            canvasRoot.AddComponent<PlaybackControlsView>().Initialize(PlaybackController, cameraController, environment, trajectory,
                onScreenshotRequested: screenshotCapture.CaptureManual,
                onMissionViewRequested: () =>
                {
                    if (RuntimeDataSourceState.CurrentMode != RuntimeDataSourceMode.Simulation)
                    {
                        cameraController.SetMode(CameraMode.Global);
                        trajectory.SetCameraMode(CameraMode.Global);
                        return;
                    }

                    cameraController.SetMissionVolumeView(RuntimeDataSourceState.SimulationProfile.TargetDepthM, missionHorizontalExtents, missionDepthScale);
                    trajectory.SetCameraMode(CameraMode.Global);
                });

            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation)
            {
                cameraController.SetMissionVolumeView(RuntimeDataSourceState.SimulationProfile.TargetDepthM, missionHorizontalExtents, missionDepthScale);
                trajectory.SetCameraMode(CameraMode.Global);
            }
        }

        private void ReloadFromCsvPath(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                Logger.AppendLoad($"Reload ignored. Missing CSV: {csvPath}");
                return;
            }

            RuntimeDataSourceState.UseCsvPath(csvPath);
            RuntimePathResolver.SetCsvPathOverride(csvPath);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ReloadFromSimulationProfile(SimulationProfile profile)
        {
            RuntimeDataSourceState.UseSimulation(profile);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static Camera CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, -20f);
            cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();
            var camera = cameraObject.AddComponent<Camera>();
            ConfigureCamera(camera);
            return camera;
        }

        private static void ConfigureCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.18f, 0.24f);
            camera.farClipPlane = 2000f;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 58f;
        }
    }
}
