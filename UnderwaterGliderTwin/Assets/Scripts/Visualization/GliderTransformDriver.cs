using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class GliderTransformDriver : MonoBehaviour
    {
        private PlaybackController playback;
        private GeoCoordinateMapper mapper;
        private AttitudeSettings attitudeSettings = AttitudeSettings.Default;

        public void Initialize(PlaybackController playbackController, GeoCoordinateMapper coordinateMapper)
        {
            Initialize(playbackController, coordinateMapper, AttitudeSettings.Default);
        }

        public void Initialize(PlaybackController playbackController, GeoCoordinateMapper coordinateMapper, AttitudeSettings settings)
        {
            if (playback != null)
            {
                playback.ContinuousChanged -= OnContinuousChanged;
            }

            playback = playbackController;
            mapper = coordinateMapper;
            attitudeSettings = settings;
            playback.ContinuousChanged -= OnContinuousChanged;
            playback.ContinuousChanged += OnContinuousChanged;
            OnContinuousChanged(playback.Model.ContinuousIndex, playback.Model.Progress01, FrameUpdateReason.Initial);
        }

        public Vector3 PlaybackVelocity { get; private set; }
        public float HorizontalScale => mapper != null ? mapper.HorizontalScale : 0f;

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.ContinuousChanged -= OnContinuousChanged;
            }
        }

        private void OnContinuousChanged(float continuousIndex, float progress01, FrameUpdateReason reason)
        {
            var sample = ContinuousMotionSampler.Sample(
                playback.Model.Frames,
                playback.Model.ContinuousElapsedSeconds);
            PlaybackVelocity = ResolvePlaybackVelocity(sample) * playback.Model.Direction;
            transform.SetPositionAndRotation(
                ResolvePosition(sample),
                PoseMapper.ToRotation(
                    sample.HeadingDeg,
                    sample.PitchDeg,
                    sample.RollDeg,
                    attitudeSettings));
        }

        private Vector3 ResolvePlaybackVelocity(ContinuousMotionSample sample)
        {
            return sample.HasDiagnostics
                ? mapper.MapDynamicsVelocity(sample.GroundVelocityEndMps)
                : mapper.MapEnuPosition(sample.DisplayVelocityEnuMps);
        }

        private Vector3 ResolvePosition(ContinuousMotionSample sample)
        {
            var mappedDepth = mapper.MapDepth(sample.DepthM);
            if (sample.HasUsableCoordinates)
            {
                var lowerPosition = mapper.Map(playback.Model.Frames[sample.LowerIndex]);
                return sample.LowerIndex == sample.UpperIndex
                    ? lowerPosition
                    : Vector3.Lerp(
                        lowerPosition,
                        mapper.Map(playback.Model.Frames[sample.UpperIndex]),
                        sample.Interpolation01);
            }

            var frames = playback.Model.Frames;
            for (var index = sample.LowerIndex; index >= 0; index--)
            {
                if (TelemetryPositionUtility.HasUsableCoordinates(frames[index]))
                {
                    var position = mapper.Map(frames[index]);
                    return new Vector3(position.x, mappedDepth, position.z);
                }
            }

            return new Vector3(0f, mappedDepth, 0f);
        }
    }
}
