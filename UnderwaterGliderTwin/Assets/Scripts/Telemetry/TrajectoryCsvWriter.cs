using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class TrajectoryCsvWriter
    {
        public const string Header = "RowIndex,RawTime,ElapsedSeconds,LongitudeDeg,LatitudeDeg,EastM,NorthM,UpM,DepthM,AltitudeM,HeadingDeg,PitchDeg,RollDeg,Voltage24V,Current24A,BatteryPercent,WorkMode,RunState,TargetSegment,TargetHeadingDeg,TargetDepthM,TargetAltitudeM,PropellerRpm,PistonMm,TurnAngleDeg,ProfileSequence,PlannedLongitudeDeg,PlannedLatitudeDeg,MissionPhase,CompletedCycles";

        public static void Write(TextWriter writer, TrajectoryExportSnapshot snapshot)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            writer.WriteLine(Header);
            var origin = snapshot.Timeline.Frames[0];
            var metersPerLongitude = 111320d * Math.Cos(origin.LatitudeDeg * Math.PI / 180d);
            foreach (var f in snapshot.Timeline.Frames)
            {
                var east = (f.LongitudeDeg - origin.LongitudeDeg) * metersPerLongitude;
                var north = (f.LatitudeDeg - origin.LatitudeDeg) * 111320d;
                var mission = f.MissionState;
                var fields = new[]
                {
                    f.RowIndex.ToString(CultureInfo.InvariantCulture), Quote(f.RawTime), F(f.ElapsedSeconds), F(f.LongitudeDeg), F(f.LatitudeDeg), F(east), F(north), F(-f.DepthM), F(f.DepthM), F(f.AltitudeM), F(f.HeadingDeg), F(f.PitchDeg), F(f.RollDeg), F(f.Voltage24V), F(f.Current24A), F(f.BatteryPercent), Quote(f.WorkMode), Quote(f.RunState), F(f.TargetSegment), F(f.TargetHeadingDeg), F(f.TargetDepthM), F(f.TargetAltitudeM), F(f.PropellerRpm), F(f.PistonMm), F(f.TurnAngleDeg), f.ProfileSequence.ToString(CultureInfo.InvariantCulture), f.HasPlannedPosition ? F(f.PlannedLongitudeDeg) : string.Empty, f.HasPlannedPosition ? F(f.PlannedLatitudeDeg) : string.Empty, mission.HasValue ? mission.Value.Phase.ToString() : string.Empty, mission.HasValue ? mission.Value.CompletedCycles.ToString(CultureInfo.InvariantCulture) : string.Empty
                };
                writer.WriteLine(string.Join(",", fields));
            }
        }

        public static void WriteFile(string path, TrajectoryExportSnapshot snapshot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true))) Write(writer, snapshot);
        }

        private static string Quote(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string F(float value) => Finite(value).ToString("0.######", CultureInfo.InvariantCulture);
        private static string F(double value) => Finite(value).ToString("0.######", CultureInfo.InvariantCulture);
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private static double Finite(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
    }
}
