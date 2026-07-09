using NUnit.Framework;
using UnderwaterGliderTwin.Logging;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class AlarmEvaluatorTests
    {
        [Test]
        public void Evaluate_FlagsDepthBatteryAndAttitude()
        {
            var evaluator = new AlarmEvaluator(maxDepthM: 1000f, minBatteryPercent: 20f, maxAbsAttitudeDeg: 20f);
            var frame = new TelemetryFrame(0, "t", 120, 25, 1201f, 100f, 0f, 22f, -21f, 28f, 0f, 9f, "潜航", "潜航", 1f, 0f, 1000f, 100f, 0f, 0f, 0f);

            var alarm = evaluator.Evaluate(frame);

            Assert.That(alarm.HasAny, Is.True);
            Assert.That(alarm.DepthExceeded, Is.True);
            Assert.That(alarm.BatteryLow, Is.True);
            Assert.That(alarm.AttitudeExceeded, Is.True);
            Assert.That(alarm.Message, Does.Contain("depth"));
            Assert.That(alarm.Message, Does.Contain("battery"));
            Assert.That(alarm.Message, Does.Contain("attitude"));
        }
    }
}
