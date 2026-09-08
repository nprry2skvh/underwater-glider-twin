using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class SimulationTrajectoryGenerator
    {
        public static IReadOnlyList<TelemetryFrame> GenerateFrames(SimulationProfile profile)
        {
            var effectiveProfile = (profile ?? SimulationProfile.Default).Clone();
            var stepper = SimulationMissionStepper.CreateInitial(effectiveProfile);
            var frames = new List<TelemetryFrame>();
            frames.Add(stepper.CurrentFrame());
            var sampleInterval = Mathf.Max(0.1f, effectiveProfile.SampleIntervalSeconds);
            while (stepper.TryAdvance(sampleInterval, out var frame))
            {
                frames.Add(frame);
            }

            return frames;
        }

        public static IEnumerable<IReadOnlyList<TelemetryFrame>> GenerateFutureSlices(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget)
        {
            return GenerateFutureSlices(snapshot, profile, frameSliceBudget, int.MaxValue);
        }

        public static IEnumerable<IReadOnlyList<TelemetryFrame>> GenerateFutureSlices(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget,
            int maximumFrameCount)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var budget = Math.Max(1, frameSliceBudget);
            var maximumFrames = Math.Max(0, maximumFrameCount);
            var stepper = SimulationMissionStepper.FromSnapshot(snapshot, profile.Clone());
            var sampleInterval = Mathf.Max(0.1f, profile.SampleIntervalSeconds);
            var slice = new List<TelemetryFrame>(budget);
            var generatedCount = 0;
            while (generatedCount < maximumFrames && stepper.TryAdvance(sampleInterval, out var frame))
            {
                slice.Add(frame);
                generatedCount++;
                if (slice.Count < budget)
                {
                    continue;
                }

                yield return slice;
                slice = new List<TelemetryFrame>(budget);
            }

            if (slice.Count > 0)
            {
                yield return slice;
            }
        }
    }

    public sealed class SimulationFutureTrajectoryGenerator : ISimulationFutureGenerator
    {
        private readonly MonoBehaviour coroutineHost;

        public SimulationFutureTrajectoryGenerator(MonoBehaviour coroutineHost)
        {
            this.coroutineHost = coroutineHost != null
                ? coroutineHost
                : throw new ArgumentNullException(nameof(coroutineHost));
        }

        public ISimulationRebuildOperation GenerateFuture(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget,
            int maximumFrameCount,
            Action<SimulationRebuildResult> onCompleted)
        {
            if (onCompleted == null)
            {
                throw new ArgumentNullException(nameof(onCompleted));
            }

            var operation = new CoroutineRebuildOperation();
            operation.Coroutine = coroutineHost.StartCoroutine(
                Generate(snapshot, profile, frameSliceBudget, maximumFrameCount, onCompleted, operation));
            return operation;
        }

        private static IEnumerator Generate(
            SimulationStateSnapshot snapshot,
            SimulationProfile profile,
            int frameSliceBudget,
            int maximumFrameCount,
            Action<SimulationRebuildResult> onCompleted,
            CoroutineRebuildOperation operation)
        {
            var stagedFuture = new List<TelemetryFrame>();
            IEnumerator<IReadOnlyList<TelemetryFrame>> slices;
            try
            {
                slices = SimulationTrajectoryGenerator
                    .GenerateFutureSlices(snapshot, profile, frameSliceBudget, maximumFrameCount)
                    .GetEnumerator();
            }
            catch (Exception ex)
            {
                onCompleted(SimulationRebuildResult.Failure(ex.Message));
                yield break;
            }

            using (slices)
            {
                while (!operation.IsCancelled)
                {
                    bool hasNext;
                    IReadOnlyList<TelemetryFrame> slice = null;
                    try
                    {
                        hasNext = slices.MoveNext();
                        if (hasNext)
                        {
                            slice = slices.Current;
                        }
                    }
                    catch (Exception ex)
                    {
                        onCompleted(SimulationRebuildResult.Failure(ex.Message));
                        yield break;
                    }

                    if (!hasNext)
                    {
                        onCompleted(SimulationRebuildResult.Success(stagedFuture));
                        yield break;
                    }

                    stagedFuture.AddRange(slice);
                    yield return null;
                }
            }

            onCompleted(SimulationRebuildResult.Cancelled());
        }

        private sealed class CoroutineRebuildOperation : ISimulationRebuildOperation
        {
            public Coroutine Coroutine { get; set; }
            public bool IsCancelled { get; private set; }

            public void Cancel()
            {
                IsCancelled = true;
            }
        }
    }
}
