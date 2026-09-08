using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct SimulationDiagnostics
    {
        public readonly Vector3 WaterVelocityEndMps;
        public readonly Vector3 CurrentVelocityEndMps;
        public readonly float NetBuoyancyForceN;
        public readonly float EnergyWatts;
        public readonly float SideSlipDeg;
        public readonly float AngleOfAttackDeg;
        public readonly float LiftForceN;
        public readonly float DragForceN;
        public readonly float SideForceN;
        public readonly Vector3 AngularVelocityRadPerSecond;
        public readonly Vector3 HydrodynamicMomentNm;
        public readonly float PistonPositionMm;
        public readonly Vector3 ControlSurfaceDeflectionDeg;
        public readonly float ActuatorPowerWatts;

        public SimulationDiagnostics(
            Vector3 waterVelocityEndMps,
            Vector3 currentVelocityEndMps,
            float netBuoyancyForceN,
            float energyWatts,
            float sideSlipDeg,
            float angleOfAttackDeg = 0f,
            float liftForceN = 0f,
            float dragForceN = 0f,
            float sideForceN = 0f,
            Vector3 angularVelocityRadPerSecond = default,
            Vector3 hydrodynamicMomentNm = default,
            float pistonPositionMm = 0f,
            Vector3 controlSurfaceDeflectionDeg = default,
            float actuatorPowerWatts = 0f)
        {
            WaterVelocityEndMps = waterVelocityEndMps;
            CurrentVelocityEndMps = currentVelocityEndMps;
            NetBuoyancyForceN = netBuoyancyForceN;
            EnergyWatts = energyWatts;
            SideSlipDeg = sideSlipDeg;
            AngleOfAttackDeg = angleOfAttackDeg;
            LiftForceN = liftForceN;
            DragForceN = dragForceN;
            SideForceN = sideForceN;
            AngularVelocityRadPerSecond = angularVelocityRadPerSecond;
            HydrodynamicMomentNm = hydrodynamicMomentNm;
            PistonPositionMm = pistonPositionMm;
            ControlSurfaceDeflectionDeg = controlSurfaceDeflectionDeg;
            ActuatorPowerWatts = actuatorPowerWatts;
        }
    }
}
