using System;
using System.Collections.Generic;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class TrajectoryView : MonoBehaviour
    {
        private LineRenderer travelledLine;
        private PlaybackController playback;
        private Vector3[] travelledBuffer = Array.Empty<Vector3>();
        private int lastTravelledCount = -1;

        public Vector3[] FullTrajectoryPoints { get; private set; } = Array.Empty<Vector3>();

        public void Initialize(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, PlaybackController playbackController)
        {
            playback = playbackController;
            FullTrajectoryPoints = TrajectorySampler.Sample(frames, mapper, 8000);
            travelledBuffer = new Vector3[FullTrajectoryPoints.Length];
            Array.Copy(FullTrajectoryPoints, travelledBuffer, FullTrajectoryPoints.Length);

            var fullLine = CreateLine("FullTrajectory", new Color(0.18f, 0.45f, 0.65f, 0.55f), 0.08f);
            fullLine.positionCount = FullTrajectoryPoints.Length;
            fullLine.SetPositions(FullTrajectoryPoints);

            travelledLine = CreateLine("TravelledTrajectory", new Color(0.0f, 0.95f, 1f, 1f), 0.13f);
            playback.FrameChanged += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
        }

        public void SetVisible(bool visible)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChanged -= OnFrameChanged;
            }
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01)
        {
            if (travelledLine == null || FullTrajectoryPoints.Length == 0)
            {
                return;
            }

            var count = Mathf.Clamp(Mathf.CeilToInt(progress01 * (FullTrajectoryPoints.Length - 1)) + 1, 1, FullTrajectoryPoints.Length);
            if (count == lastTravelledCount)
            {
                return;
            }

            travelledLine.positionCount = count;
            travelledLine.SetPositions(travelledBuffer);

            lastTravelledCount = count;
        }

        private LineRenderer CreateLine(string lineName, Color color, float width)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.sharedMaterial = CreateLineMaterial(color);
            return line;
        }

        private static Material CreateLineMaterial(Color color)
        {
            var material = new Material(Shader.Find("Sprites/Default"))
            {
                color = color
            };
            return material;
        }
    }
}
