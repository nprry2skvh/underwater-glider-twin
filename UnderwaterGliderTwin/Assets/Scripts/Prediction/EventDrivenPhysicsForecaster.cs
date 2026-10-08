using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Prediction
{
    /// <summary>Physical validation baseline, not the legacy analogue predictor.
    /// Only the issue-time state and explicit profile enter continuation.</summary>
    public static class EventDrivenPhysicsForecaster
    {
        public static IReadOnlyList<TelemetryFrame> Forecast(
            SimulationStateSnapshot snapshot, SimulationProfile profile,
            IReadOnlyList<float> targetElapsedSeconds)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!snapshot.HasExplicitProfile || !snapshot.HasExplicitDynamics || !snapshot.HasExplicitCurrent
                || profile.Dynamics == null || (profile.OceanCurrentProfile == null && profile.OceanCurrentField == null))
                throw new InvalidOperationException("Physical baseline unavailable: explicit profile/dynamics/current dependencies required.");
            if (!GliderDynamicsProfileValidator.TryValidate(profile.Dynamics, out var dynamicsError))
                throw new InvalidOperationException("Physical baseline unavailable: " + dynamicsError);
            // An explicitly supplied empty current configuration declares zero
            // simulated flow; null sources mean missing data, never zero truth.
            if (snapshot.Profile == null || !snapshot.Frame.MissionState.HasValue
                || !snapshot.Frame.Diagnostics.HasValue)
                throw new InvalidOperationException("Physical baseline unavailable: explicit mission/dynamics state required.");
            if (targetElapsedSeconds == null || targetElapsedSeconds.Count == 0)
                throw new ArgumentException("A nonempty independent target time axis is required.", nameof(targetElapsedSeconds));
            var previous = snapshot.Frame.ElapsedSeconds;
            foreach (var target in targetElapsedSeconds)
            {
                if (float.IsNaN(target) || float.IsInfinity(target) || target - previous < .1f)
                    throw new ArgumentException("Targets must be finite, strictly increasing and at least 0.1s apart.");
                previous = target;
            }
            var stepper = SimulationMissionStepper.FromSnapshot(snapshot, profile);
            var frames = new List<TelemetryFrame>(targetElapsedSeconds.Count);
            previous = snapshot.Frame.ElapsedSeconds;
            foreach (var target in targetElapsedSeconds)
            {
                if (!stepper.TryAdvance(target - previous, out var frame))
                    throw new InvalidOperationException("Physical baseline unavailable: mission ended before requested target.");
                if (Math.Abs(frame.ElapsedSeconds - target) > .05f)
                    throw new InvalidOperationException("Physical stepper did not reach the requested target time.");
                frames.Add(frame);
                previous = target;
            }
            return frames.AsReadOnly();
        }
    }
}
