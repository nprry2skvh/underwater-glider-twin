namespace UnderwaterGliderTwin.Logging
{
    public readonly struct AlarmState
    {
        public readonly bool DepthExceeded;
        public readonly bool BatteryLow;
        public readonly bool AttitudeExceeded;
        public readonly string Message;

        public bool HasAny => DepthExceeded || BatteryLow || AttitudeExceeded;

        public AlarmState(bool depthExceeded, bool batteryLow, bool attitudeExceeded, string message)
        {
            DepthExceeded = depthExceeded;
            BatteryLow = batteryLow;
            AttitudeExceeded = attitudeExceeded;
            Message = message;
        }
    }
}
