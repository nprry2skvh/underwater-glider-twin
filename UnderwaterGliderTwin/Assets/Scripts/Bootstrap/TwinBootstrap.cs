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
        public SimulationRuntimeSession SimulationSession { get; private set; }

        private TrajectoryView trajectoryView;
        private OceanVolumeView oceanVolume;

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
            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Csv)
            {
                LaunchCoordinator.SaveSuccessfulCsvPath(CurrentCsvPath);
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

            trajectoryView = new GameObject("TrajectoryView").AddComponent<TrajectoryView>();
            trajectoryView.Initialize(LoadResult.Frames, Mapper, PlaybackController, prediction);

            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation)
            {
                SimulationSession = new SimulationRuntimeSession(
                    PlaybackController.Model,
                    RuntimeDataSourceState.SimulationProfile,
                    new SimulationFutureTrajectoryGenerator(this));
                PlaybackController.Model.FramesReplaced += trajectoryView.ReplaceFutureTrajectory;
                SimulationSession.StatusChanged += OnSimulationSessionStatusChanged;
                SimulationRuntimeRegistry.SetActive(SimulationSession);
            }
            else
            {
                SimulationRuntimeRegistry.SetActive(null);
            }

            var environment = new GameObject("UnderwaterEnvironment").AddComponent<UnderwaterEnvironmentBuilder>();
            environment.Build();
            var missionHorizontalExtents = new Vector2(64f, 64f);
            var missionHorizontalCenter = Vector3.zero;
            oceanVolume = null;

            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation)
            {
                environment.SetParticlesEnabled(false);
                missionHorizontalExtents = MissionMapOverlay.ComputeHorizontalExtents(trajectoryView.FullTrajectoryPoints);
                missionHorizontalCenter = MissionMapOverlay.ComputeHorizontalCenter(trajectoryView.FullTrajectoryPoints);
                oceanVolume = new GameObject("OceanVolume").AddComponent<OceanVolumeView>();
                oceanVolume.Initialize(
                    RuntimeDataSourceState.SimulationProfile,
                    Mapper,
                    PlaybackController,
                    glider.transform,
                    trajectoryView.FullTrajectoryPoints,
                    RuntimeDataSourceState.SimulationProfile.TargetDepthM,
                    missionHorizontalExtents,
                    missionDepthScale,
                    missionHorizontalCenter);
                new GameObject("MissionMapOverlay").AddComponent<MissionMapOverlay>().Initialize(LoadResult.Frames, Mapper);
            }

            var camera = Camera.main != null ? Camera.main : CreateMainCamera();
            ConfigureCamera(camera);
            var cameraController = camera.gameObject.AddComponent<TwinCameraController>();
            cameraController.Initialize(glider.transform, trajectoryView.FullTrajectoryPoints);

            var screenshotCapture = gameObject.AddComponent<RuntimeScreenshotCapture>();
            screenshotCapture.Initialize(screenshotOptions);

            var canvasRoot = new GameObject("RuntimeUI");
            canvasRoot.AddComponent<DataInputView>().Initialize(
                CurrentCsvPath,
                RuntimeDataSourceState.SimulationProfile,
                prediction,
                ReloadFromCsvPath,
                ReloadFromSimulationProfile,
                oceanVolume != null ? oceanVolume.UpdateCurrentProfile : null);
            canvasRoot.AddComponent<DashboardView>().Initialize(PlaybackController, prediction);
            canvasRoot.AddComponent<StatusPanelView>().Initialize(PlaybackController, AlarmEvaluator, Logger, prediction);
            canvasRoot.AddComponent<OceanCommandToolbarView>().Initialize(cameraController, trajectoryView);
            canvasRoot.AddComponent<PlaybackControlsView>().Initialize(PlaybackController, cameraController, environment, trajectoryView,
                onScreenshotRequested: screenshotCapture.CaptureManual,
                onMissionViewRequested: () =>
                {
                    if (RuntimeDataSourceState.CurrentMode != RuntimeDataSourceMode.Simulation)
                    {
                        cameraController.SetMode(CameraMode.Global);
                        trajectoryView.SetCameraMode(CameraMode.Global);
                        return;
                    }

                    cameraController.SetMissionVolumeView(RuntimeDataSourceState.SimulationProfile.TargetDepthM, missionHorizontalExtents, missionDepthScale);
                    trajectoryView.SetCameraMode(CameraMode.Global);
                });

            if (RuntimeDataSourceState.CurrentMode == RuntimeDataSourceMode.Simulation)
            {
                cameraController.SetMissionVolumeView(RuntimeDataSourceState.SimulationProfile.TargetDepthM, missionHorizontalExtents, missionDepthScale);
                trajectoryView.SetCameraMode(CameraMode.Global);
            }
        }

        private void Update()
        {
            SimulationSession?.Tick();
        }

        private void OnDestroy()
        {
            if (SimulationSession != null)
            {
                SimulationSession.StatusChanged -= OnSimulationSessionStatusChanged;
                SimulationSession.CancelPendingRebuild();
                if (ReferenceEquals(SimulationRuntimeRegistry.Active, SimulationSession))
                {
                    SimulationRuntimeRegistry.SetActive(null);
                }
            }

            if (PlaybackController != null && trajectoryView != null)
            {
                PlaybackController.Model.FramesReplaced -= trajectoryView.ReplaceFutureTrajectory;
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
            if (RuntimeDataSourceState.CurrentMode != RuntimeDataSourceMode.Simulation || SimulationSession == null)
            {
                RuntimeDataSourceState.UseSimulation(profile);
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (!SimulationSession.RequestProfileUpdate(profile))
            {
                Logger.AppendLoad($"Simulation profile update rejected: {SimulationSession.LastError}");
            }
        }

        private void OnSimulationSessionStatusChanged()
        {
            if (SimulationSession == null || SimulationSession.IsRebuildPending)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(SimulationSession.LastError))
            {
                Logger.AppendLoad($"Simulation profile update failed: {SimulationSession.LastError}");
                return;
            }

            RuntimeDataSourceState.UseSimulation(SimulationSession.ActiveProfile);
            oceanVolume?.UpdateCurrentProfile(SimulationSession.ActiveProfileReference);
            Logger.AppendLoad("Simulation profile update applied without reloading the scene.");
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
