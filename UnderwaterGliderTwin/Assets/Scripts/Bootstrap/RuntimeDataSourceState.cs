using System;
using System.Collections.Generic;
using System.Globalization;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public enum RuntimeDataSourceMode
    {
        Csv,
        Simulation
    }

    public static class RuntimeDataSourceState
    {
        public static RuntimeDataSourceMode CurrentMode { get; private set; } = RuntimeDataSourceMode.Csv;
        public static string LastCsvPath { get; private set; } = string.Empty;
        public static SimulationProfile SimulationProfile { get; private set; } = SimulationProfile.Default;

        public static void UseCsvPath(string csvPath)
        {
            CurrentMode = RuntimeDataSourceMode.Csv;
            if (!string.IsNullOrWhiteSpace(csvPath))
            {
                LastCsvPath = csvPath;
            }
        }

        public static void UseSimulation(SimulationProfile profile)
        {
            CurrentMode = RuntimeDataSourceMode.Simulation;
            SimulationProfile = profile != null ? profile.Clone() : SimulationProfile.Default;
        }

        public static bool ApplyCommandLineArguments(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return false;
            }

            var simulationRequested = false;
            var hasLaunchCurrent = false;
            var hasLaunchDepth = false;
            var eastwardMps = 0f;
            var northwardMps = 0f;
            var targetDepthM = 0f;
            for (var i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], "--simulation", StringComparison.OrdinalIgnoreCase))
                {
                    simulationRequested = true;
                    continue;
                }

                if (string.Equals(args[i], "--simulation-depth", StringComparison.OrdinalIgnoreCase)
                    && i + 1 < args.Count
                    && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out targetDepthM)
                    && !float.IsNaN(targetDepthM)
                    && !float.IsInfinity(targetDepthM)
                    && targetDepthM >= 0f)
                {
                    simulationRequested = true;
                    hasLaunchDepth = true;
                    i += 1;
                    continue;
                }

                if (!string.Equals(args[i], "--simulation-current", StringComparison.OrdinalIgnoreCase)
                    || i + 2 >= args.Count
                    || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out eastwardMps)
                    || !float.TryParse(args[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out northwardMps))
                {
                    continue;
                }

                simulationRequested = true;
                hasLaunchCurrent = true;
                i += 2;
            }

            if (!simulationRequested)
            {
                return false;
            }

            var profile = SimulationProfile.Default;
            if (hasLaunchDepth)
            {
                profile.TargetDepthM = targetDepthM;
                profile.WaterColumnDepthM = Math.Max(profile.WaterColumnDepthM, targetDepthM);
            }

            profile.CycleDurationSeconds = MissionProfileConstraints.NormalizeEngineeringCycleDuration(
                profile.CycleDurationSeconds,
                profile.TargetDepthM,
                profile.HorizontalSpeedMps);

            if (hasLaunchCurrent)
            {
                profile.OceanCurrentProfile = new OceanCurrentProfile(new[]
                {
                    new OceanCurrentLayer(0f, profile.TargetDepthM, eastwardMps, northwardMps)
                });
            }

            UseSimulation(profile);
            return true;
        }
    }
}
