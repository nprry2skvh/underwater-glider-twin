using System;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class OceanCurrentInstancedRenderer : MonoBehaviour
    {
        private const int MaximumTrajectorySafetyPoints = 96;
        public const string CurrentArrowShaderResourcePath = "InstancedOceanCurrent";
        public static readonly Color CurrentArrowEmissionColor = new Color(0.16f, 0.92f, 1f, 1f) * 1.8f;
        private readonly Matrix4x4[] matrices = new Matrix4x4[OceanVolumeSamplingCache.MaximumVisibleArrowCount];
        private readonly Vector4[] colors = new Vector4[OceanVolumeSamplingCache.MaximumVisibleArrowCount];

        private OceanVolumeSamplingCache samplingCache;
        private OceanCurrentResolver resolver;
        private SimulationProfile profile;
        private GeoCoordinateMapper mapper;
        private PlaybackController playback;
        private Transform glider;
        private Vector3[] trajectoryEnuPoints;
        private Mesh arrowMesh;
        private Material arrowMaterial;
        private Mesh combinedMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Vector3[] arrowVertices;
        private int[] arrowTriangles;
        private Vector3[] combinedVertices;
        private Color[] combinedColors;
        private int[] combinedTriangles;
        private Camera currentCamera;
        private Vector3 lastCameraEnuPosition;
        private int instanceCount;
        private bool hasCameraPosition;
        private Vector3 volumeCenterWorld;
        private Vector2 volumeExtentsWorld;
        private float missionDepthM;

        public int VisibleArrowCount => instanceCount;
        public SimulationProfile CurrentProfile => profile;

        public void Initialize(
            SimulationProfile profile,
            GeoCoordinateMapper coordinateMapper,
            PlaybackController playbackController,
            Transform gliderTransform,
            Vector3[] trajectoryWorldPoints,
            Vector3 volumeCenterWorld,
            Vector2 volumeExtentsWorld,
            float missionDepthM)
        {
            Unsubscribe();
            mapper = coordinateMapper;
            playback = playbackController;
            glider = gliderTransform;
            if (profile == null || mapper == null)
            {
                enabled = false;
                return;
            }

            this.profile = profile;
            this.volumeCenterWorld = volumeCenterWorld;
            this.volumeExtentsWorld = volumeExtentsWorld;
            this.missionDepthM = missionDepthM;
            trajectoryEnuPoints = MapTrajectoryPoints(trajectoryWorldPoints, mapper);
            CreateRenderResources();
            RebuildSourceAndCandidateCache();

            if (playback != null)
            {
                playback.FrameChangedWithReason += OnFrameChanged;
                OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
            }
            else
            {
                RefreshForTime(0f);
            }
        }

        public void RebuildSourceAndCandidateCache()
        {
            if (profile == null || mapper == null)
            {
                return;
            }

            samplingCache = new OceanVolumeSamplingCache(profile);
            resolver = new OceanCurrentResolver(profile);
            var centerEnu = mapper.UnmapPosition(volumeCenterWorld);
            var extentsEnu = new Vector2(
                volumeExtentsWorld.x / Mathf.Max(0.000001f, GetHorizontalScale(mapper, Vector3.right)),
                volumeExtentsWorld.y / Mathf.Max(0.000001f, GetHorizontalScale(mapper, Vector3.forward)));
            samplingCache.RebuildCandidateCache(new Vector3(centerEnu.x, 0f, centerEnu.z), extentsEnu, missionDepthM);
            var elapsedSeconds = playback != null && playback.Model != null
                ? playback.Model.CurrentFrame.ElapsedSeconds
                : 0f;
            RefreshForTime(elapsedSeconds);
        }

        public void UpdateProfile(SimulationProfile updatedProfile)
        {
            if (updatedProfile == null)
            {
                return;
            }

            profile = updatedProfile;
            RebuildSourceAndCandidateCache();
        }

        private void LateUpdate()
        {
            if (samplingCache == null || mapper == null)
            {
                return;
            }

            currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }

            var cameraEnu = mapper.UnmapPosition(currentCamera.transform.position);
            if (!hasCameraPosition || (cameraEnu - lastCameraEnuPosition).sqrMagnitude > 0.01f)
            {
                samplingCache.RefreshForCamera(cameraEnu);
                RebuildInstanceData();
                lastCameraEnuPosition = cameraEnu;
                hasCameraPosition = true;
            }

        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Application.isPlaying)
            {
                Destroy(arrowMesh);
                Destroy(arrowMaterial);
                Destroy(combinedMesh);
            }
            else
            {
                DestroyImmediate(arrowMesh);
                DestroyImmediate(arrowMaterial);
                DestroyImmediate(combinedMesh);
            }
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            RefreshForTime(frame.ElapsedSeconds);
        }

        private void RefreshForTime(float elapsedSeconds)
        {
            if (samplingCache == null || resolver == null)
            {
                return;
            }

            var gliderEnu = glider != null ? mapper.UnmapPosition(glider.position) : Vector3.positiveInfinity;
            samplingCache.RefreshForTime(resolver, elapsedSeconds, gliderEnu, trajectoryEnuPoints);
            hasCameraPosition = false;
        }

        private void RebuildInstanceData()
        {
            instanceCount = samplingCache.VisibleCandidates.Count;
            var maximumDistance = 1f;
            if (instanceCount > 0)
            {
                maximumDistance = Mathf.Max(1f, samplingCache.VisibleCandidates[instanceCount - 1].CameraDistanceSquared);
            }

            for (var index = 0; index < instanceCount; index++)
            {
                var visible = samplingCache.VisibleCandidates[index];
                var velocity = visible.Vector.VelocityMps;
                var visualVelocity = mapper.MapEnuPosition(velocity);
                var direction = visualVelocity.sqrMagnitude > 0.000001f ? visualVelocity.normalized : Vector3.forward;
                var normalizedSpeed = Mathf.Clamp01(velocity.magnitude / 1.2f);
                var distanceRatio = Mathf.Clamp01(visible.CameraDistanceSquared / maximumDistance);
                var scale = Mathf.Lerp(0.65f, 0.28f, distanceRatio);
                var length = Mathf.Lerp(2.2f, 4.8f, normalizedSpeed) * scale;
                var width = Mathf.Lerp(0.18f, 0.28f, normalizedSpeed) * scale;
                matrices[index] = Matrix4x4.TRS(
                    mapper.MapEnuPosition(visible.Candidate.EnuPositionM),
                    Quaternion.LookRotation(direction, Vector3.up),
                    new Vector3(width, width, length));

                var color = Color.Lerp(new Color(0f, 0.38f, 1f, 0.32f), new Color(0.35f, 1f, 0.76f, 0.88f), normalizedSpeed);
                color.a *= Mathf.Lerp(1f, 0.3f, distanceRatio);
                colors[index] = color;
            }

            RebuildMeshSurface();
        }

        private void CreateRenderResources()
        {
            arrowMesh = BuildArrowMesh();
            var shader = Resources.Load<Shader>(CurrentArrowShaderResourcePath);
            if (shader == null)
            {
                throw new InvalidOperationException($"Missing current arrow shader resource: {CurrentArrowShaderResourcePath}");
            }

            arrowMaterial = new Material(shader)
            {
                name = "VolumetricCurrentArrowMaterial",
                color = CurrentArrowEmissionColor
            };
            arrowMaterial.enableInstancing = false;
            arrowMaterial.renderQueue = 3000;

            arrowVertices = arrowMesh.vertices;
            arrowTriangles = arrowMesh.triangles;
            combinedVertices = new Vector3[OceanVolumeSamplingCache.MaximumVisibleArrowCount * arrowVertices.Length];
            combinedColors = new Color[combinedVertices.Length];
            combinedTriangles = new int[OceanVolumeSamplingCache.MaximumVisibleArrowCount * arrowTriangles.Length];
            for (var index = 0; index < OceanVolumeSamplingCache.MaximumVisibleArrowCount; index++)
            {
                var vertexOffset = index * arrowVertices.Length;
                var triangleOffset = index * arrowTriangles.Length;
                for (var triangle = 0; triangle < arrowTriangles.Length; triangle++)
                {
                    combinedTriangles[triangleOffset + triangle] = vertexOffset + arrowTriangles[triangle];
                }
            }

            combinedMesh = new Mesh { name = "VolumetricOceanCurrentMesh" };
            combinedMesh.MarkDynamic();
            meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = combinedMesh;
            meshRenderer.sharedMaterial = arrowMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void RebuildMeshSurface()
        {
            if (combinedMesh == null || arrowVertices == null || arrowTriangles == null)
            {
                return;
            }

            var vertexCount = instanceCount * arrowVertices.Length;
            for (var index = 0; index < instanceCount; index++)
            {
                var vertexOffset = index * arrowVertices.Length;
                var color = (Color)colors[index];
                for (var vertex = 0; vertex < arrowVertices.Length; vertex++)
                {
                    combinedVertices[vertexOffset + vertex] = ToMeshLocalVertex(transform, matrices[index], arrowVertices[vertex]);
                    combinedColors[vertexOffset + vertex] = color;
                }
            }

            combinedMesh.Clear(false);
            if (vertexCount == 0)
            {
                return;
            }

            combinedMesh.SetVertices(combinedVertices, 0, vertexCount);
            combinedMesh.SetColors(combinedColors, 0, vertexCount);
            combinedMesh.SetIndices(combinedTriangles, 0, instanceCount * arrowTriangles.Length, MeshTopology.Triangles, 0, true);
        }

        private static Vector3 ToMeshLocalVertex(Transform volumeTransform, Matrix4x4 worldMatrix, Vector3 sourceVertex)
        {
            return volumeTransform.InverseTransformPoint(worldMatrix.MultiplyPoint3x4(sourceVertex));
        }

        private void Unsubscribe()
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
            }
        }

        private static Vector3[] MapTrajectoryPoints(Vector3[] points, GeoCoordinateMapper coordinateMapper)
        {
            if (points == null || points.Length == 0)
            {
                return Array.Empty<Vector3>();
            }

            var count = Mathf.Min(points.Length, MaximumTrajectorySafetyPoints);
            var result = new Vector3[count];
            for (var index = 0; index < count; index++)
            {
                var sourceIndex = count == 1
                    ? 0
                    : Mathf.RoundToInt(index * (points.Length - 1f) / (count - 1f));
                result[index] = coordinateMapper.UnmapPosition(points[sourceIndex]);
            }

            return result;
        }

        private static float GetHorizontalScale(GeoCoordinateMapper coordinateMapper, Vector3 axis)
        {
            return coordinateMapper.MapEnuPosition(axis).magnitude;
        }

        private static Mesh BuildArrowMesh()
        {
            var vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(-0.5f, -0.5f, 0.62f), new Vector3(0.5f, -0.5f, 0.62f),
                new Vector3(0.5f, 0.5f, 0.62f), new Vector3(-0.5f, 0.5f, 0.62f),
                new Vector3(-1f, -1f, 0.62f), new Vector3(1f, -1f, 0.62f),
                new Vector3(1f, 1f, 0.62f), new Vector3(-1f, 1f, 0.62f),
                new Vector3(0f, 0f, 1f)
            };
            var triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7, 8, 9, 12, 9, 10, 12,
                10, 11, 12, 11, 8, 12, 4, 5, 9, 4, 9, 8, 5, 6, 10, 5, 10, 9,
                6, 7, 11, 6, 11, 10, 7, 4, 8, 7, 8, 11
            };
            var mesh = new Mesh { name = "OceanCurrentInstancedArrowMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
