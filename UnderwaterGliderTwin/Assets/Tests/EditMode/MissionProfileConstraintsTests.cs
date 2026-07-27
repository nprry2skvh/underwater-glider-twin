using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class MissionProfileConstraintsTests
    {
        [Test]
        public void ComputeRequiredGlideAngle_IdentifiesAnUnrealisticDeepShortCycle()
        {
            var angle = MissionProfileConstraints.ComputeRequiredGlideAngleDeg(1600f, 900f, 0.65f);

            Assert.That(angle, Is.GreaterThan(75f));
        }

        [Test]
        public void ComputeMinimumCycleDuration_PreservesAThirtyTwoDegreeGlideEnvelope()
        {
            var duration = MissionProfileConstraints.ComputeMinimumCycleDurationSeconds(1600f, 0.65f);

            Assert.That(duration, Is.GreaterThan(7800f));
            Assert.That(duration, Is.LessThan(8000f));
        }

        [Test]
        public void ApplyEngineeringMinimumCycleDuration_ExtendsAnUnrealisticCycle()
        {
            var duration = MissionProfileConstraints.ApplyEngineeringMinimumCycleDuration(900f, 1600f, 0.65f);

            Assert.That(duration, Is.EqualTo(32900f));
        }

        [Test]
        public void NormalizeEngineeringCycleDuration_RoundsAnAutoCorrectedCycleUpToTheNextMinute()
        {
            var duration = MissionProfileConstraints.NormalizeEngineeringCycleDuration(900f, 1600f, 0.65f);

            Assert.That(duration, Is.EqualTo(32940f));
        }
    }
}
