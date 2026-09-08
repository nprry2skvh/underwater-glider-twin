using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct SimulationStateSnapshot
    {
        public TelemetryFrame Frame { get; }
        public SimulationProfile Profile { get; }
        public GliderDynamicsState DynamicsState { get; }
        public SimulationMissionState MissionState { get; }

        private SimulationStateSnapshot(
            TelemetryFrame frame,
            SimulationProfile profile,
            GliderDynamicsState dynamicsState,
            SimulationMissionState missionState)
        {
            Frame = frame;
            Profile = profile;
            DynamicsState = dynamicsState;
            MissionState = missionState;
        }

        public static SimulationStateSnapshot FromFrame(TelemetryFrame frame, SimulationProfile profile)
        {
            var sourceProfile = profile?.Clone() ?? SimulationProfile.Default;
            var latitudeRadians = sourceProfile.OriginLatitudeDeg * Mathf.Deg2Rad;
            var metersPerDegreeLongitude = 111320d * System.Math.Cos(latitudeRadians);
            var localX = System.Math.Abs(metersPerDegreeLongitude) > 0.001d
                ? (float)((frame.LongitudeDeg - sourceProfile.OriginLongitudeDeg) * metersPerDegreeLongitude)
                : 0f;
            var localZ = (float)((frame.LatitudeDeg - sourceProfile.OriginLatitudeDeg) * 111320d);
            var headingRadians = frame.HeadingDeg * Mathf.Deg2Rad;
            var waterVelocity = new Vector3(
                Mathf.Sin(headingRadians) * sourceProfile.HorizontalSpeedMps,
                0f,
                Mathf.Cos(headingRadians) * sourceProfile.HorizontalSpeedMps);
            var currentVelocity = Vector3.zero;
            var diagnostics = frame.Diagnostics;
            if (diagnostics.HasValue)
            {
                waterVelocity = diagnostics.Value.WaterVelocityEndMps;
                currentVelocity = diagnostics.Value.CurrentVelocityEndMps;
            }

            var state = new GliderDynamicsState
            {
                PositionEndM = new Vector3(localX, frame.DepthM, localZ),
                EarthVelocityEndMps = waterVelocity + currentVelocity,
                WaterVelocityEndMps = waterVelocity,
                HeadingDeg = frame.HeadingDeg,
                PitchDeg = frame.PitchDeg,
                RollDeg = frame.RollDeg,
                NetBuoyancyForceN = diagnostics?.NetBuoyancyForceN ?? 0f,
                PistonPositionMm = diagnostics?.PistonPositionMm ?? frame.PistonMm,
                BatteryPercent = frame.BatteryPercent,
                AngleOfAttackDeg = diagnostics?.AngleOfAttackDeg ?? 0f,
                SideslipDeg = diagnostics?.SideSlipDeg ?? 0f,
                LiftForceN = diagnostics?.LiftForceN ?? 0f,
                DragForceN = diagnostics?.DragForceN ?? 0f,
                SideForceN = diagnostics?.SideForceN ?? 0f,
                AngularVelocityRadPerSecond = diagnostics?.AngularVelocityRadPerSecond ?? Vector3.zero,
                HydrodynamicMomentNm = diagnostics?.HydrodynamicMomentNm ?? Vector3.zero,
                RollControlSurfaceDeflectionDeg = diagnostics?.ControlSurfaceDeflectionDeg.x ?? 0f,
                PitchControlSurfaceDeflectionDeg = diagnostics?.ControlSurfaceDeflectionDeg.y ?? 0f,
                YawControlSurfaceDeflectionDeg = diagnostics?.ControlSurfaceDeflectionDeg.z ?? 0f,
                ActuatorPowerWatts = diagnostics?.ActuatorPowerWatts ?? 0f
            };

            if (diagnostics.HasValue)
            {
                state.RollRateDegPerSecond = diagnostics.Value.AngularVelocityRadPerSecond.x * Mathf.Rad2Deg;
                state.PitchRateDegPerSecond = diagnostics.Value.AngularVelocityRadPerSecond.y * Mathf.Rad2Deg;
                state.YawRateDegPerSecond = diagnostics.Value.AngularVelocityRadPerSecond.z * Mathf.Rad2Deg;
            }

            var missionState = frame.MissionState ?? InferMissionState(frame);
            return new SimulationStateSnapshot(frame, sourceProfile, state, missionState);
        }

        private static SimulationMissionState InferMissionState(TelemetryFrame frame)
        {
            var completedCycles = Mathf.Max(0, Mathf.RoundToInt(frame.TargetSegment) - 1);
            if (frame.RunState == "Turnaround")
            {
                return new SimulationMissionState { Phase = SimulationMissionPhase.BottomTurn, CompletedCycles = completedCycles };
            }

            if (frame.RunState == "Surface" || frame.DepthM <= 0.001f)
            {
                return SimulationMissionState.AtSurface(completedCycles);
            }

            if (frame.TargetDepthM > 0.001f && frame.DepthM >= frame.TargetDepthM - 1f)
            {
                return new SimulationMissionState { Phase = SimulationMissionPhase.BottomTurn, CompletedCycles = completedCycles };
            }

            return new SimulationMissionState
            {
                Phase = frame.TargetDepthM > 0.001f && frame.DepthM < frame.TargetDepthM
                    ? SimulationMissionPhase.Descent
                    : SimulationMissionPhase.Ascent,
                CompletedCycles = completedCycles
            };
        }
    }
}
