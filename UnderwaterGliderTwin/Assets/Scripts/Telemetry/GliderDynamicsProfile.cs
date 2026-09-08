namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class GliderDynamicsProfile
    {
        public string PresetName { get; set; } = "Sea Trial Approximation";
        public float MassKg { get; set; } = 52f;
        public float ReferenceAreaM2 { get; set; } = 0.22f;
        public float ReferenceLengthM { get; set; } = 4.6f;
        public float WingSpanM { get; set; } = 2.4f;
        public float MeanChordM { get; set; } = 0.32f;
        public float RollInertiaKgM2 { get; set; } = 28f;
        public float PitchInertiaKgM2 { get; set; } = 42f;
        public float YawInertiaKgM2 { get; set; } = 54f;
        public float WaterDensityKgPerM3 { get; set; } = 1025f;
        public float BaseDragCoefficient { get; set; } = 0.32f;
        public float LiftCoefficient { get; set; } = 0.18f;
        public float LiftSlopePerRad { get; set; } = 2.8f;
        public float InducedDragCoefficient { get; set; } = 0.08f;
        public float MaxAngleOfAttackDeg { get; set; } = 16f;
        public float SideForceCoefficient { get; set; } = 0.28f;
        public float RollControlMomentNmPerRad { get; set; } = 10f;
        public float PitchControlMomentNmPerRad { get; set; } = 18f;
        public float YawControlMomentNmPerRad { get; set; } = 12f;
        public float CurrentSideSlipGain { get; set; } = 0.85f;
        public float PitchRestoringGain { get; set; } = 0.75f;
        public float RollRestoringGain { get; set; } = 1.1f;
        public float YawResponseGain { get; set; } = 0.42f;
        public float AngularDamping { get; set; } = 0.65f;
        public float MaxBuoyancyForceN { get; set; } = 16f;
        public float BuoyancyResponseSeconds { get; set; } = 18f;
        public float BuoyancyCurveExponent { get; set; } = 1f;
        public float BuoyancyDeadbandFraction { get; set; } = 0f;
        public float PistonStrokeMm { get; set; } = 44f;
        public float PistonResponseSeconds { get; set; } = 8f;
        public float PistonHysteresisFraction { get; set; } = 0f;
        public float TurnaroundDurationSeconds { get; set; } = 360f;
        public float PistonPowerWattsPerMm { get; set; } = 0.4f;
        public float MaxControlSurfaceDeflectionDeg { get; set; } = 20f;
        public float ControlSurfaceResponseSeconds { get; set; } = 3f;
        public float ControlSurfaceCommandGain { get; set; } = 1f;
        public float ControlSurfacePowerWattsPerDeg { get; set; } = 0.08f;
        public float RollCurveExponent { get; set; } = 1f;
        public float RollDeadbandFraction { get; set; } = 0f;
        public float NonlinearRollRestoringGain { get; set; } = 0f;
        public float MaxRollMomentNm { get; set; } = float.MaxValue;
        public float CruiseSpeedMps { get; set; } = 0.65f;
        public float SpeedResponseSeconds { get; set; } = 8f;
        public float MaxPitchRateDegPerSecond { get; set; } = 3f;
        public float MaxRollRateDegPerSecond { get; set; } = 5f;
        public float MaxYawRateDegPerSecond { get; set; } = 4f;
        public float TurbulenceMps { get; set; } = 0.02f;
        public float BasePowerWatts { get; set; } = 3.5f;
        public float BuoyancyPowerWattsPerNewton { get; set; } = 0.4f;
        public float BatteryCapacityWh { get; set; } = 850f;
        public float MinimumBatteryPercent { get; set; } = 15f;
        public float IntegrationStepSeconds { get; set; } = 0.5f;

        public static GliderDynamicsProfile Default => new GliderDynamicsProfile();

        public GliderDynamicsProfile Clone()
        {
            return new GliderDynamicsProfile
            {
                PresetName = PresetName,
                MassKg = MassKg,
                ReferenceAreaM2 = ReferenceAreaM2,
                ReferenceLengthM = ReferenceLengthM,
                WingSpanM = WingSpanM,
                MeanChordM = MeanChordM,
                RollInertiaKgM2 = RollInertiaKgM2,
                PitchInertiaKgM2 = PitchInertiaKgM2,
                YawInertiaKgM2 = YawInertiaKgM2,
                WaterDensityKgPerM3 = WaterDensityKgPerM3,
                BaseDragCoefficient = BaseDragCoefficient,
                LiftCoefficient = LiftCoefficient,
                LiftSlopePerRad = LiftSlopePerRad,
                InducedDragCoefficient = InducedDragCoefficient,
                MaxAngleOfAttackDeg = MaxAngleOfAttackDeg,
                SideForceCoefficient = SideForceCoefficient,
                RollControlMomentNmPerRad = RollControlMomentNmPerRad,
                PitchControlMomentNmPerRad = PitchControlMomentNmPerRad,
                YawControlMomentNmPerRad = YawControlMomentNmPerRad,
                CurrentSideSlipGain = CurrentSideSlipGain,
                PitchRestoringGain = PitchRestoringGain,
                RollRestoringGain = RollRestoringGain,
                YawResponseGain = YawResponseGain,
                AngularDamping = AngularDamping,
                MaxBuoyancyForceN = MaxBuoyancyForceN,
                BuoyancyResponseSeconds = BuoyancyResponseSeconds,
                BuoyancyCurveExponent = BuoyancyCurveExponent,
                BuoyancyDeadbandFraction = BuoyancyDeadbandFraction,
                PistonStrokeMm = PistonStrokeMm,
                PistonResponseSeconds = PistonResponseSeconds,
                PistonHysteresisFraction = PistonHysteresisFraction,
                TurnaroundDurationSeconds = TurnaroundDurationSeconds,
                PistonPowerWattsPerMm = PistonPowerWattsPerMm,
                MaxControlSurfaceDeflectionDeg = MaxControlSurfaceDeflectionDeg,
                ControlSurfaceResponseSeconds = ControlSurfaceResponseSeconds,
                ControlSurfaceCommandGain = ControlSurfaceCommandGain,
                ControlSurfacePowerWattsPerDeg = ControlSurfacePowerWattsPerDeg,
                RollCurveExponent = RollCurveExponent,
                RollDeadbandFraction = RollDeadbandFraction,
                NonlinearRollRestoringGain = NonlinearRollRestoringGain,
                MaxRollMomentNm = MaxRollMomentNm,
                CruiseSpeedMps = CruiseSpeedMps,
                SpeedResponseSeconds = SpeedResponseSeconds,
                MaxPitchRateDegPerSecond = MaxPitchRateDegPerSecond,
                MaxRollRateDegPerSecond = MaxRollRateDegPerSecond,
                MaxYawRateDegPerSecond = MaxYawRateDegPerSecond,
                TurbulenceMps = TurbulenceMps,
                BasePowerWatts = BasePowerWatts,
                BuoyancyPowerWattsPerNewton = BuoyancyPowerWattsPerNewton,
                BatteryCapacityWh = BatteryCapacityWh,
                MinimumBatteryPercent = MinimumBatteryPercent,
                IntegrationStepSeconds = IntegrationStepSeconds
            };
        }
    }
}
