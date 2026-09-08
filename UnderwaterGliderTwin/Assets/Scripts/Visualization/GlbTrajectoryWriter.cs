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
            var positions = new List<Vector3>();
            var indices = new List<int>();
            var nodeNames = new List<string>();
            var origin = snapshot.Timeline.Frames[0];
            foreach (var segment in snapshot.Timeline.Segments)
            {
                var points = snapshot.Timeline.Frames.Where(f => f.ProfileSequence == segment.ProfileSequence).Select(f => ToPosition(f, origin)).ToList();
                if (points.Count == 0) continue;
                var offset = positions.Count;
                var decimated = TrajectoryMeshDecimator.Decimate(points, Math.Max(2, vertexBudget / Math.Max(1, snapshot.Timeline.Segments.Count)), new[] { 0, points.Count - 1 });
                positions.AddRange(decimated);
                for (var i = 1; i < decimated.Count; i++) { indices.Add(offset + i - 1); indices.Add(offset + i); }
                nodeNames.Add("ProfileSequence-" + segment.ProfileSequence);
            }
            if (positions.Count == 0) positions.Add(Vector3.zero);
            var json = BuildJson(positions, indices, nodeNames, out var jsonBytes, out var binBytes);
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

        private static string BuildJson(List<Vector3> positions, List<int> indices, List<string> nodeNames, out byte[] jsonBytes, out byte[] binBytes)
        {
            using (var binary = new MemoryStream())
            using (var writer = new BinaryWriter(binary))
            {
                foreach (var point in positions) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                var positionBytes = (int)binary.Length;
                foreach (var index in indices) writer.Write(index);
                binBytes = Pad(binary.ToArray(), 4, 0);
                var meshJson = new StringBuilder();
                var nodesJson = new StringBuilder();
                var meshesJson = new StringBuilder();
                for (var i = 0; i < nodeNames.Count; i++)
                {
                    if (i > 0) { nodesJson.Append(','); meshesJson.Append(','); }
                    nodesJson.Append("{\"name\":\"").Append(nodeNames[i]).Append("\",\"mesh\":0}");
                    meshesJson.Append(i == 0 ? "{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1,\"mode\":1}]}" : "{\"primitives\":[]}");
                }
                if (nodeNames.Count == 0) { nodesJson.Append("{\"name\":\"Trajectory\"}"); meshesJson.Append("{\"primitives\":[]}"); }
                var text = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"UnderwaterGliderTwin\"},\"scene\":0,\"scenes\":[{\"nodes\":[" + string.Join(",", Enumerable.Range(0, nodeNames.Count)) + "]}],\"nodes\":[" + nodesJson + "],\"meshes\":[" + meshesJson + "],\"buffers\":[{\"byteLength\":" + binBytes.Length + "}],\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + positionBytes + ",\"target\":34962},{\"buffer\":0,\"byteOffset\":" + positionBytes + ",\"byteLength\":" + (binBytes.Length - positionBytes) + ",\"target\":34963}],\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":" + positions.Count + ",\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5125,\"count\":" + indices.Count + ",\"type\":\"SCALAR\"}]}";
                jsonBytes = Pad(Encoding.UTF8.GetBytes(text), 4, 0x20);
                return text;
            }
        }

        private static void WriteHeader(Stream output, int length) { using (var writer = new BinaryWriter(output, Encoding.UTF8, true)) { writer.Write(0x46546C67); writer.Write(2); writer.Write(length); } }
        private static void WriteChunk(Stream output, string type, byte[] data) { using (var writer = new BinaryWriter(output, Encoding.UTF8, true)) { writer.Write(data.Length); var typeBytes = Encoding.ASCII.GetBytes(type); writer.Write(typeBytes); writer.Write(data); } }
        private static byte[] Pad(byte[] bytes, int alignment, byte pad) { var length = (bytes.Length + alignment - 1) / alignment * alignment; if (length == bytes.Length) return bytes; var result = new byte[length]; Array.Copy(bytes, result, bytes.Length); for (var i = bytes.Length; i < result.Length; i++) result[i] = pad; return result; }
    }
}
