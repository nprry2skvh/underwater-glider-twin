using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public static class LaunchRequestParser
    {
        public static LaunchRequest Parse(IReadOnlyList<string> args, string preferredCsvPath, SimulationProfile defaultProfile)
        {
            var csvErrors = new List<string>();
            var simulationErrors = new List<string>();
            var csv = string.IsNullOrWhiteSpace(preferredCsvPath) ? string.Empty : Path.GetFullPath(preferredCsvPath);
            var csvSpecified = false;
            var simulation = false;
            var depth = 0f; var hasDepth = false;
            var east = 0f; var north = 0f; var hasCurrent = false;
            for (var i = 0; args != null && i < args.Count; i++)
            {
                var arg = args[i] ?? string.Empty;
                if (arg.Equals("--simulation", StringComparison.OrdinalIgnoreCase)) { simulation = true; continue; }
                if (arg.Equals("--simulation-depth", StringComparison.OrdinalIgnoreCase))
                {
                    simulation = true;
                    if (i + 1 >= args.Count || IsArgument(args[i + 1]) || !TryParseFiniteFloat(args[++i], out depth) || depth < 0f)
                        simulationErrors.Add("--simulation-depth requires a non-negative number.");
                    else hasDepth = true;
                    continue;
                }
                if (arg.Equals("--simulation-current", StringComparison.OrdinalIgnoreCase))
                {
                    simulation = true;
                    if (i + 2 >= args.Count || IsArgument(args[i + 1]) || IsArgument(args[i + 2])
                        || !TryParseFiniteFloat(args[++i], out east) || !TryParseFiniteFloat(args[++i], out north))
                        simulationErrors.Add("--simulation-current requires eastward and northward numbers.");
                    else hasCurrent = true;
                    continue;
                }
                if (arg.Equals("--csv", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--csv=", StringComparison.OrdinalIgnoreCase))
                {
                    csvSpecified = true;
                    var value = arg.StartsWith("--csv=", StringComparison.OrdinalIgnoreCase)
                        ? arg.Substring(6)
                        : (i + 1 < args.Count && !IsArgument(args[i + 1]) ? args[++i] : string.Empty);
                    if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--"))
                        csvErrors.Add("--csv requires a file path.");
                    else if (!File.Exists(Path.GetFullPath(value)))
                        csvErrors.Add($"CSV file does not exist: {value}");
                    else csv = Path.GetFullPath(value);
                }
            }
            if (simulation)
            {
                var profile = defaultProfile != null ? defaultProfile.Clone() : SimulationProfile.Default;
                if (hasDepth) { profile.TargetDepthM = depth; profile.WaterColumnDepthM = Math.Max(profile.WaterColumnDepthM, depth); }
                profile.CycleDurationSeconds = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                    profile.CycleDurationSeconds,
                    profile.TargetDepthM,
                    profile.HorizontalSpeedMps);
                if (hasCurrent) profile.OceanCurrentProfile = new OceanCurrentProfile(new[] { new OceanCurrentLayer(0f, profile.TargetDepthM, east, north) });
                return new LaunchRequest(simulationErrors.Count > 0 ? LaunchMode.Welcome : LaunchMode.Simulation, csv, profile, simulationErrors);
            }
            if (csvSpecified && csvErrors.Count == 0) return new LaunchRequest(LaunchMode.Csv, csv, null, csvErrors);
            return new LaunchRequest(LaunchMode.Welcome, csv, null, csvErrors);
        }

        private static bool IsArgument(string value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith("--", StringComparison.Ordinal);

        private static bool TryParseFiniteFloat(string value, out float parsed)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                || float.IsNaN(parsed)
                || float.IsInfinity(parsed))
            {
                parsed = 0f;
                return false;
            }

            return true;
        }
    }
}
