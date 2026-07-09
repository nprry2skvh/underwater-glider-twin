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
                messages.Add($"depth {frame.DepthM:0.0}m > {maxDepthM:0.0}m");
            }

            if (batteryLow)
            {
                messages.Add($"battery {frame.BatteryPercent:0.0}% < {minBatteryPercent:0.0}%");
            }

            if (attitudeExceeded)
            {
                messages.Add($"attitude pitch {frame.PitchDeg:0.0} roll {frame.RollDeg:0.0}");
            }

            return new AlarmState(depthExceeded, batteryLow, attitudeExceeded, string.Join("; ", messages));
        }
    }
}
