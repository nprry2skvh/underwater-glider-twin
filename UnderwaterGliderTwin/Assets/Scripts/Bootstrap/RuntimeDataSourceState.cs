using System;
using System.Collections.Generic;
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
        public static RuntimeDataSourceMode CurrentMode { get; private set; } = RuntimeDataSourceMode.Simulation;
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

            return ApplyLaunchRequest(LaunchRequestParser.Parse(args, string.Empty, SimulationProfile.Default));
        }

        public static bool ApplyLaunchRequest(LaunchRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.HasErrors)
            {
                return false;
            }

            if (request.Mode == LaunchMode.Simulation)
            {
                UseSimulation(request.SimulationProfile);
                return true;
            }

            if (request.Mode == LaunchMode.Csv)
            {
                UseCsvPath(request.CsvPath);
                return true;
            }

            return false;
        }
    }
}
