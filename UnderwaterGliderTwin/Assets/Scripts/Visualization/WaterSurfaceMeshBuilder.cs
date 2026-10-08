using UnityEngine;
using UnityEngine.Rendering;

namespace UnderwaterGliderTwin.Visualization
{
    public static class WaterSurfaceMeshBuilder
    {
        private const float DenseSegmentFraction = 0.68f;
        private const float DenseHalfExtentFraction = 0.14f;

        public static Mesh Build(float sizeM, int segments, float maximumWaveHeightM)
        {
            var safeSize = Mathf.Max(1f, sizeM);
            var safeSegments = Mathf.Clamp(segments, 1, 512);
            var verticesPerSide = safeSegments + 1;
            var vertices = new Vector3[verticesPerSide * verticesPerSide];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[safeSegments * safeSegments * 6];
            var halfSize = safeSize * 0.5f;

            for (var z = 0; z < verticesPerSide; z++)
            {
                var zPosition = MapGridCoordinate(z, safeSegments, halfSize);
                var v = z / (float)safeSegments;
                for (var x = 0; x < verticesPerSide; x++)
                {
                    var u = x / (float)safeSegments;
                    var xPosition = MapGridCoordinate(x, safeSegments, halfSize);
                    var vertexIndex = z * verticesPerSide + x;
                    vertices[vertexIndex] = new Vector3(
                        xPosition,
                        0f,
                        zPosition);
                    uvs[vertexIndex] = new Vector2(u, v);
                }
            }

            var triangleIndex = 0;
            for (var z = 0; z < safeSegments; z++)
            {
                for (var x = 0; x < safeSegments; x++)
                {
                    var bottomLeft = z * verticesPerSide + x;
                    var topLeft = bottomLeft + verticesPerSide;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topLeft + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "Deterministic Water Surface",
                indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(
                Vector3.zero,
                new Vector3(safeSize, Mathf.Max(1f, maximumWaveHeightM * 2f + 0.5f), safeSize));
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static float MapGridCoordinate(int index, int segments, float halfSize)
        {
            var normalized = index / (float)segments * 2f - 1f;
            var denseSegmentStart = -DenseSegmentFraction;
            var denseSegmentEnd = DenseSegmentFraction;
            float mapped;

            if (normalized < denseSegmentStart)
            {
                mapped = Mathf.Lerp(-1f, -DenseHalfExtentFraction,
                    Mathf.InverseLerp(-1f, denseSegmentStart, normalized));
            }
            else if (normalized <= denseSegmentEnd)
            {
                mapped = Mathf.Lerp(-DenseHalfExtentFraction, DenseHalfExtentFraction,
                    Mathf.InverseLerp(denseSegmentStart, denseSegmentEnd, normalized));
            }
            else
            {
                mapped = Mathf.Lerp(DenseHalfExtentFraction, 1f,
                    Mathf.InverseLerp(denseSegmentEnd, 1f, normalized));
            }

            return mapped * halfSize;
        }
    }
}
