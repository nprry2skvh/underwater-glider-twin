namespace UnderwaterGliderTwin.Telemetry
{
    public readonly struct TelemetryFrame
    {
        public readonly int RowIndex;
        public readonly string RawTime;
        public readonly double LongitudeDeg;
        public readonly double LatitudeDeg;
        public readonly float DepthM;
        public readonly float AltitudeM;
        public readonly float HeadingDeg;
        public readonly float PitchDeg;
        public readonly float RollDeg;
        public readonly float Voltage24V;
        public readonly float Current24A;
        public readonly float BatteryPercent;
        public readonly string WorkMode;
        public readonly string RunState;
        public readonly float TargetSegment;
        public readonly float TargetHeadingDeg;
        public readonly float TargetDepthM;
        public readonly float TargetAltitudeM;
        public readonly float PropellerRpm;
        public readonly float PistonMm;
        public readonly float TurnAngleDeg;

        public TelemetryFrame(
            int rowIndex,
            string rawTime,
            double longitudeDeg,
            double latitudeDeg,
            float depthM,
            float altitudeM,
            float headingDeg,
            float pitchDeg,
            float rollDeg,
            float voltage24V,
            float current24A,
            float batteryPercent,
            string workMode,
            string runState,
            float targetSegment,
            float targetHeadingDeg,
            float targetDepthM,
            float targetAltitudeM,
            float propellerRpm,
            float pistonMm,
            float turnAngleDeg)
        {
            RowIndex = rowIndex;
            RawTime = rawTime;
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
            DepthM = depthM;
            AltitudeM = altitudeM;
            HeadingDeg = headingDeg;
            PitchDeg = pitchDeg;
            RollDeg = rollDeg;
            Voltage24V = voltage24V;
            Current24A = current24A;
            BatteryPercent = batteryPercent;
            WorkMode = workMode;
            RunState = runState;
            TargetSegment = targetSegment;
            TargetHeadingDeg = targetHeadingDeg;
            TargetDepthM = targetDepthM;
            TargetAltitudeM = targetAltitudeM;
            PropellerRpm = propellerRpm;
            PistonMm = pistonMm;
            TurnAngleDeg = turnAngleDeg;
        }
    }
}
