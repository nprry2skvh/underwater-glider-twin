using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public static class GlbTrajectoryWriter
    {
        public static void Write(Stream output, TrajectoryExportSnapshot snapshot, int vertexBudget = 100000)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var origin = snapshot.Timeline.Frames[0];
            var batches = new List<MeshBatch>();
            var perSegmentBudget = Math.Max(2, vertexBudget / Math.Max(1, snapshot.Timeline.Segments.Count));
            foreach (var segment in snapshot.Timeline.Segments)
            {
                var points = snapshot.Timeline.Frames
                    .Where(f => f.ProfileSequence == segment.ProfileSequence)
                    .Select(f => ToPosition(f, origin))
                    .ToList();
                if (points.Count == 0) continue;

                var decimated = TrajectoryMeshDecimator.Decimate(
                    points,
                    perSegmentBudget,
                    new[] { 0, points.Count - 1 });
                batches.Add(new MeshBatch("ProfileSequence-" + segment.ProfileSequence, decimated));
            }

            if (batches.Count == 0)
            {
                batches.Add(new MeshBatch("Trajectory", new[] { Vector3.zero }));
            }

            BuildJson(batches, out var jsonBytes, out var binBytes);
            WriteHeader(output, 12 + 8 + jsonBytes.Length + 8 + binBytes.Length);
            WriteChunk(output, "JSON", jsonBytes);
            WriteChunk(output, "BIN\0", binBytes);
        }

        private static Vector3 ToPosition(TelemetryFrame frame, TelemetryFrame origin)
        {
            var metersPerLongitude = 111320d * Math.Cos(origin.LatitudeDeg * Math.PI / 180d);
            var east = (frame.LongitudeDeg - origin.LongitudeDeg) * metersPerLongitude;
            var north = (frame.LatitudeDeg - origin.LatitudeDeg) * 111320d;
            return GlbCoordinateMapper.ToGlbPosition(east, north, frame.DepthM);
        }

        private static void BuildJson(
            IReadOnlyList<MeshBatch> batches,
            out byte[] jsonBytes,
            out byte[] binBytes)
        {
            using (var binary = new MemoryStream())
            using (var writer = new BinaryWriter(binary))
            {
                var views = new List<BufferViewInfo>();
                var accessors = new List<AccessorInfo>();
                var meshes = new StringBuilder();
                var nodes = new StringBuilder();
                var sceneNodes = new StringBuilder();

                for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    if (batchIndex > 0)
                    {
                        meshes.Append(',');
                        nodes.Append(',');
                        sceneNodes.Append(',');
                    }

                    var positionOffset = (int)binary.Position;
                    foreach (var point in batch.Points)
                    {
                        writer.Write(point.x);
                        writer.Write(point.y);
                        writer.Write(point.z);
                    }

                    var positionLength = (int)binary.Position - positionOffset;
                    var positionViewIndex = views.Count;
                    views.Add(new BufferViewInfo(positionOffset, positionLength, 34962));
                    var positionAccessorIndex = accessors.Count;
                    accessors.Add(new AccessorInfo(positionViewIndex, 5126, batch.Points.Count, "VEC3"));

                    var indexOffset = (int)binary.Position;
                    for (var index = 1; index < batch.Points.Count; index++)
                    {
                        writer.Write(index - 1);
                        writer.Write(index);
                    }

                    var indexCount = Math.Max(0, (batch.Points.Count - 1) * 2);
                    var indexLength = (int)binary.Position - indexOffset;
                    var indexAccessorIndex = -1;
                    if (indexCount > 0)
                    {
                        var indexViewIndex = views.Count;
                        views.Add(new BufferViewInfo(indexOffset, indexLength, 34963));
                        indexAccessorIndex = accessors.Count;
                        accessors.Add(new AccessorInfo(indexViewIndex, 5125, indexCount, "SCALAR"));
                    }

                    var primitive = indexAccessorIndex >= 0
                        ? "{\"attributes\":{\"POSITION\":" + positionAccessorIndex + "},\"indices\":" + indexAccessorIndex + ",\"mode\":1}"
                        : "{\"attributes\":{\"POSITION\":" + positionAccessorIndex + "},\"mode\":1}";
                    meshes.Append("{\"primitives\":[").Append(primitive).Append("]}");
                    nodes.Append("{\"name\":\"").Append(batch.Name).Append("\",\"mesh\":").Append(batchIndex).Append('}');
                    sceneNodes.Append(batchIndex);
                }

                binBytes = Pad(binary.ToArray(), 4, 0);
                var viewJson = string.Join(",", views.Select(view =>
                    "{\"buffer\":0,\"byteOffset\":" + view.Offset + ",\"byteLength\":" + view.Length + ",\"target\":" + view.Target + "}"));
                var accessorJson = string.Join(",", accessors.Select(accessor =>
                    "{\"bufferView\":" + accessor.BufferView + ",\"componentType\":" + accessor.ComponentType + ",\"count\":" + accessor.Count + ",\"type\":\"" + accessor.Type + "\"}"));
                var text = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"UnderwaterGliderTwin\"},\"scene\":0,\"scenes\":[{\"nodes\":[" + sceneNodes + "]}],\"nodes\":[" + nodes + "],\"meshes\":[" + meshes + "],\"buffers\":[{\"byteLength\":" + binBytes.Length + "}],\"bufferViews\":[" + viewJson + "],\"accessors\":[" + accessorJson + "]}";
                jsonBytes = Pad(Encoding.UTF8.GetBytes(text), 4, 0x20);
            }
        }

        private sealed class MeshBatch
        {
            public MeshBatch(string name, IReadOnlyList<Vector3> points)
            {
                Name = name;
                Points = points;
            }

            public string Name { get; }
            public IReadOnlyList<Vector3> Points { get; }
        }

        private readonly struct BufferViewInfo
        {
            public BufferViewInfo(int offset, int length, int target) { Offset = offset; Length = length; Target = target; }
            public int Offset { get; }
            public int Length { get; }
            public int Target { get; }
        }

        private readonly struct AccessorInfo
        {
            public AccessorInfo(int bufferView, int componentType, int count, string type) { BufferView = bufferView; ComponentType = componentType; Count = count; Type = type; }
            public int BufferView { get; }
            public int ComponentType { get; }
            public int Count { get; }
            public string Type { get; }
        }

        private static void WriteHeader(Stream output, int length)
        {
            using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                writer.Write(0x46546C67);
                writer.Write(2);
                writer.Write(length);
            }
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
            {
                writer.Write(data.Length);
                writer.Write(Encoding.ASCII.GetBytes(type));
                writer.Write(data);
            }
        }

        private static byte[] Pad(byte[] bytes, int alignment, byte pad)
        {
            var length = (bytes.Length + alignment - 1) / alignment * alignment;
            if (length == bytes.Length) return bytes;
            var result = new byte[length];
            Array.Copy(bytes, result, bytes.Length);
            for (var i = bytes.Length; i < result.Length; i++) result[i] = pad;
            return result;
        }
    }
}
