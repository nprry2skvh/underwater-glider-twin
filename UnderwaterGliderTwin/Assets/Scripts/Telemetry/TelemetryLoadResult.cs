using System.Collections.Generic;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class TelemetryLoadResult
    {
        public IReadOnlyList<TelemetryFrame> Frames { get; }
        public IReadOnlyList<string> Errors { get; }
        public int SkippedRows { get; }

        public TelemetryLoadResult(IReadOnlyList<TelemetryFrame> frames, IReadOnlyList<string> errors, int skippedRows)
        {
            Frames = frames;
            Errors = errors;
            SkippedRows = skippedRows;
        }
    }
}
