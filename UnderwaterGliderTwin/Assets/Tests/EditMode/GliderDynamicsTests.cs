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

            var clone = profile.Clone();
            clone.Dynamics.CurrentSideSlipGain = 4f;

            Assert.That(profile.Dynamics.CurrentSideSlipGain, Is.EqualTo(1.5f));
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
    }
}
