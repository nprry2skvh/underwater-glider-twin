using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct SimulationStateSnapshot
    {
        public TelemetryFrame Frame { get; }
        public SimulationProfile Profile { get; }
        public GliderDynamicsState DynamicsState { get; }

        private SimulationStateSnapshot(
            TelemetryFrame frame,
            SimulationProfile profile,
            GliderDynamicsState dynamicsState)
        {
            Frame = frame;
            Profile = profile;
            DynamicsState = dynamicsState;
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
                Mathf.Sin(headingRadians) * sourceProfile.Dynamics.CruiseSpeedMps,
                0f,
                Mathf.Cos(headingRadians) * sourceProfile.Dynamics.CruiseSpeedMps);
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

            return new SimulationStateSnapshot(frame, sourceProfile, state);
        }
    }
}
