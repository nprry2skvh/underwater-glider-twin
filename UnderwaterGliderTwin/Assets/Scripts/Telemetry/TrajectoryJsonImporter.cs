using System;
using System.IO;
using System.Linq;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class TrajectoryJsonImporter
    {
        public static bool TryRestoreJson(string json, out SimulationTimelineSnapshot timeline, out string error)
        {
            timeline = null;
            if (!TrajectoryJsonCodec.TryDeserialize(json, out var snapshot, out error)) return false;
            timeline = snapshot.Timeline;
            return true;
        }

        public static bool TryRestore(string packageDirectory, out SimulationTrajectoryTimeline timeline, out TrajectoryPlaybackState playback, out string error)
        {
            timeline = null;
            playback = null;
            error = null;
            if (string.IsNullOrWhiteSpace(packageDirectory)) { error = "Package directory is empty."; return false; }
            var path = Path.Combine(packageDirectory, "trajectory.json");
            if (!File.Exists(path)) { error = "trajectory.json is missing."; return false; }
            if (!TrajectoryJsonCodec.TryDeserialize(File.ReadAllText(path), out var export, out error)) return false;
            var source = export.Timeline;
            var segments = source.Segments.OrderBy(s => s.ProfileSequence).ToArray();
            if (segments.Length == 0) { error = "No profile segments were found."; return false; }
            var firstBoundary = FindFrameIndex(source, segments[0].StartRowIndex);
            if (firstBoundary < 0) { error = "Initial segment start is missing."; return false; }
            var firstFutureBoundary = segments.Length > 1 ? FindFrameIndex(source, segments[1].StartRowIndex) : source.Frames.Count;
            var restoredFrames = source.Frames.Take(Math.Max(1, firstFutureBoundary)).ToArray();
            try
            {
                timeline = new SimulationTrajectoryTimeline(restoredFrames, segments[0]);
                var sourceFutureStart = firstFutureBoundary;
                for (var i = 1; i < segments.Length; i++)
                {
                    var boundary = FindFrameIndex(source, segments[i].StartRowIndex);
                    if (boundary < sourceFutureStart) { error = "Profile segment boundaries are not monotonic."; timeline = null; return false; }
                    var nextBoundary = i + 1 < segments.Length ? FindFrameIndex(source, segments[i + 1].StartRowIndex) : source.Frames.Count;
                    if (nextBoundary <= boundary) { error = "Profile segment boundaries are not monotonic."; timeline = null; return false; }
                    var future = source.Frames.Skip(boundary).Take(nextBoundary - boundary).ToArray();
                    timeline.ReplaceFutureFrom(timeline.CommittedSnapshot.Frames.Count - 1, future, segments[i]);
                    sourceFutureStart = nextBoundary;
                }
                timeline.SetStatus(source.Status);
                playback = export.Playback;
                return true;
            }
            catch (Exception ex)
            {
                timeline = null;
                error = "Unable to restore timeline: " + ex.Message;
                return false;
            }
        }

        private static int FindFrameIndex(SimulationTimelineSnapshot snapshot, int rowIndex)
        {
            for (var i = 0; i < snapshot.Frames.Count; i++) if (snapshot.Frames[i].RowIndex == rowIndex) return i;
            return -1;
        }
    }
}
