using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public enum LaunchMode { Welcome, Csv, Simulation }

    public sealed class LaunchRequest
    {
        public LaunchMode Mode { get; }
        public string CsvPath { get; }
        public SimulationProfile SimulationProfile { get; }
        public bool HasErrors => Errors.Count > 0;
        public IReadOnlyList<string> Errors { get; }

        public LaunchRequest(LaunchMode mode, string csvPath, SimulationProfile profile, IReadOnlyList<string> errors)
        {
            Mode = mode;
            CsvPath = csvPath ?? string.Empty;
            SimulationProfile = profile != null ? profile.Clone() : SimulationProfile.Default;
            Errors = errors ?? new List<string>();
        }
    }
}
