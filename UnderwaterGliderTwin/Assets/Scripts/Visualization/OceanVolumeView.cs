using System.Collections.Generic;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class OceanVolumeView : MonoBehaviour
    {
        private OceanCurrentInstancedRenderer currentRenderer;

        public int VisibleCurrentArrowCount => currentRenderer != null ? currentRenderer.VisibleArrowCount : 0;

        public readonly struct VolumeEdge
        {
            public VolumeEdge(Vector3 start, Vector3 end)
            {
                Start = start;
                End = end;
            }

            public Vector3 Start { get; }
            public Vector3 End { get; }
        }

        public readonly struct LayerSample
        {
            public LayerSample(float depthM, Vector2 velocityMps)
            {
                DepthM = depthM;
                VelocityMps = velocityMps;
            }

            public float DepthM { get; }
            public Vector2 VelocityMps { get; }
        }

        public readonly struct CurrentArrowGeometry
        {
            public CurrentArrowGeometry(Vector3 direction, float shaftLength, float headLength, float shaftRadius, float headRadius)
            {
                Direction = direction;
                ShaftLength = shaftLength;
                HeadLength = headLength;
                ShaftRadius = shaftRadius;
                HeadRadius = headRadius;
            }

            public Vector3 Direction { get; }
            public float ShaftLength { get; }
            public float HeadLength { get; }
            public float ShaftRadius { get; }
            public float HeadRadius { get; }
            public float TotalLength => ShaftLength + HeadLength;
        }

        public static CurrentArrowGeometry BuildCurrentArrowGeometry(Vector2 velocityMps)
        {
            var velocity = new Vector3(velocityMps.x, 0f, velocityMps.y);
            var magnitude = Mathf.Max(0.05f, velocity.magnitude);
            var direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector3.forward;
            // Keep low-speed vectors legible after the deep-water volume is compressed for display.
            var totalLength = Mathf.Clamp(2.2f + magnitude * 4f, 3.0f, 8f);
            var headLength = Mathf.Clamp(totalLength * 0.32f, 0.8f, 1.6f);
            var shaftLength = Mathf.Max(0.5f, totalLength - headLength);
            var shaftRadius = Mathf.Clamp(0.055f + magnitude * 0.02f, 0.055f, 0.12f);
            var headRadius = Mathf.Clamp(0.16f + magnitude * 0.10f, 0.16f, 0.30f);
            return new CurrentArrowGeometry(direction, shaftLength, headLength, shaftRadius, headRadius);
        }

        public static IReadOnlyList<LayerSample> BuildLayerSamples(OceanCurrentProfile profile, float missionDepthM)
        {
            var samples = new List<LayerSample>();
            if (profile == null) return samples;
            var maximumDepth = Mathf.Max(0f, missionDepthM);
            foreach (var layer in profile.Layers)
            {
                if (layer == null || layer.MinDepthM > maximumDepth) continue;
                var depth = Mathf.Min(maximumDepth, Mathf.Max(0f, (layer.MinDepthM + layer.MaxDepthM) * 0.5f));
                samples.Add(new LayerSample(depth, new Vector2(layer.EastwardMps, layer.NorthwardMps)));
            }
            return samples;
        }

        public static IReadOnlyList<LayerSample> SelectVisualLayerSamples(IReadOnlyList<LayerSample> samples, int maximumSamples)
        {
            var selected = new List<LayerSample>();
            if (samples == null || samples.Count == 0)
            {
                return selected;
            }

            var count = Mathf.Max(1, maximumSamples);
            if (samples.Count <= count)
            {
                selected.AddRange(samples);
                return selected;
            }

            if (count == 1)
            {
                selected.Add(samples[samples.Count / 2]);
                return selected;
            }

            for (var index = 0; index < count; index++)
            {
                var sourceIndex = Mathf.RoundToInt(index * (samples.Count - 1f) / (count - 1f));
                selected.Add(samples[sourceIndex]);
            }

            return selected;
        }

        public static float CalculateVisualDepthScale(float missionDepthM, float preferredScale, float maximumVisualDepth = 32f)
        {
            var safePreferredScale = Mathf.Max(0.001f, preferredScale);
            var safeDepth = Mathf.Max(1f, missionDepthM);
            var compressedScale = Mathf.Max(0.001f, maximumVisualDepth) / safeDepth;
            return Mathf.Min(safePreferredScale, compressedScale);
        }

        public static IReadOnlyList<Vector3> BuildCurrentGridPoints(float width, int columns)
        {
            return BuildCurrentGridPoints(new Vector2(width, width), columns);
        }

        public static IReadOnlyList<Vector3> BuildCurrentGridPoints(Vector2 size, int columns)
        {
            var points = new List<Vector3>();
            var count = Mathf.Max(1, columns);
            var spacingX = size.x / (count + 1f);
            var spacingZ = size.y / (count + 1f);
            var startX = -size.x * 0.5f + spacingX;
            var startZ = -size.y * 0.5f + spacingZ;
            for (var row = 0; row < count; row++)
            {
                for (var column = 0; column < count; column++)
                {
                    points.Add(new Vector3(startX + column * spacingX, 0f, startZ + row * spacingZ));
                }
            }
            return points;
        }

        public static IReadOnlyList<VolumeEdge> BuildVolumeEdges(float width, float depth)
        {
            return BuildVolumeEdges(new Vector2(width, width), depth);
        }

        public static IReadOnlyList<VolumeEdge> BuildVolumeEdges(Vector2 size, float depth)
        {
            var halfX = Mathf.Max(1f, size.x) * 0.5f;
            var halfZ = Mathf.Max(1f, size.y) * 0.5f;
            var bottom = -Mathf.Max(1f, depth);
            var topFrontLeft = new Vector3(-halfX, 0f, -halfZ);
            var topFrontRight = new Vector3(halfX, 0f, -halfZ);
            var topBackLeft = new Vector3(-halfX, 0f, halfZ);
            var topBackRight = new Vector3(halfX, 0f, halfZ);
            var bottomFrontLeft = new Vector3(-halfX, bottom, -halfZ);
            var bottomFrontRight = new Vector3(halfX, bottom, -halfZ);
            var bottomBackLeft = new Vector3(-halfX, bottom, halfZ);
            var bottomBackRight = new Vector3(halfX, bottom, halfZ);
            return new[]
            {
                new VolumeEdge(topFrontLeft, topFrontRight), new VolumeEdge(topFrontRight, topBackRight),
                new VolumeEdge(topBackRight, topBackLeft), new VolumeEdge(topBackLeft, topFrontLeft),
                new VolumeEdge(bottomFrontLeft, bottomFrontRight), new VolumeEdge(bottomFrontRight, bottomBackRight),
                new VolumeEdge(bottomBackRight, bottomBackLeft), new VolumeEdge(bottomBackLeft, bottomFrontLeft),
                new VolumeEdge(topFrontLeft, bottomFrontLeft), new VolumeEdge(topFrontRight, bottomFrontRight),
                new VolumeEdge(topBackLeft, bottomBackLeft), new VolumeEdge(topBackRight, bottomBackRight)
            };
        }

        public void Initialize(OceanCurrentProfile profile, float missionDepthM, float horizontalExtent = 64f, float verticalScale = 0.05f, Vector3 horizontalCenter = default)
        {
            Initialize(profile, missionDepthM, new Vector2(horizontalExtent, horizontalExtent), verticalScale, horizontalCenter);
        }

        public void Initialize(OceanCurrentProfile profile, float missionDepthM, Vector2 horizontalExtents, float verticalScale = 0.05f, Vector3 horizontalCenter = default)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            transform.position = new Vector3(horizontalCenter.x, 0f, horizontalCenter.z);
            var depth = Mathf.Max(20f, missionDepthM);
            var worldDepth = Mathf.Max(2f, depth * Mathf.Max(0.001f, verticalScale));
            var size = new Vector2(Mathf.Max(20f, horizontalExtents.x), Mathf.Max(20f, horizontalExtents.y));
            CreateWaterVolume(size, worldDepth);
            CreateSurfaceGrid(size, 8);
            CreateCube("海底", new Vector3(0f, -worldDepth, 0f), new Vector3(size.x, 1f, size.y), new Color(0.03f, 0.14f, 0.13f, 0.75f));
            var visualSamples = SelectVisualLayerSamples(BuildLayerSamples(profile, depth), 12);
            foreach (var sample in visualSamples)
            {
                foreach (var position in BuildCurrentGridPoints(size * 0.82f, 5)) CreateCurrentArrow(sample, position, verticalScale);
            }
        }

        public void Initialize(
            SimulationProfile profile,
            GeoCoordinateMapper mapper,
            PlaybackController playback,
            Transform glider,
            Vector3[] trajectoryWorldPoints,
            float missionDepthM,
            Vector2 horizontalExtents,
            float verticalScale = 0.05f,
            Vector3 horizontalCenter = default)
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            transform.position = new Vector3(horizontalCenter.x, 0f, horizontalCenter.z);
            var depth = Mathf.Max(20f, missionDepthM);
            var worldDepth = Mathf.Max(2f, depth * Mathf.Max(0.001f, verticalScale));
            var size = new Vector2(Mathf.Max(20f, horizontalExtents.x), Mathf.Max(20f, horizontalExtents.y));
            CreateWaterVolume(size, worldDepth);
            CreateSurfaceGrid(size, 8);
            CreateCube("娴峰簳", new Vector3(0f, -worldDepth, 0f), new Vector3(size.x, 1f, size.y), new Color(0.03f, 0.14f, 0.13f, 0.75f));

            currentRenderer = GetComponent<OceanCurrentInstancedRenderer>();
            if (currentRenderer == null)
            {
                currentRenderer = gameObject.AddComponent<OceanCurrentInstancedRenderer>();
            }

            currentRenderer.Initialize(profile, mapper, playback, glider, trajectoryWorldPoints, horizontalCenter, size, depth);
        }

        public void RebuildCurrentSourceAndCandidateCache()
        {
            if (currentRenderer == null)
            {
                currentRenderer = GetComponent<OceanCurrentInstancedRenderer>();
            }

            if (currentRenderer != null)
            {
                currentRenderer.RebuildSourceAndCandidateCache();
            }
        }

        private void CreateCurrentArrow(LayerSample sample, Vector3 position, float verticalScale)
        {
            var geometry = BuildCurrentArrowGeometry(sample.VelocityMps);
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "海流箭头";
            arrow.transform.SetParent(transform, false);
            arrow.transform.localScale = new Vector3(geometry.ShaftRadius * 2f, geometry.ShaftLength * 0.5f, geometry.ShaftRadius * 2f);
            arrow.transform.localPosition = new Vector3(position.x, -sample.DepthM * Mathf.Max(0.001f, verticalScale), position.z);
            arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, geometry.Direction);
            var speed = Mathf.Clamp01(sample.VelocityMps.magnitude / 1.2f);
            var shaftColor = Color.Lerp(new Color(0f, 0.8f, 1f), new Color(1f, 0.6f, 0f), speed);
            var headColor = Color.Lerp(new Color(1f, 0.95f, 0.08f), new Color(1f, 0.2f, 0.02f), speed);
            var arrowMaterial = RuntimeMaterialFactory.Opaque("OceanCurrentArrowMaterial", shaftColor);
            var headMaterial = RuntimeMaterialFactory.Opaque("OceanCurrentArrowHeadMaterial", headColor);
            ConfigureEmissive(arrowMaterial, shaftColor);
            ConfigureEmissive(headMaterial, headColor);
            var headPosition = arrow.transform.localPosition + geometry.Direction * (geometry.ShaftLength * 0.5f);
            CreateArrowHead(transform, headPosition, arrow.transform.localRotation, geometry, headMaterial);
            RemoveCollider(arrow);
            arrow.GetComponent<Renderer>().sharedMaterial = RuntimeMaterialFactory.Opaque("海流箭头材质", Color.Lerp(new Color(0f, 0.8f, 1f), new Color(1f, 0.6f, 0f), speed));
        }

        private static void ConfigureEmissive(Material material, Color color)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.8f);
        }

        private static void CreateArrowHead(Transform parent, Vector3 localPosition, Quaternion localRotation, CurrentArrowGeometry geometry, Material material)
        {
            var head = new GameObject("OceanCurrentArrowHead");
            head.transform.SetParent(parent, false);
            head.transform.localPosition = localPosition;
            head.transform.localRotation = localRotation;
            var filter = head.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildConeMesh(geometry.HeadRadius, geometry.HeadLength, 8);
            head.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh BuildConeMesh(float radius, float height, int segments)
        {
            var safeSegments = Mathf.Max(3, segments);
            var vertices = new Vector3[safeSegments + 1];
            var triangles = new int[safeSegments * 3];
            for (var index = 0; index < safeSegments; index++)
            {
                var angle = index / (float)safeSegments * Mathf.PI * 2f;
                vertices[index] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var triangleIndex = index * 3;
                triangles[triangleIndex] = index;
                triangles[triangleIndex + 1] = safeSegments;
                triangles[triangleIndex + 2] = (index + 1) % safeSegments;
            }

            vertices[safeSegments] = Vector3.up * height;
            var mesh = new Mesh { name = "OceanCurrentArrowMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void RemoveCollider(GameObject objectToClean)
        {
            var collider = objectToClean.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private void CreateWaterVolume(Vector2 size, float depth)
        {
            foreach (var edge in BuildVolumeEdges(size, depth))
            {
                var edgeObject = new GameObject("海域体积边界");
                edgeObject.transform.SetParent(transform, false);
                var line = edgeObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.widthMultiplier = 0.12f;
                line.positionCount = 2;
                line.SetPosition(0, edge.Start);
                line.SetPosition(1, edge.End);
                line.sharedMaterial = RuntimeMaterialFactory.Line("海域体积边界材质", new Color(0.16f, 0.88f, 1f, 0.56f));
            }
        }

        private void CreateSurfaceGrid(Vector2 size, int divisions)
        {
            var halfX = size.x * 0.5f;
            var halfZ = size.y * 0.5f;
            var count = Mathf.Max(2, divisions);
            for (var index = 0; index <= count; index++)
            {
                var offsetX = Mathf.Lerp(-halfX, halfX, index / (float)count);
                var offsetZ = Mathf.Lerp(-halfZ, halfZ, index / (float)count);
                CreateSurfaceLine(new Vector3(-halfX, 0.05f, offsetZ), new Vector3(halfX, 0.05f, offsetZ));
                CreateSurfaceLine(new Vector3(offsetX, 0.05f, -halfZ), new Vector3(offsetX, 0.05f, halfZ));
            }
        }

        private void CreateSurfaceLine(Vector3 start, Vector3 end)
        {
            var lineObject = new GameObject("海面定位网格");
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = 0.055f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.sharedMaterial = RuntimeMaterialFactory.Line("海面定位网格材质", new Color(0.28f, 0.8f, 0.96f, 0.28f));
        }

        private void CreateCube(string name, Vector3 position, Vector3 scale, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = color.a < 0.99f
                ? RuntimeMaterialFactory.Transparent(name + "材质", color)
                : RuntimeMaterialFactory.Opaque(name + "材质", color);
        }
    }
}
