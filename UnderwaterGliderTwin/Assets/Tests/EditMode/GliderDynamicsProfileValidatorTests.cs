using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class GliderDynamicsProfileValidatorTests
    {
        [Test]
        public void InvalidNonlinearParametersAreRejected()
        {
            var profile = GliderDynamicsProfile.Default;
            profile.BuoyancyCurveExponent = 4f;

            Assert.That(GliderDynamicsProfileValidator.TryValidate(profile, out var error), Is.False);
            Assert.That(error, Does.Contain("BuoyancyCurveExponent"));
        }

        [Test]
        public void HysteresisLargerThanDeadbandIsRejectedWithoutChangingProfile()
        {
            var profile = GliderDynamicsProfile.Default;
            profile.BuoyancyDeadbandFraction = 0.04f;
            profile.PistonHysteresisFraction = 0.06f;

            Assert.That(GliderDynamicsProfileValidator.TryValidate(profile, out var error), Is.False);
            Assert.That(error, Does.Contain("PistonHysteresisFraction"));
            Assert.That(profile.PistonHysteresisFraction, Is.EqualTo(0.06f));
        }

        [Test]
        public void NonFiniteMomentLimitIsRejected()
        {
            var profile = GliderDynamicsProfile.Default;
            profile.MaxRollMomentNm = float.PositiveInfinity;

            Assert.That(GliderDynamicsProfileValidator.TryValidate(profile, out var error), Is.False);
            Assert.That(error, Does.Contain("MaxRollMomentNm"));
        }
    }
}
