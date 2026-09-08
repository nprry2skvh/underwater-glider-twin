using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public struct GliderDynamicsState
    {
        public Vector3 PositionEndM;
        public Vector3 EarthVelocityEndMps;
        public Vector3 WaterVelocityEndMps;
        public float HeadingDeg;
        public float PitchDeg;
        public float RollDeg;
        public float YawRateDegPerSecond;
        public float PitchRateDegPerSecond;
        public float RollRateDegPerSecond;
        public Vector3 AngularVelocityRadPerSecond;
        public float NetBuoyancyForceN;
        public float PistonPositionMm;
        public float RollControlSurfaceDeflectionDeg;
        public float PitchControlSurfaceDeflectionDeg;
        public float YawControlSurfaceDeflectionDeg;
        public float ActuatorPowerWatts;
        public float BatteryPercent;
        public float AngleOfAttackDeg;
        public float SideslipDeg;
        public float LiftForceN;
        public float DragForceN;
        public float SideForceN;
        public Vector3 HydrodynamicMomentNm;

        public static GliderDynamicsState AtSurface(float headingDeg = 0f, float batteryPercent = 96f)
        {
            return new GliderDynamicsState
            {
                HeadingDeg = headingDeg,
                BatteryPercent = batteryPercent
            };
        }
    }
}
