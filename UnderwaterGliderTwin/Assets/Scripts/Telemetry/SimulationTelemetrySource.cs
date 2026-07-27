namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationTelemetrySource : ITelemetrySource
    {
        private readonly SimulationProfile profile;

        public SimulationTelemetrySource(SimulationProfile profile)
        {
            this.profile = profile ?? SimulationProfile.Default;
        }

        public TelemetryLoadResult Load()
        {
            return new TelemetryLoadResult(
                SimulationTrajectoryGenerator.GenerateFrames(profile),
                System.Array.Empty<string>(),
                skippedRows: 0);
        }
    }
}
