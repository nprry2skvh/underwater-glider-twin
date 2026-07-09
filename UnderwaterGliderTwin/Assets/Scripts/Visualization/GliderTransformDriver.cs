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

        public void Initialize(PlaybackController playbackController, GeoCoordinateMapper coordinateMapper)
        {
            if (playback != null)
            {
                playback.FrameChanged -= OnFrameChanged;
            }

            playback = playbackController;
            mapper = coordinateMapper;
            playback.FrameChanged += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
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
            transform.position = mapper.Map(frame);
            transform.rotation = PoseMapper.ToRotation(frame);
        }
    }
}
