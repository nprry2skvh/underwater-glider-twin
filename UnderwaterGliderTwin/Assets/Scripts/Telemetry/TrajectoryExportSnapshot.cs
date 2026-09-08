using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Prediction;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class CameraSnapshot
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float FieldOfView { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public int CullingMask { get; }

        public CameraSnapshot(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Position = camera.transform.position;
            Rotation = camera.transform.rotation;
            FieldOfView = camera.fieldOfView;
            PixelWidth = camera.pixelWidth;
            PixelHeight = camera.pixelHeight;
            CullingMask = camera.cullingMask;
        }

        public CameraSnapshot(Vector3 position, Quaternion rotation, float fieldOfView, int pixelWidth, int pixelHeight, int cullingMask)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            CullingMask = cullingMask;
        }
    }

    public sealed class TrajectoryExportSnapshot
    {
        public const int SchemaVersion = 1;
        public const string AlgorithmVersion = "hybrid-glider-trajectory-v1";

        public SimulationTimelineSnapshot Timeline { get; }
        public TrajectoryPlaybackState Playback { get; }
        public PredictionSnapshot Prediction { get; }
        public CameraSnapshot Camera { get; }
        public DateTime CreatedAtUtc { get; }

        public TrajectoryExportSnapshot(
            SimulationTimelineSnapshot timeline,
            TrajectoryPlaybackState playback,
            PredictionSnapshot prediction = null,
            CameraSnapshot camera = null,
            DateTime? createdAtUtc = null)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            Playback = playback ?? throw new ArgumentNullException(nameof(playback));
            Prediction = prediction;
            Camera = camera;
            CreatedAtUtc = (createdAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        }

        public static TrajectoryExportSnapshot Capture(
            SimulationTimelineSnapshot timeline,
            PlaybackModel playback,
            PredictionSnapshot prediction,
            CameraSnapshot camera)
        {
            if (playback == null) throw new ArgumentNullException(nameof(playback));
            var current = playback.CurrentFrame;
            var state = new TrajectoryPlaybackState(
                playback.CurrentIndex,
                playback.CurrentIndex,
                current.ElapsedSeconds,
                playback.IsPlaying,
                playback.Speed,
                playback.Direction);
            return new TrajectoryExportSnapshot(timeline, state, prediction, camera);
        }
    }
}
