using UnityEngine;

namespace UnderwaterGliderTwin.Playback
{
    public readonly struct ContinuousMotionSample
    {
        public int LowerIndex { get; }
        public int UpperIndex { get; }
        public float Interpolation01 { get; }
        public float ElapsedSeconds { get; }
        public double LongitudeDeg { get; }
        public double LatitudeDeg { get; }
        public float DepthM { get; }
        public float HeadingDeg { get; }
        public float PitchDeg { get; }
        public float RollDeg { get; }
        public bool HasUsableCoordinates { get; }
        public bool HasDiagnostics { get; }
        public Vector3 WaterVelocityEndMps { get; }
        public Vector3 CurrentVelocityEndMps { get; }
        public Vector3 GroundVelocityEndMps { get; }
        public Vector3 DisplayVelocityEnuMps { get; }
        public Vector3 ControlSurfaceDeflectionDeg { get; }

        public ContinuousMotionSample(
            int lowerIndex,
            int upperIndex,
            float interpolation01,
            float elapsedSeconds,
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float headingDeg,
            float pitchDeg,
            float rollDeg,
            bool hasUsableCoordinates,
            bool hasDiagnostics,
            Vector3 waterVelocityEndMps,
            Vector3 currentVelocityEndMps,
            Vector3 groundVelocityEndMps,
            Vector3 displayVelocityEnuMps,
            Vector3 controlSurfaceDeflectionDeg)
        {
            LowerIndex = lowerIndex;
            UpperIndex = upperIndex;
            Interpolation01 = interpolation01;
            ElapsedSeconds = elapsedSeconds;
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
            DepthM = depthM;
            HeadingDeg = headingDeg;
            PitchDeg = pitchDeg;
            RollDeg = rollDeg;
            HasUsableCoordinates = hasUsableCoordinates;
            HasDiagnostics = hasDiagnostics;
            WaterVelocityEndMps = waterVelocityEndMps;
            CurrentVelocityEndMps = currentVelocityEndMps;
            GroundVelocityEndMps = groundVelocityEndMps;
            DisplayVelocityEnuMps = displayVelocityEnuMps;
            ControlSurfaceDeflectionDeg = controlSurfaceDeflectionDeg;
        }
    }
}
