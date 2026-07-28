using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class GliderDynamicsTests
    {
        [Test]
        public void ProfileClone_DeepCopiesDynamicsSettings()
        {
            var profile = SimulationProfile.Default;
            profile.Dynamics.CurrentSideSlipGain = 1.5f;
            profile.Dynamics.BuoyancyCurveExponent = 2f;
            profile.Dynamics.MaxRollMomentNm = 7f;

            var clone = profile.Clone();
            clone.Dynamics.CurrentSideSlipGain = 4f;
            clone.Dynamics.BuoyancyCurveExponent = 3f;
            clone.Dynamics.MaxRollMomentNm = 2f;

            Assert.That(profile.Dynamics.CurrentSideSlipGain, Is.EqualTo(1.5f));
            Assert.That(profile.Dynamics.BuoyancyCurveExponent, Is.EqualTo(2f));
            Assert.That(profile.Dynamics.MaxRollMomentNm, Is.EqualTo(7f));
        }

        [Test]
        public void Step_CrossCurrentProducesSideSlipAndRollResponse()
        {
            var state = GliderDynamicsState.AtSurface();
            state.EarthVelocityEndMps = new Vector3(1f, 0f, 0.8f);
            var settings = GliderDynamicsProfile.Default;
            settings.TurbulenceMps = 0f;

            var next = GliderDynamicsIntegrator.Step(state, settings, new Vector3(1f, 0f, 0f), 20f, 0f, 10f);

            Assert.That(next.PositionEndM.x, Is.GreaterThan(0f));
            Assert.That(next.RollDeg, Is.GreaterThan(0f));
        }

        [Test]
        public void Step_ComputesHydrodynamicLoadsAndThreeAxisAngularVelocity()
        {
            var state = GliderDynamicsState.AtSurface();
            state.EarthVelocityEndMps = new Vector3(0f, 0f, 0.8f);
            var settings = GliderDynamicsProfile.Default;

            var next = GliderDynamicsIntegrator.Step(
                state,
                settings,
                Vector3.zero,
                targetDepthM: 40f,
                targetHeadingDeg: 15f,
                deltaSeconds: 0.5f,
                commandedRollDeg: 8f,
                commandedPitchDeg: 12f);

            Assert.That(next.DragForceN, Is.GreaterThan(0f));
            Assert.That(next.LiftForceN, Is.Not.NaN);
            Assert.That(next.AngleOfAttackDeg, Is.InRange(-settings.MaxAngleOfAttackDeg, settings.MaxAngleOfAttackDeg));
            Assert.That(next.AngularVelocityRadPerSecond.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(next.HydrodynamicMomentNm.sqrMagnitude, Is.GreaterThan(0f));
        }

        [Test]
        public void Step_ComputesRelativeWaterVelocityFromEarthVelocityAndCurrent()
        {
            var state = GliderDynamicsState.AtSurface();
            state.EarthVelocityEndMps = new Vector3(0f, 0f, 0.8f);
            var settings = GliderDynamicsProfile.Default;

            var next = GliderDynamicsIntegrator.Step(
                state,
                settings,
                new Vector3(0.4f, 0f, 0f),
                targetDepthM: 20f,
                targetHeadingDeg: 0f,
                deltaSeconds: 0.01f);

            Assert.That(next.WaterVelocityEndMps.x, Is.LessThan(-0.3f));
            Assert.That(next.SideslipDeg, Is.LessThan(-15f));
            Assert.That(next.PositionEndM.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Step_RateLimitsActuatorsAndAddsActuatorPower()
        {
            var state = GliderDynamicsState.AtSurface();
            state.EarthVelocityEndMps = new Vector3(0f, 0f, 0.8f);
            var settings = GliderDynamicsProfile.Default;

            var next = GliderDynamicsIntegrator.Step(
                state,
                settings,
                Vector3.zero,
                targetDepthM: 80f,
                targetHeadingDeg: 30f,
                deltaSeconds: 0.5f,
                commandedRollDeg: 20f,
                commandedPitchDeg: 15f);

            Assert.That(Mathf.Abs(next.PistonPositionMm), Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(next.PistonPositionMm), Is.LessThan(settings.PistonStrokeMm * 0.5f));
            Assert.That(Mathf.Abs(next.RollControlSurfaceDeflectionDeg), Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(next.PitchControlSurfaceDeflectionDeg), Is.GreaterThan(0f));
            Assert.That(next.ActuatorPowerWatts, Is.GreaterThan(0f));
            Assert.That(next.NetBuoyancyForceN, Is.EqualTo(
                next.PistonPositionMm / (settings.PistonStrokeMm * 0.5f) * settings.MaxBuoyancyForceN).Within(0.001f));
        }

        [Test]
        public void Step_UsesPositiveNetBuoyancyAsAnUpwardForce()
        {
            var state = GliderDynamicsState.AtSurface();
            var settings = GliderDynamicsProfile.Default;

            var next = GliderDynamicsIntegrator.Step(
                state,
                settings,
                Vector3.zero,
                targetDepthM: 0f,
                targetHeadingDeg: 0f,
                deltaSeconds: 0.5f,
                commandedNetBuoyancyForceN: 12f);

            Assert.That(next.NetBuoyancyForceN, Is.GreaterThan(0f));
            Assert.That(next.EarthVelocityEndMps.y, Is.LessThan(0f));
        }

        [Test]
        public void LinearDefaultsRemainEquivalent()
        {
            var settings = GliderDynamicsProfile.Default;
            settings.BuoyancyCurveExponent = 1f;
            settings.BuoyancyDeadbandFraction = 0f;
            settings.PistonHysteresisFraction = 0f;
            settings.RollCurveExponent = 1f;
            settings.RollDeadbandFraction = 0f;

            Assert.That(Integrate(settings), Is.EqualTo(IntegrateLegacy(settings)).Within(0.0001f));
        }

        [Test]
        public void Step_BoundsMonotonicBuoyancyForIncreasingCommands()
        {
            var settings = GliderDynamicsProfile.Default;
            settings.BuoyancyCurveExponent = 2f;
            settings.BuoyancyDeadbandFraction = 0.1f;
            var state = GliderDynamicsState.AtSurface();

            var low = GliderDynamicsIntegrator.Step(state, settings, Vector3.zero, 0f, 0f, 1f, commandedNetBuoyancyForceN: 4f);
            var high = GliderDynamicsIntegrator.Step(state, settings, Vector3.zero, 0f, 0f, 1f, commandedNetBuoyancyForceN: 12f);

            Assert.That(low.NetBuoyancyForceN, Is.InRange(0f, settings.MaxBuoyancyForceN));
            Assert.That(high.NetBuoyancyForceN, Is.InRange(0f, settings.MaxBuoyancyForceN));
            Assert.That(high.NetBuoyancyForceN, Is.GreaterThan(low.NetBuoyancyForceN));
        }

        [Test]
        public void Step_AppliesPistonHysteresisWhenCommandReversesInsideBand()
        {
            var settings = GliderDynamicsProfile.Default;
            settings.BuoyancyDeadbandFraction = 0.1f;
            settings.PistonHysteresisFraction = 0.05f;
            settings.PistonResponseSeconds = 0.1f;
            var state = GliderDynamicsState.AtSurface();

            state = GliderDynamicsIntegrator.Step(state, settings, Vector3.zero, 0f, 0f, 1f, commandedNetBuoyancyForceN: 8f);
            var reversed = GliderDynamicsIntegrator.Step(state, settings, Vector3.zero, 0f, 0f, 1f, commandedNetBuoyancyForceN: 7.8f);

            Assert.That(reversed.PistonPositionMm, Is.EqualTo(state.PistonPositionMm).Within(0.0001f));
        }

        [Test]
        public void Step_UsesNonlinearBoundedRollRestoringMomentAtLargeDeflection()
        {
            var settings = GliderDynamicsProfile.Default;
            settings.RollCurveExponent = 2f;
            settings.NonlinearRollRestoringGain = 15f;
            settings.MaxRollMomentNm = 4f;
            var state = GliderDynamicsState.AtSurface();
            state.RollDeg = 25f;

            var next = GliderDynamicsIntegrator.Step(state, settings, Vector3.zero, 0f, 0f, 0.5f);

            Assert.That(next.RollRateDegPerSecond, Is.LessThan(0f));
        }

        [Test]
        public void Step_RemainsFiniteAtExtremeValidNonlinearSettings()
        {
            var settings = GliderDynamicsProfile.Default;
            settings.BuoyancyCurveExponent = 3f;
            settings.BuoyancyDeadbandFraction = 0.2f;
            settings.PistonHysteresisFraction = 0.1f;
            settings.PistonResponseSeconds = 0.1f;
            settings.BuoyancyResponseSeconds = 120f;
            settings.RollCurveExponent = 3f;
            settings.RollDeadbandFraction = 0.25f;
            settings.NonlinearRollRestoringGain = 100f;
            settings.MaxRollMomentNm = 0f;
            var state = GliderDynamicsState.AtSurface();
            state.RollDeg = 30f;

            var next = GliderDynamicsIntegrator.Step(state, settings, new Vector3(100f, -100f, 100f), 100000f, 180f, 1f, 30f, 35f, 16f);

            Assert.That(float.IsNaN(next.NetBuoyancyForceN) || float.IsInfinity(next.NetBuoyancyForceN), Is.False);
            Assert.That(float.IsNaN(next.RollRateDegPerSecond) || float.IsInfinity(next.RollRateDegPerSecond), Is.False);
            Assert.That(float.IsNaN(next.HydrodynamicMomentNm.x) || float.IsInfinity(next.HydrodynamicMomentNm.x), Is.False);
        }

        [Test]
        public void ProfileClone_PreservesNonlinearDynamicsSettings()
        {
            var profile = GliderDynamicsProfile.Default;
            profile.BuoyancyCurveExponent = 2f;
            profile.BuoyancyDeadbandFraction = 0.12f;
            profile.PistonHysteresisFraction = 0.06f;
            profile.RollCurveExponent = 2.5f;
            profile.RollDeadbandFraction = 0.15f;
            profile.NonlinearRollRestoringGain = 3f;
            profile.MaxRollMomentNm = 7f;

            var clone = profile.Clone();

            Assert.That(clone.BuoyancyCurveExponent, Is.EqualTo(2f));
            Assert.That(clone.BuoyancyDeadbandFraction, Is.EqualTo(0.12f));
            Assert.That(clone.PistonHysteresisFraction, Is.EqualTo(0.06f));
            Assert.That(clone.RollCurveExponent, Is.EqualTo(2.5f));
            Assert.That(clone.RollDeadbandFraction, Is.EqualTo(0.15f));
            Assert.That(clone.NonlinearRollRestoringGain, Is.EqualTo(3f));
            Assert.That(clone.MaxRollMomentNm, Is.EqualTo(7f));
        }

        private static float Integrate(GliderDynamicsProfile settings)
        {
            var state = GliderDynamicsState.AtSurface();
            return GliderDynamicsIntegrator.Step(state, settings, Vector3.zero, 0f, 0f, 0.5f,
                commandedNetBuoyancyForceN: settings.MaxBuoyancyForceN).NetBuoyancyForceN;
        }

        private static float IntegrateLegacy(GliderDynamicsProfile settings)
        {
            var halfPistonStroke = Mathf.Max(0.1f, settings.PistonStrokeMm * 0.5f);
            var desiredPistonPosition = halfPistonStroke;
            var pistonPosition = Mathf.MoveTowards(0f, desiredPistonPosition,
                halfPistonStroke / Mathf.Max(0.1f, settings.PistonResponseSeconds) * 0.5f);
            return pistonPosition / halfPistonStroke * settings.MaxBuoyancyForceN;
        }
    }
}
