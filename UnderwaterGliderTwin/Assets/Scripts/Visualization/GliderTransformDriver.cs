using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class GliderTransformDriver : MonoBehaviour
    {
        private const float PlaybackPositionBlend = 0.28f;
        private const float PlaybackRotationBlend = 0.22f;
        private const float MaxHeadingStepPerUpdate = 8f;
        private const float MaxPitchStepPerUpdate = 3.5f;
        private const float MaxRollStepPerUpdate = 4.5f;

        private PlaybackController playback;
        private GeoCoordinateMapper mapper;
        private AttitudeSettings attitudeSettings = AttitudeSettings.Default;
        private bool hasPose;
        private bool hasValidPosition;
        private Vector3 lastValidPosition;
        private bool hasSmoothedAttitude;
        private float smoothedHeadingDeg;
        private float smoothedPitchDeg;
        private float smoothedRollDeg;

        public void Initialize(PlaybackController playbackController, GeoCoordinateMapper coordinateMapper)
        {
            Initialize(playbackController, coordinateMapper, AttitudeSettings.Default);
        }

        public void Initialize(PlaybackController playbackController, GeoCoordinateMapper coordinateMapper, AttitudeSettings settings)
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
            }

            playback = playbackController;
            mapper = coordinateMapper;
            attitudeSettings = settings;
            playback.FrameChangedWithReason += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
            }
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            var targetPosition = ResolvePosition(frame);
            var shouldSnap = !hasPose || reason != FrameUpdateReason.Playback || progress01 <= 0f || progress01 >= 1f;
            var targetRotation = ResolveRotation(frame, index, shouldSnap);

            if (shouldSnap)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, PlaybackPositionBlend);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, PlaybackRotationBlend);
            }

            hasPose = true;
        }

        private Vector3 ResolvePosition(TelemetryFrame frame)
        {
            var mappedDepth = mapper.MapDepth(frame.DepthM);
            if (TelemetryPositionUtility.HasUsableCoordinates(frame))
            {
                lastValidPosition = mapper.Map(frame);
                hasValidPosition = true;
                return lastValidPosition;
            }

            return hasValidPosition
                ? new Vector3(lastValidPosition.x, mappedDepth, lastValidPosition.z)
                : new Vector3(0f, mappedDepth, 0f);
        }

        private Quaternion ResolveRotation(TelemetryFrame frame, int index, bool shouldSnap)
        {
            if (shouldSnap || !hasSmoothedAttitude)
            {
                smoothedHeadingDeg = frame.HeadingDeg;
                smoothedPitchDeg = frame.PitchDeg;
                smoothedRollDeg = frame.RollDeg;
                hasSmoothedAttitude = true;
            }
            else
            {
                smoothedHeadingDeg = Mathf.MoveTowardsAngle(smoothedHeadingDeg, frame.HeadingDeg, MaxHeadingStepPerUpdate);
                smoothedPitchDeg = Mathf.MoveTowards(smoothedPitchDeg, frame.PitchDeg, MaxPitchStepPerUpdate);
                smoothedRollDeg = Mathf.MoveTowards(smoothedRollDeg, frame.RollDeg, MaxRollStepPerUpdate);
            }

            if (TryResolveGroundTrackDirection(index, out var groundTrackDirection))
            {
                var up = Mathf.Abs(Vector3.Dot(groundTrackDirection, Vector3.up)) > 0.98f
                    ? Vector3.forward
                    : Vector3.up;
                var trackRotation = Quaternion.LookRotation(groundTrackDirection, up);
                return trackRotation * Quaternion.AngleAxis(-smoothedRollDeg, Vector3.forward);
            }

            var smoothedFrame = new TelemetryFrame(
                frame.RowIndex,
                frame.RawTime,
                frame.ElapsedSeconds,
                frame.LongitudeDeg,
                frame.LatitudeDeg,
                frame.DepthM,
                frame.AltitudeM,
                smoothedHeadingDeg,
                smoothedPitchDeg,
                smoothedRollDeg,
                frame.Voltage24V,
                frame.Current24A,
                frame.BatteryPercent,
                frame.WorkMode,
                frame.RunState,
                frame.TargetSegment,
                frame.TargetHeadingDeg,
                frame.TargetDepthM,
                frame.TargetAltitudeM,
                frame.PropellerRpm,
                frame.PistonMm,
                frame.TurnAngleDeg);
            return PoseMapper.ToRotation(smoothedFrame, attitudeSettings);
        }

        private bool TryResolveGroundTrackDirection(int index, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (playback?.Model?.Frames == null || mapper == null)
            {
                return false;
            }

            var frames = playback.Model.Frames;
            if (index < 0 || index >= frames.Count || !TryMapPosition(frames[index], out var current))
            {
                return false;
            }

            var hasPrevious = TryFindMappedPosition(frames, index - 1, -1, out var previous);
            var hasNext = TryFindMappedPosition(frames, index + 1, 1, out var next);
            direction = hasPrevious && hasNext
                ? next - previous
                : hasNext ? next - current : hasPrevious ? current - previous : Vector3.zero;
            return direction.sqrMagnitude > 0.000001f;
        }

        private bool TryFindMappedPosition(System.Collections.Generic.IReadOnlyList<TelemetryFrame> frames, int index, int step, out Vector3 position)
        {
            for (var candidate = index; candidate >= 0 && candidate < frames.Count; candidate += step)
            {
                if (TryMapPosition(frames[candidate], out position))
                {
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private bool TryMapPosition(TelemetryFrame frame, out Vector3 position)
        {
            if (TelemetryPositionUtility.HasUsableCoordinates(frame))
            {
                position = mapper.Map(frame);
                return true;
            }

            position = Vector3.zero;
            return false;
        }
    }
}
