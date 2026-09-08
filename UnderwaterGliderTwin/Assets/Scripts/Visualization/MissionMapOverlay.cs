using System.Collections.Generic;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class MissionMapOverlay : MonoBehaviour
    {
        public static Vector3[] BuildActualPoints(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper)
        {
            var points = new Vector3[frames?.Count ?? 0];
            for (var index = 0; index < points.Length; index++)
            {
                points[index] = mapper.Map(frames[index]);
            }
            return points;
        }

        public static float ComputeHorizontalExtent(IReadOnlyList<Vector3> points, float padding = 20f)
        {
            var extents = ComputeHorizontalExtents(points, 0.30f, 64f, padding);
            return Mathf.Max(extents.x, extents.y);
        }

        public static Vector2 ComputeHorizontalExtents(
            IReadOnlyList<Vector3> points,
            float paddingRatio = 0.30f,
            float minimumExtent = 64f,
            float minimumPadding = 20f)
        {
            if (points == null || points.Count == 0)
            {
                return new Vector2(minimumExtent, minimumExtent);
            }

            var bounds = BuildBounds(points);
            var safePaddingRatio = Mathf.Max(0f, paddingRatio);
            var safeMinimumPadding = Mathf.Max(0f, minimumPadding);
            var paddingX = Mathf.Max(safeMinimumPadding, bounds.size.x * safePaddingRatio);
            var paddingZ = Mathf.Max(safeMinimumPadding, bounds.size.z * safePaddingRatio);
            return new Vector2(
                Mathf.Max(minimumExtent, bounds.size.x + paddingX),
                Mathf.Max(minimumExtent, bounds.size.z + paddingZ));
        }

        public static Vector3 ComputeHorizontalCenter(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                return Vector3.zero;
            }

            var center = BuildBounds(points).center;
            return new Vector3(center.x, 0f, center.z);
        }

        public static Vector3[] BuildNoCurrentBaselinePoints(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper)
        {
            var points = new List<Vector3>();
            if (frames == null || mapper == null) return points.ToArray();
            foreach (var frame in frames)
            {
                if (!frame.HasPlannedPosition) continue;
                var planned = new TelemetryFrame(
                    frame.RowIndex, frame.RawTime, frame.ElapsedSeconds, frame.PlannedLongitudeDeg, frame.PlannedLatitudeDeg,
                    frame.DepthM, frame.AltitudeM, frame.HeadingDeg, frame.PitchDeg, frame.RollDeg,
                    frame.Voltage24V, frame.Current24A, frame.BatteryPercent, frame.WorkMode, frame.RunState,
                    frame.TargetSegment, frame.TargetHeadingDeg, frame.TargetDepthM, frame.TargetAltitudeM,
                    frame.PropellerRpm, frame.PistonMm, frame.TurnAngleDeg);
                var point = mapper.Map(planned);
                points.Add(point);
            }
            return points.ToArray();
        }

        public void Initialize(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper)
        {
            var points = BuildActualPoints(frames, mapper);
            if (points.Length < 2) return;
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "任务目标点";
            target.transform.SetParent(transform, false);
            target.transform.position = points[points.Length - 1];
            target.transform.position += Vector3.up * 0.55f;
            target.transform.localScale = Vector3.one * 0.7f;
            target.GetComponent<Renderer>().sharedMaterial = RuntimeMaterialFactory.Opaque("任务目标材质", new Color(1f, 0.82f, 0.16f, 1f));
        }

        private static Bounds BuildBounds(IReadOnlyList<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (var index = 1; index < points.Count; index++)
            {
                bounds.Encapsulate(points[index]);
            }

            return bounds;
        }

        private void CreateLine(string name, Vector3[] points, float width, Color color)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.sharedMaterial = RuntimeMaterialFactory.Line(name + "材质", color);
        }
    }
}
