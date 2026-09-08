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
            if (!GliderDynamicsProfileValidator.TryValidate(profile.Dynamics, out var validationError))
            {
                return new TelemetryLoadResult(System.Array.Empty<TelemetryFrame>(), new[] { "Simulation profile validation failed: " + validationError }, 0);
            }

            return new TelemetryLoadResult(
                SimulationTrajectoryGenerator.GenerateFrames(profile),
                System.Array.Empty<string>(),
                skippedRows: 0);
        }
    }
}
