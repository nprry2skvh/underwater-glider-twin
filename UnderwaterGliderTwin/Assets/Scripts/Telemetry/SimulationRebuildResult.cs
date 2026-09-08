using System;
using System.Collections.Generic;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationRebuildResult
    {
        private static readonly IReadOnlyList<TelemetryFrame> EmptyFrames = Array.Empty<TelemetryFrame>();

        public bool Succeeded { get; }
        public bool WasCancelled { get; }
        public IReadOnlyList<TelemetryFrame> Frames { get; }
        public string Error { get; }

        private SimulationRebuildResult(
            bool succeeded,
            bool wasCancelled,
            IReadOnlyList<TelemetryFrame> frames,
            string error)
        {
            Succeeded = succeeded;
            WasCancelled = wasCancelled;
            Frames = frames ?? EmptyFrames;
            Error = error;
        }

        public static SimulationRebuildResult Success(IReadOnlyList<TelemetryFrame> frames)
        {
            return new SimulationRebuildResult(true, false, frames, null);
        }

        public static SimulationRebuildResult Failure(string error)
        {
            return new SimulationRebuildResult(false, false, EmptyFrames, error ?? "Simulation rebuild failed.");
        }

        public static SimulationRebuildResult Cancelled(string error = null)
        {
            return new SimulationRebuildResult(false, true, EmptyFrames, error ?? "Simulation rebuild was cancelled.");
        }
    }

    public interface ISimulationRebuildOperation
    {
        bool IsCancelled { get; }
        void Cancel();
    }

    public interface ISimulationFutureGenerator
    {
        ISimulationRebuildOperation GenerateFuture(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget,
            Action<SimulationRebuildResult> onCompleted);
    }
}
