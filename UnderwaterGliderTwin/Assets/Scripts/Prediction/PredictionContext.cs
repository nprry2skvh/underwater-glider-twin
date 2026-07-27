using System.Collections.Generic;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public sealed class PredictionContext
    {
        public PredictionContext(IReadOnlyList<TelemetryFrame> frames, GeoCoordinateMapper mapper, PredictionWindow window)
        {
            Frames = frames;
            Mapper = mapper;
            Window = window;
        }

        public IReadOnlyList<TelemetryFrame> Frames { get; }
        public GeoCoordinateMapper Mapper { get; }
        public PredictionWindow Window { get; }
        public TelemetryFrame CurrentFrame => Window.WindowFrames[Window.WindowFrames.Count - 1];
    }
}
