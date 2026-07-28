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

            var request = LaunchRequestParser.Parse(args, string.Empty, SimulationProfile.Default);
            if (request.HasErrors || request.Mode != LaunchMode.Simulation)
            {
                return false;
            }

            UseSimulation(request.SimulationProfile);
            return true;
        }
    }
}
