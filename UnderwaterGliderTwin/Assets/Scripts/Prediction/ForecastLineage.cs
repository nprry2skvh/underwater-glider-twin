using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    public static class ForecastLineage
    {
        public static string HashHistory(IReadOnlyList<TelemetryFrame> frames)
        {
            var text = new StringBuilder();
            foreach (var frame in frames)
            {
                text.AppendFormat(CultureInfo.InvariantCulture,
                    "{0}|{1:R}|{2:R}|{3:R}|{4:R}|{5:R}|{6:R}|{7:R}|{8:R}|{9:R}|{10:R}|{11:R}|{12}\n",
                    frame.RowIndex, frame.ElapsedSeconds, frame.LongitudeDeg, frame.LatitudeDeg,
                    frame.DepthM, frame.HeadingDeg, frame.PitchDeg, frame.RollDeg,
                    frame.TargetHeadingDeg, frame.TargetDepthM, frame.PistonMm, frame.TurnAngleDeg, frame.ProfileSequence);
            }
            return Hash(text.ToString());
        }

        public static string HashModelDirectory(string root)
        {
            if (!Directory.Exists(root)) return "unavailable";
            var paths = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            var text = new StringBuilder();
            using (var sha = SHA256.Create())
                foreach (var path in paths)
                    text.Append(path.Substring(root.Length)).Append(':')
                        .Append(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))).Append('\n');
            return Hash(text.ToString());
        }

        public static string HashCurrent(SimulationProfile profile)
        {
            if (profile == null) return "not_provided";
            var text = new StringBuilder("simulation_config_no_product_issue_metadata|");
            if (profile.OceanCurrentProfile != null)
                foreach (var layer in profile.OceanCurrentProfile.Layers)
                    text.AppendFormat(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R};",
                        layer.MinDepthM, layer.MaxDepthM, layer.EastwardMps, layer.NorthwardMps);
            if (profile.OceanCurrentField != null)
                foreach (var sample in profile.OceanCurrentField.Samples)
                    text.AppendFormat(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R};",
                        sample.LongitudeDeg, sample.LatitudeDeg, sample.DepthM, sample.ElapsedSeconds,
                        sample.EastwardMps, sample.NorthwardMps, sample.VerticalMps);
            return "simulation_config:" + Hash(text.ToString());
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
