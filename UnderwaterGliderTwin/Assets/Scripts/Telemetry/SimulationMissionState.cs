namespace UnderwaterGliderTwin.Telemetry
{
    public enum SimulationMissionPhase
    {
        Surface,
        Descent,
        BottomTurn,
        Ascent,
        LegTimeout
    }

    public struct SimulationMissionState
    {
        public SimulationMissionPhase Phase;
        public int CompletedCycles;
        public float LegElapsedSeconds;
        public float TurnaroundElapsedSeconds;
        public bool SafetyWarningRaised;

        public static SimulationMissionState AtSurface(int completedCycles = 0)
        {
            return new SimulationMissionState
            {
                Phase = SimulationMissionPhase.Surface,
                CompletedCycles = completedCycles
            };
        }
    }
}
