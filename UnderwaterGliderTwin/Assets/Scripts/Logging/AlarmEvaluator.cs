using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Logging
{
    public sealed class AlarmEvaluator
    {
        private readonly float maxDepthM;
        private readonly float minBatteryPercent;
        private readonly float maxAbsAttitudeDeg;

        public AlarmEvaluator(float maxDepthM, float minBatteryPercent, float maxAbsAttitudeDeg)
        {
            this.maxDepthM = maxDepthM;
            this.minBatteryPercent = minBatteryPercent;
            this.maxAbsAttitudeDeg = maxAbsAttitudeDeg;
        }

        public AlarmState Evaluate(TelemetryFrame frame)
        {
            var depthExceeded = frame.DepthM > maxDepthM;
            var batteryLow = frame.BatteryPercent < minBatteryPercent;
            var attitudeExceeded = System.Math.Abs(frame.PitchDeg) > maxAbsAttitudeDeg || System.Math.Abs(frame.RollDeg) > maxAbsAttitudeDeg;
            var messages = new List<string>();

            if (depthExceeded)
            {
                messages.Add($"深度 {frame.DepthM:0.0} > {maxDepthM:0.0}");
            }

            if (batteryLow)
            {
                messages.Add($"电量 {frame.BatteryPercent:0.0} < {minBatteryPercent:0.0}");
            }

            if (attitudeExceeded)
            {
                messages.Add($"姿态 俯仰 {frame.PitchDeg:0.0} 横滚 {frame.RollDeg:0.0}");
            }

            return new AlarmState(depthExceeded, batteryLow, attitudeExceeded, string.Join("; ", messages));
        }
    }
}
