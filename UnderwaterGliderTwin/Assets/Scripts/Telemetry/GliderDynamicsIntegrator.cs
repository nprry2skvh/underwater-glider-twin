using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class GliderDynamicsIntegrator
    {
        public static GliderDynamicsState Step(
            GliderDynamicsState state,
            GliderDynamicsProfile settings,
            Vector3 currentEndMps,
            float targetDepthM,
            float targetHeadingDeg,
            float deltaSeconds,
            float commandedRollDeg = 0f,
            float commandedPitchDeg = float.NaN,
            float commandedNetBuoyancyForceN = float.NaN)
        {
            if (deltaSeconds <= 0f)
            {
                return state;
            }

            if (deltaSeconds > 1f)
            {
                var remainingSeconds = deltaSeconds;
                while (remainingSeconds > 0f)
                {
                    var substepSeconds = Mathf.Min(1f, remainingSeconds);
                    state = Step(
                        state,
                        settings,
                        currentEndMps,
                        targetDepthM,
                        targetHeadingDeg,
                        substepSeconds,
                        commandedRollDeg,
                        commandedPitchDeg,
                        commandedNetBuoyancyForceN);
                    remainingSeconds -= substepSeconds;
                }

                return state;
            }

            settings ??= GliderDynamicsProfile.Default;
            var mass = Mathf.Max(0.1f, settings.MassKg);
            if (state.EarthVelocityEndMps.sqrMagnitude < 0.000001f
                && state.WaterVelocityEndMps.sqrMagnitude > 0.000001f)
            {
                // Preserve compatibility for callers that still initialize only water-relative velocity.
                state.EarthVelocityEndMps = state.WaterVelocityEndMps + currentEndMps;
            }

            var forward = ComputeForward(state.HeadingDeg, state.PitchDeg);
            var right = ComputeRight(state.HeadingDeg);
            var down = Vector3.Cross(forward, right).normalized;
            right = Quaternion.AngleAxis(state.RollDeg, forward) * right;
            down = Vector3.Cross(forward, right).normalized;

            var depthError = targetDepthM - state.PositionEndM.y;
            var desiredBuoyancy = float.IsNaN(commandedNetBuoyancyForceN)
                ? -depthError * 0.45f
                : commandedNetBuoyancyForceN;
            desiredBuoyancy = Mathf.Clamp(desiredBuoyancy, -settings.MaxBuoyancyForceN, settings.MaxBuoyancyForceN);
            var halfPistonStroke = Mathf.Max(0.1f, settings.PistonStrokeMm * 0.5f);
            var desiredPistonPosition = desiredBuoyancy / Mathf.Max(0.1f, settings.MaxBuoyancyForceN) * halfPistonStroke;
            var previousPistonPosition = state.PistonPositionMm;
            state.PistonPositionMm = Mathf.MoveTowards(
                state.PistonPositionMm,
                desiredPistonPosition,
                halfPistonStroke / Mathf.Max(0.1f, settings.PistonResponseSeconds) * deltaSeconds);
            state.NetBuoyancyForceN = state.PistonPositionMm / halfPistonStroke * settings.MaxBuoyancyForceN;

            var relativeWaterVelocity = state.EarthVelocityEndMps - currentEndMps;
            var bodyForwardSpeed = Vector3.Dot(relativeWaterVelocity, forward);
            var bodySideSpeed = Vector3.Dot(relativeWaterVelocity, right);
            var bodyDownSpeed = Vector3.Dot(relativeWaterVelocity, down);
            var waterSpeed = relativeWaterVelocity.magnitude;
            var safeForwardSpeed = Mathf.Max(0.1f, Mathf.Abs(bodyForwardSpeed));
            var angleOfAttackRad = Mathf.Atan2(bodyDownSpeed, safeForwardSpeed);
            var sideslipRad = Mathf.Atan2(bodySideSpeed, safeForwardSpeed);
            var dynamicPressure = 0.5f * Mathf.Max(1f, settings.WaterDensityKgPerM3) * waterSpeed * waterSpeed;
            var liftCoefficient = Mathf.Clamp(
                settings.LiftCoefficient + settings.LiftSlopePerRad * angleOfAttackRad,
                -1.2f,
                1.2f);
            var liftForce = dynamicPressure * Mathf.Max(0.01f, settings.ReferenceAreaM2) * liftCoefficient;
            var dragCoefficient = Mathf.Max(0f, settings.BaseDragCoefficient)
                + Mathf.Max(0f, settings.InducedDragCoefficient) * liftCoefficient * liftCoefficient;
            var dragForce = dynamicPressure * Mathf.Max(0.01f, settings.ReferenceAreaM2) * dragCoefficient;
            var sideForce = -dynamicPressure
                * Mathf.Max(0.01f, settings.ReferenceAreaM2)
                * settings.SideForceCoefficient
                * sideslipRad;

            var targetForwardSpeed = Mathf.Max(0f, settings.CruiseSpeedMps);
            var driveForce = Mathf.Clamp(
                (targetForwardSpeed - bodyForwardSpeed) * mass / Mathf.Max(1f, settings.SpeedResponseSeconds),
                -24f,
                24f);
            var dragDirection = waterSpeed > 0.001f ? -relativeWaterVelocity.normalized : Vector3.zero;
            var forceWorld = forward * driveForce
                + dragDirection * dragForce
                + right * sideForce
                - down * liftForce
                - down * state.NetBuoyancyForceN;

            state.EarthVelocityEndMps += forceWorld / mass * deltaSeconds;
            state.EarthVelocityEndMps *= 1f / (1f + settings.BaseDragCoefficient * deltaSeconds * 0.05f);
            state.WaterVelocityEndMps = state.EarthVelocityEndMps - currentEndMps;
            state.PositionEndM += state.EarthVelocityEndMps * deltaSeconds;

            var depthDrivenPitch = Mathf.Clamp(depthError * 0.12f - state.NetBuoyancyForceN * 0.28f, -35f, 35f);
            var desiredPitch = float.IsNaN(commandedPitchDeg)
                ? depthDrivenPitch
                : Mathf.Clamp(commandedPitchDeg + Mathf.Clamp(depthError * 0.02f, -5f, 5f), -35f, 35f);
            // Relative water velocity drives hydrodynamic side force. The local current
            // component separately drives the navigation controller's ground-track correction.
            var lateralCurrent = Mathf.Abs(bodyForwardSpeed) > 0.1f
                ? Vector3.Dot(currentEndMps, right)
                : 0f;
            var desiredRoll = Mathf.Clamp(
                commandedRollDeg + lateralCurrent * settings.CurrentSideSlipGain * 12f,
                -30f,
                30f);
            var desiredHeading = targetHeadingDeg - lateralCurrent * settings.CurrentSideSlipGain * 8f;

            var rollErrorRad = Mathf.DeltaAngle(state.RollDeg, desiredRoll) * Mathf.Deg2Rad;
            var pitchErrorRad = Mathf.DeltaAngle(state.PitchDeg, desiredPitch) * Mathf.Deg2Rad;
            var yawErrorRad = Mathf.DeltaAngle(state.HeadingDeg, desiredHeading) * Mathf.Deg2Rad;
            var maxControlSurfaceDeflection = Mathf.Max(0.1f, settings.MaxControlSurfaceDeflectionDeg);
            var controlSurfaceRate = maxControlSurfaceDeflection / Mathf.Max(0.1f, settings.ControlSurfaceResponseSeconds);
            var desiredRollSurface = Mathf.Clamp(
                rollErrorRad * Mathf.Rad2Deg * settings.ControlSurfaceCommandGain,
                -maxControlSurfaceDeflection,
                maxControlSurfaceDeflection);
            var desiredPitchSurface = Mathf.Clamp(
                pitchErrorRad * Mathf.Rad2Deg * settings.ControlSurfaceCommandGain,
                -maxControlSurfaceDeflection,
                maxControlSurfaceDeflection);
            var desiredYawSurface = Mathf.Clamp(
                yawErrorRad * Mathf.Rad2Deg * settings.ControlSurfaceCommandGain,
                -maxControlSurfaceDeflection,
                maxControlSurfaceDeflection);
            var previousRollSurface = state.RollControlSurfaceDeflectionDeg;
            var previousPitchSurface = state.PitchControlSurfaceDeflectionDeg;
            var previousYawSurface = state.YawControlSurfaceDeflectionDeg;
            state.RollControlSurfaceDeflectionDeg = Mathf.MoveTowards(
                state.RollControlSurfaceDeflectionDeg, desiredRollSurface, controlSurfaceRate * deltaSeconds);
            state.PitchControlSurfaceDeflectionDeg = Mathf.MoveTowards(
                state.PitchControlSurfaceDeflectionDeg, desiredPitchSurface, controlSurfaceRate * deltaSeconds);
            state.YawControlSurfaceDeflectionDeg = Mathf.MoveTowards(
                state.YawControlSurfaceDeflectionDeg, desiredYawSurface, controlSurfaceRate * deltaSeconds);
            var rollRateRad = state.RollRateDegPerSecond * Mathf.Deg2Rad;
            var pitchRateRad = state.PitchRateDegPerSecond * Mathf.Deg2Rad;
            var yawRateRad = state.YawRateDegPerSecond * Mathf.Deg2Rad;
            var rollHydrodynamicMoment = -sideForce * Mathf.Max(0.1f, settings.WingSpanM) * 0.35f;
            var pitchHydrodynamicMoment = liftForce * Mathf.Max(0.1f, settings.MeanChordM) * 0.25f;
            var yawHydrodynamicMoment = sideForce * Mathf.Max(0.1f, settings.ReferenceLengthM) * 0.2f;
            var dampingScale = Mathf.Max(0f, settings.AngularDamping);
            var rollMoment = rollHydrodynamicMoment
                + settings.RollControlMomentNmPerRad * state.RollControlSurfaceDeflectionDeg * Mathf.Deg2Rad
                - dampingScale * Mathf.Max(0.1f, settings.RollInertiaKgM2) * rollRateRad;
            var pitchMoment = pitchHydrodynamicMoment
                + settings.PitchControlMomentNmPerRad * state.PitchControlSurfaceDeflectionDeg * Mathf.Deg2Rad
                - dampingScale * Mathf.Max(0.1f, settings.PitchInertiaKgM2) * pitchRateRad;
            var yawMoment = yawHydrodynamicMoment
                + settings.YawControlMomentNmPerRad * state.YawControlSurfaceDeflectionDeg * Mathf.Deg2Rad
                - dampingScale * Mathf.Max(0.1f, settings.YawInertiaKgM2) * yawRateRad;

            state.RollRateDegPerSecond += rollMoment / Mathf.Max(0.1f, settings.RollInertiaKgM2) * Mathf.Rad2Deg * deltaSeconds;
            state.PitchRateDegPerSecond += pitchMoment / Mathf.Max(0.1f, settings.PitchInertiaKgM2) * Mathf.Rad2Deg * deltaSeconds;
            state.YawRateDegPerSecond += yawMoment / Mathf.Max(0.1f, settings.YawInertiaKgM2) * Mathf.Rad2Deg * deltaSeconds;
            state.RollRateDegPerSecond = Mathf.Clamp(state.RollRateDegPerSecond, -settings.MaxRollRateDegPerSecond, settings.MaxRollRateDegPerSecond);
            state.PitchRateDegPerSecond = Mathf.Clamp(state.PitchRateDegPerSecond, -settings.MaxPitchRateDegPerSecond, settings.MaxPitchRateDegPerSecond);
            state.YawRateDegPerSecond = Mathf.Clamp(state.YawRateDegPerSecond, -settings.MaxYawRateDegPerSecond, settings.MaxYawRateDegPerSecond);
            state.RollDeg = Mathf.Clamp(state.RollDeg + state.RollRateDegPerSecond * deltaSeconds, -30f, 30f);
            state.PitchDeg = Mathf.Clamp(state.PitchDeg + state.PitchRateDegPerSecond * deltaSeconds, -35f, 35f);
            state.HeadingDeg = NormalizeHeading(state.HeadingDeg + state.YawRateDegPerSecond * deltaSeconds);
            state.AngularVelocityRadPerSecond = new Vector3(
                state.RollRateDegPerSecond * Mathf.Deg2Rad,
                state.PitchRateDegPerSecond * Mathf.Deg2Rad,
                state.YawRateDegPerSecond * Mathf.Deg2Rad);
            state.AngleOfAttackDeg = Mathf.Clamp(angleOfAttackRad * Mathf.Rad2Deg, -settings.MaxAngleOfAttackDeg, settings.MaxAngleOfAttackDeg);
            state.SideslipDeg = sideslipRad * Mathf.Rad2Deg;
            state.LiftForceN = liftForce;
            state.DragForceN = dragForce;
            state.SideForceN = sideForce;
            state.HydrodynamicMomentNm = new Vector3(
                rollHydrodynamicMoment,
                pitchHydrodynamicMoment,
                yawHydrodynamicMoment);

            var pistonRateMmPerSecond = Mathf.Abs(state.PistonPositionMm - previousPistonPosition) / deltaSeconds;
            var controlSurfaceRateDegPerSecond = (
                Mathf.Abs(state.RollControlSurfaceDeflectionDeg - previousRollSurface)
                + Mathf.Abs(state.PitchControlSurfaceDeflectionDeg - previousPitchSurface)
                + Mathf.Abs(state.YawControlSurfaceDeflectionDeg - previousYawSurface)) / deltaSeconds;
            state.ActuatorPowerWatts = pistonRateMmPerSecond * Mathf.Max(0f, settings.PistonPowerWattsPerMm)
                + controlSurfaceRateDegPerSecond * Mathf.Max(0f, settings.ControlSurfacePowerWattsPerDeg);
            var powerWatts = settings.BasePowerWatts
                + Mathf.Abs(state.NetBuoyancyForceN) * settings.BuoyancyPowerWattsPerNewton
                + dragForce * 0.05f
                + state.ActuatorPowerWatts;
            state.BatteryPercent = Mathf.Max(
                0f,
                state.BatteryPercent - powerWatts * deltaSeconds / Mathf.Max(1f, settings.BatteryCapacityWh * 36f));
            return state;
        }

        private static Vector3 ComputeForward(float headingDeg, float pitchDeg)
        {
            var headingRadians = headingDeg * Mathf.Deg2Rad;
            var pitchRadians = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(headingRadians) * Mathf.Cos(pitchRadians),
                Mathf.Sin(pitchRadians),
                Mathf.Cos(headingRadians) * Mathf.Cos(pitchRadians)).normalized;
        }

        private static Vector3 ComputeRight(float headingDeg)
        {
            var headingRadians = headingDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(headingRadians), 0f, -Mathf.Sin(headingRadians)).normalized;
        }

        private static float NormalizeHeading(float headingDeg)
        {
            var normalized = headingDeg % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }
    }
}
