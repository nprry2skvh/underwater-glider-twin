using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class SimulationProfile
    {
        public int CycleCount { get; set; } = 6;
        public float CycleDurationSeconds { get; set; } = 900f;
        public float SampleIntervalSeconds { get; set; } = 5f;
        public float TargetDepthM { get; set; } = 160f;
        public float HorizontalSpeedMps { get; set; } = 0.65f;
        public float StartHeadingDeg { get; set; } = 42f;
        public float HeadingDeltaPerCycleDeg { get; set; } = 14f;
        public float PitchAmplitudeDeg { get; set; } = 12f;
        public float RollAmplitudeDeg { get; set; } = 3f;
        // NaN retains compatibility with profiles that only contain the legacy symmetric controls.
        public float DescentSpeedMps { get; set; } = float.NaN;
        public float AscentSpeedMps { get; set; } = float.NaN;
        public float DescentPitchDeg { get; set; } = float.NaN;
        public float AscentPitchDeg { get; set; } = float.NaN;
        public float DescentRollDeg { get; set; } = float.NaN;
        public float AscentRollDeg { get; set; } = float.NaN;
        public float DescentNetBuoyancyForceN { get; set; } = float.NaN;
        public float AscentNetBuoyancyForceN { get; set; } = float.NaN;
        public double OriginLongitudeDeg { get; set; } = 120.0;
        public double OriginLatitudeDeg { get; set; } = 25.0;
        public float WaterColumnDepthM { get; set; } = 520f;
        public OceanCurrentProfile OceanCurrentProfile { get; set; } = new OceanCurrentProfile();
        public OceanCurrentField OceanCurrentField { get; set; } = new OceanCurrentField();
        public OceanCurrentSourcePreference OceanCurrentSourcePreference { get; set; } = OceanCurrentSourcePreference.LayeredPreferred;
        public float OceanCurrentPrefetchHalfWidthKm { get; set; } = 25f;
        public float OceanCurrentForecastWindowHours { get; set; } = 72f;
        public float GliderClearanceRadiusM { get; set; } = 500f;
        public float TrajectorySafetyCorridorRadiusM { get; set; } = 750f;
        public float MinimumArrowSpeedMps { get; set; } = 0.02f;
        public float MinimumStreamlineSpeedMps { get; set; } = 0.05f;
        public float IrregularFieldIdwRadiusKm { get; set; } = 50f;
        public int CandidateDepthLayerCount { get; set; } = 12;
        public int CandidateHorizontalColumnCount { get; set; } = 12;
        public int CandidateHorizontalRowCount { get; set; } = 10;
        public GliderDynamicsProfile Dynamics { get; set; } = GliderDynamicsProfile.Default;

        public static SimulationProfile Default => new SimulationProfile();

        public SimulationProfile Clone()
        {
            return new SimulationProfile
            {
                CycleCount = CycleCount,
                CycleDurationSeconds = CycleDurationSeconds,
                SampleIntervalSeconds = SampleIntervalSeconds,
                TargetDepthM = TargetDepthM,
                HorizontalSpeedMps = HorizontalSpeedMps,
                StartHeadingDeg = StartHeadingDeg,
                HeadingDeltaPerCycleDeg = HeadingDeltaPerCycleDeg,
                PitchAmplitudeDeg = PitchAmplitudeDeg,
                RollAmplitudeDeg = RollAmplitudeDeg,
                DescentSpeedMps = DescentSpeedMps,
                AscentSpeedMps = AscentSpeedMps,
                DescentPitchDeg = DescentPitchDeg,
                AscentPitchDeg = AscentPitchDeg,
                DescentRollDeg = DescentRollDeg,
                AscentRollDeg = AscentRollDeg,
                DescentNetBuoyancyForceN = DescentNetBuoyancyForceN,
                AscentNetBuoyancyForceN = AscentNetBuoyancyForceN,
                OriginLongitudeDeg = OriginLongitudeDeg,
                OriginLatitudeDeg = OriginLatitudeDeg,
                WaterColumnDepthM = WaterColumnDepthM,
                OceanCurrentProfile = OceanCurrentProfile?.Clone() ?? new OceanCurrentProfile(),
                OceanCurrentField = OceanCurrentField?.Clone() ?? new OceanCurrentField(),
                OceanCurrentSourcePreference = OceanCurrentSourcePreference,
                OceanCurrentPrefetchHalfWidthKm = OceanCurrentPrefetchHalfWidthKm,
                OceanCurrentForecastWindowHours = OceanCurrentForecastWindowHours,
                GliderClearanceRadiusM = GliderClearanceRadiusM,
                TrajectorySafetyCorridorRadiusM = TrajectorySafetyCorridorRadiusM,
                MinimumArrowSpeedMps = MinimumArrowSpeedMps,
                MinimumStreamlineSpeedMps = MinimumStreamlineSpeedMps,
                IrregularFieldIdwRadiusKm = IrregularFieldIdwRadiusKm,
                CandidateDepthLayerCount = CandidateDepthLayerCount,
                CandidateHorizontalColumnCount = CandidateHorizontalColumnCount,
                CandidateHorizontalRowCount = CandidateHorizontalRowCount,
                Dynamics = Dynamics?.Clone() ?? GliderDynamicsProfile.Default
            };
        }

        public float ResolveDescentSpeedMps()
        {
            return ResolvePositive(DescentSpeedMps, HorizontalSpeedMps);
        }

        public float ResolveAscentSpeedMps()
        {
            return ResolvePositive(AscentSpeedMps, HorizontalSpeedMps);
        }

        public float ResolveDescentPitchDeg()
        {
            return ResolveMagnitude(DescentPitchDeg, PitchAmplitudeDeg);
        }

        public float ResolveAscentPitchDeg()
        {
            return ResolveMagnitude(AscentPitchDeg, PitchAmplitudeDeg);
        }

        public float ResolveDescentRollDeg()
        {
            return ResolveSigned(DescentRollDeg, RollAmplitudeDeg);
        }

        public float ResolveAscentRollDeg()
        {
            return ResolveSigned(AscentRollDeg, RollAmplitudeDeg);
        }

        public bool HasDirectionalBuoyancyCommands()
        {
            return IsFinite(DescentNetBuoyancyForceN) || IsFinite(AscentNetBuoyancyForceN);
        }

        public float ResolveDescentNetBuoyancyForceN(float fallbackForceN)
        {
            return ResolveSigned(DescentNetBuoyancyForceN, fallbackForceN);
        }

        public float ResolveAscentNetBuoyancyForceN(float fallbackForceN)
        {
            return ResolveSigned(AscentNetBuoyancyForceN, fallbackForceN);
        }

        private static float ResolvePositive(float value, float fallback)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f
                ? value
                : Mathf.Max(0f, fallback);
        }

        private static float ResolveMagnitude(float value, float fallback)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Abs(value)
                : Mathf.Abs(fallback);
        }

        private static float ResolveSigned(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
