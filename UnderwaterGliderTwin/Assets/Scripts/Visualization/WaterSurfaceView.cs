using System.Collections.Generic;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderwaterGliderTwin.Visualization
{
    [DisallowMultipleComponent]
    public sealed class WaterSurfaceView : MonoBehaviour
    {
        private static readonly int SimulationTimeId = Shader.PropertyToID("_SimulationTime");
        private static readonly int SurfaceHeightId = Shader.PropertyToID("_SurfaceHeight");
        private static readonly int TimeMultiplierId = Shader.PropertyToID("_TimeMultiplier");
        private static readonly int LongWaveId = Shader.PropertyToID("_LongWave");
        private static readonly int CrossWaveId = Shader.PropertyToID("_CrossWave");
        private static readonly int MediumWaveId = Shader.PropertyToID("_MediumWave");
        private static readonly int DetailWaveId = Shader.PropertyToID("_DetailWave");
        private static readonly int FineWaveId = Shader.PropertyToID("_FineWave");
        private static readonly int WavePhasesId = Shader.PropertyToID("_WavePhases");
        private static readonly int WavePhasesBId = Shader.PropertyToID("_WavePhasesB");
        private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private static readonly int UnderwaterColorId = Shader.PropertyToID("_UnderwaterColor");
        private static readonly int InteractionFoamColorId = Shader.PropertyToID("_InteractionFoamColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int FresnelStrengthId = Shader.PropertyToID("_FresnelStrength");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int RefractionDistortionId = Shader.PropertyToID("_RefractionDistortion");
        private static readonly int InteractorPositionId = Shader.PropertyToID("_InteractorPosition");
        private static readonly int InteractorVelocityId = Shader.PropertyToID("_InteractorVelocity");
        private static readonly int InteractionParamsId = Shader.PropertyToID("_InteractionParams");
        private static readonly int WakeFieldId = Shader.PropertyToID("_WakeField");
        private static readonly int WakeFieldOriginExtentId = Shader.PropertyToID("_WakeFieldOriginExtent");
        private static readonly int WakeFieldEnabledId = Shader.PropertyToID("_WakeFieldEnabled");

        private PlaybackController playback;
        private Camera targetCamera;
        private Transform interactionTarget;
        private GliderTransformDriver interactionDriver;
        private WaterSurfaceSettings settings;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh ownedMesh;
        private Material ownedMaterial;
        private MaterialPropertyBlock propertyBlock;
        private float simulationTimeSeconds;
        private Vector3 previousInteractionPosition;
        private Vector3 interactionVelocity;
        private float previousInteractionTime;
        private bool hasInteractionSample;
        private FittedTrajectory wakeTrajectory;
        private IReadOnlyList<TelemetryFrame> wakeFrames;
        private GeoCoordinateMapper wakeMapper;
        private WaterWakeField wakeField;
        private readonly WaterWakePathSample[] wakeSamples = new WaterWakePathSample[WaterWakePathSampler.MaximumSampleCount];
        private Vector2 wakeFieldOrigin;
        private bool hasWakeFieldOrigin;

        public WaterSurfaceSettings Settings => settings;
        public float SimulationTimeSeconds => simulationTimeSeconds;
        public bool IsVisible => meshRenderer != null && meshRenderer.enabled;
        public Mesh SurfaceMesh => ownedMesh;
        public Transform InteractionTarget => interactionTarget;
        public float InteractionSpeedMps => new Vector2(interactionVelocity.x, interactionVelocity.z).magnitude
            / Mathf.Max(0.000001f, interactionDriver != null ? interactionDriver.HorizontalScale : 1f);
        public float InteractionDepthM => interactionTarget != null
            ? Mathf.Abs(settings.SurfaceHeightM - interactionTarget.position.y)
            : 0f;

        public void Build(WaterSurfaceSettings sourceSettings = null)
        {
            settings = (sourceSettings ?? WaterSurfaceSettings.CreateDefault()).ValidatedCopy();
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            ReleaseOwnedResources();
            ownedMesh = WaterSurfaceMeshBuilder.Build(
                settings.PatchSizeM,
                settings.GridSegments,
                settings.MaximumWaveHeightM);
            meshFilter.sharedMesh = ownedMesh;

            var shader = Resources.Load<Shader>("DeterministicOceanSurface")
                ?? Shader.Find("UnderwaterGliderTwin/DeterministicOceanSurface");
            if (shader == null)
            {
                meshRenderer.enabled = false;
                Debug.LogError("Water surface shader could not be loaded. The water surface has been disabled.", this);
                return;
            }

            ownedMaterial = new Material(shader) { name = "Deterministic Ocean Surface (Runtime)" };
            meshRenderer.sharedMaterial = ownedMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            var generatorShader = Resources.Load<Shader>("WaterWakeField");
            wakeField = new WaterWakeField(settings, generatorShader);
            transform.position = new Vector3(transform.position.x, settings.SurfaceHeightM, transform.position.z);
            ApplyMaterialProperties();
        }

        public void Bind(PlaybackController playbackController, Camera camera)
        {
            Bind(playbackController, camera, null);
        }

        public void Bind(PlaybackController playbackController, Camera camera, Transform surfaceInteractionTarget)
        {
            Bind(playbackController, camera, surfaceInteractionTarget, null, null, null);
        }

        public void Bind(
            PlaybackController playbackController,
            Camera camera,
            Transform surfaceInteractionTarget,
            FittedTrajectory fittedTrajectory,
            IReadOnlyList<TelemetryFrame> frames,
            GeoCoordinateMapper mapper)
        {
            if (playback != null)
            {
                playback.ContinuousChanged -= OnPlaybackContinuousChanged;
            }

            playback = playbackController;
            targetCamera = camera;
            SetInteractionTarget(surfaceInteractionTarget);
            SetWakeTrajectory(fittedTrajectory, frames, mapper);
            if (playback != null)
            {
                playback.ContinuousChanged += OnPlaybackContinuousChanged;
                SetSimulationTime(playback.Model.ContinuousElapsedSeconds - playback.Model.StartElapsedSeconds);
            }
            else
            {
                SetSimulationTime(0f);
            }

            RebuildWakeField();
        }

        public void SetWakeTrajectory(
            FittedTrajectory fittedTrajectory,
            IReadOnlyList<TelemetryFrame> frames,
            GeoCoordinateMapper mapper)
        {
            wakeTrajectory = fittedTrajectory;
            wakeFrames = frames;
            wakeMapper = mapper;
            RebuildWakeField();
        }

        public void SetInteractionTarget(Transform target)
        {
            interactionTarget = target;
            interactionDriver = target != null ? target.GetComponent<GliderTransformDriver>() : null;
            hasInteractionSample = false;
            interactionVelocity = Vector3.zero;
            if (interactionTarget != null)
            {
                previousInteractionPosition = interactionTarget.position;
                previousInteractionTime = simulationTimeSeconds;
            }

            ApplyMaterialProperties();
            RebuildWakeField();
        }

        public void SetSimulationTime(float elapsedSeconds)
        {
            simulationTimeSeconds = float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)
                ? 0f
                : elapsedSeconds;
            UpdateInteractionState();
            ApplyMaterialProperties();
        }

        public void SetVisible(bool visible)
        {
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                meshRenderer.enabled = visible;
            }

        }

        public float SampleHeight(Vector2 worldXZ)
        {
            var activeSettings = settings ?? WaterSurfaceSettings.CreateDefault();
            return WaterWaveMath.Evaluate(activeSettings, worldXZ, simulationTimeSeconds).HeightM;
        }

        private void LateUpdate()
        {
            if (targetCamera == null || settings == null || settings.FollowSnapM <= 0f)
            {
                return;
            }

            var snap = settings.FollowSnapM;
            var cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(
                Mathf.Round(cameraPosition.x / snap) * snap,
                settings.SurfaceHeightM,
                Mathf.Round(cameraPosition.z / snap) * snap);
            UpdateInteractionState();
            ApplyMaterialProperties();
        }

        private void OnPlaybackContinuousChanged(float continuousIndex, float progress01, FrameUpdateReason reason)
        {
            if (playback != null && playback.Model != null)
            {
                SetSimulationTime(playback.Model.ContinuousElapsedSeconds - playback.Model.StartElapsedSeconds);
                RebuildWakeField();
            }
        }

        private void ApplyMaterialProperties()
        {
            if (meshRenderer == null || ownedMaterial == null || settings == null)
            {
                return;
            }

            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(SimulationTimeId, simulationTimeSeconds);
            propertyBlock.SetFloat(SurfaceHeightId, settings.SurfaceHeightM);
            propertyBlock.SetFloat(TimeMultiplierId, settings.TimeMultiplier);
            propertyBlock.SetVector(LongWaveId, ToShaderWave(settings.LongWave));
            propertyBlock.SetVector(CrossWaveId, ToShaderWave(settings.CrossWave));
            propertyBlock.SetVector(MediumWaveId, ToShaderWave(settings.MediumWave));
            propertyBlock.SetVector(DetailWaveId, ToShaderWave(settings.DetailWave));
            propertyBlock.SetVector(FineWaveId, ToShaderWave(settings.FineWave));
            propertyBlock.SetVector(WavePhasesId, new Vector4(
                settings.LongWave.PhaseRadians,
                settings.CrossWave.PhaseRadians,
                settings.MediumWave.PhaseRadians,
                settings.DetailWave.PhaseRadians));
            propertyBlock.SetVector(WavePhasesBId, new Vector4(settings.FineWave.PhaseRadians, 0f, 0f, 0f));
            propertyBlock.SetColor(ShallowColorId, settings.ShallowColor);
            propertyBlock.SetColor(DeepColorId, settings.DeepColor);
            propertyBlock.SetColor(UnderwaterColorId, settings.UnderwaterColor);
            propertyBlock.SetColor(InteractionFoamColorId, settings.InteractionFoamColor);
            propertyBlock.SetFloat(SmoothnessId, settings.Smoothness);
            propertyBlock.SetFloat(FresnelStrengthId, settings.FresnelStrength);
            propertyBlock.SetFloat(AlphaId, settings.Alpha);
            propertyBlock.SetFloat(RefractionDistortionId, settings.RefractionDistortion);
            var targetPosition = interactionTarget != null ? interactionTarget.position : Vector3.zero;
            propertyBlock.SetVector(InteractorPositionId, new Vector4(targetPosition.x, targetPosition.y, targetPosition.z, interactionTarget != null ? 1f : 0f));
            var horizontalSpeed = new Vector2(interactionVelocity.x, interactionVelocity.z).magnitude;
            var physicalSpeed = horizontalSpeed / Mathf.Max(0.000001f, interactionDriver != null ? interactionDriver.HorizontalScale : 1f);
            var normalizedSpeed = Mathf.Clamp01(physicalSpeed / settings.InteractionReferenceSpeedMps);
            propertyBlock.SetVector(InteractorVelocityId, new Vector4(interactionVelocity.x, interactionVelocity.y, interactionVelocity.z, normalizedSpeed));
            propertyBlock.SetVector(InteractionParamsId, new Vector4(
                settings.InteractionStrengthM,
                settings.InteractionRadiusM,
                WaterSurfaceScale.DepthToWorld(
                    settings.InteractionDepthFadeM,
                    wakeMapper != null ? wakeMapper.DepthScale : 1f),
                settings.WakeLengthM));
            var wakeTexture = wakeField != null ? wakeField.Texture : null;
            propertyBlock.SetTexture(WakeFieldId, wakeTexture != null ? wakeTexture : Texture2D.blackTexture);
            propertyBlock.SetFloat(WakeFieldEnabledId, wakeTexture != null ? 1f : 0f);
            propertyBlock.SetVector(WakeFieldOriginExtentId, new Vector4(
                wakeFieldOrigin.x,
                wakeFieldOrigin.y,
                settings.WakeLengthM * 2f,
                1f / Mathf.Max(0.001f, settings.WakeLengthM * 2f)));
            meshRenderer.SetPropertyBlock(propertyBlock);
            Shader.SetGlobalFloat("_TwinSimulationTime", simulationTimeSeconds);
        }

        private void RebuildWakeField()
        {
            if (wakeField == null || interactionTarget == null || wakeTrajectory == null || wakeMapper == null
                || playback == null || playback.Model == null || playback.Model.Frames == null)
            {
                wakeField?.Clear();
                hasWakeFieldOrigin = false;
                ApplyWakeFieldBinding();
                return;
            }

            wakeFieldOrigin = new Vector2(interactionTarget.position.x, interactionTarget.position.z);
            hasWakeFieldOrigin = true;
            var sampleCount = WaterWakePathSampler.FillAnchored(
                wakeTrajectory,
                wakeFrames ?? playback.Model.Frames,
                wakeMapper,
                playback.Model.ContinuousElapsedSeconds,
                playback.Model.Direction,
                settings.WakeLengthM,
                Mathf.Max(2f, settings.WakeLengthM * 0.055f),
                wakeSamples);
            var currentElapsed = playback.Model.ContinuousElapsedSeconds;
            var historyElapsed = sampleCount > 0
                ? currentElapsed - (playback.Model.Direction < 0 ? -1f : 1f)
                    * wakeSamples[sampleCount - 1].AgeSeconds
                : currentElapsed;
            var movingAgeSeconds = wakeTrajectory.HorizontalMovingDurationBetween(
                currentElapsed, historyElapsed);
            var rebuilt = sampleCount > 0 && wakeField.Rebuild(
                wakeSamples,
                sampleCount,
                wakeFieldOrigin,
                settings.WakeLengthM * 2f,
                WaterWakeField.ResolutionForQuality(settings.Quality),
                wakeMapper.DepthScale,
                wakeMapper.HorizontalScale,
                movingAgeSeconds);
            if (!rebuilt)
            {
                wakeField.Clear();
            }

            ApplyWakeFieldBinding();
        }

        private void ApplyWakeFieldBinding()
        {
            if (meshRenderer == null || ownedMaterial == null || settings == null)
            {
                return;
            }

            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            var texture = wakeField != null ? wakeField.Texture : null;
            propertyBlock.SetTexture(WakeFieldId, texture != null ? texture : Texture2D.blackTexture);
            propertyBlock.SetFloat(WakeFieldEnabledId, texture != null && hasWakeFieldOrigin ? 1f : 0f);
            propertyBlock.SetVector(WakeFieldOriginExtentId, new Vector4(
                wakeFieldOrigin.x,
                wakeFieldOrigin.y,
                settings.WakeLengthM * 2f,
                1f / Mathf.Max(0.001f, settings.WakeLengthM * 2f)));
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateInteractionState()
        {
            if (interactionTarget == null)
            {
                interactionVelocity = Vector3.zero;
                hasInteractionSample = false;
                return;
            }

            if (interactionDriver != null && playback != null && playback.Model != null)
            {
                interactionVelocity = playback.Model.IsPlaying
                    ? interactionDriver.PlaybackVelocity
                    : Vector3.zero;
                previousInteractionPosition = interactionTarget.position;
                previousInteractionTime = simulationTimeSeconds;
                hasInteractionSample = true;
                return;
            }

            var position = interactionTarget.position;
            var deltaTime = simulationTimeSeconds - previousInteractionTime;
            if (hasInteractionSample && Mathf.Abs(deltaTime) <= 0.0001f)
            {
                return;
            }

            if (hasInteractionSample && Mathf.Abs(deltaTime) < 30f)
            {
                interactionVelocity = (position - previousInteractionPosition) / deltaTime;
                var horizontal = new Vector2(interactionVelocity.x, interactionVelocity.z);
                if (horizontal.magnitude > 8f)
                {
                    horizontal = horizontal.normalized * 8f;
                    interactionVelocity.x = horizontal.x;
                    interactionVelocity.z = horizontal.y;
                }
            }
            else
            {
                interactionVelocity = Vector3.zero;
            }

            previousInteractionPosition = position;
            previousInteractionTime = simulationTimeSeconds;
            hasInteractionSample = true;
        }

        private static Vector4 ToShaderWave(WaterWaveBand wave)
        {
            var direction = wave.Direction;
            return new Vector4(direction.x, direction.y, wave.AmplitudeM, wave.WavelengthM);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.ContinuousChanged -= OnPlaybackContinuousChanged;
            }

            ReleaseOwnedResources();
        }

        private void ReleaseOwnedResources()
        {
            if (meshFilter != null && meshFilter.sharedMesh == ownedMesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial == ownedMaterial)
            {
                meshRenderer.sharedMaterial = null;
            }

            DestroyOwned(ownedMesh);
            DestroyOwned(ownedMaterial);
            wakeField?.Dispose();
            wakeField = null;
            ownedMesh = null;
            ownedMaterial = null;
        }

        private static void DestroyOwned(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
