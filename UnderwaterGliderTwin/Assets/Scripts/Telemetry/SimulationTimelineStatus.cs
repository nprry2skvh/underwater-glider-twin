namespace UnderwaterGliderTwin.Telemetry
{
    public enum SimulationTimelineStatus
    {
        Idle,
        Queued,
        Generating,
        WaitingForFuture,
        Committed,
        Completed,
        Cancelled,
        Failed
    }
}
