namespace UnderwaterGliderTwin.Mapping
{
    public readonly struct AttitudeSettings
    {
        public static readonly AttitudeSettings Default = new AttitudeSettings(
            pitchScale: 1f,
            rollScale: 1f,
            maxAbsPitchDeg: 45f,
            maxAbsRollDeg: 75f);

        public AttitudeSettings(float pitchScale, float rollScale, float maxAbsPitchDeg, float maxAbsRollDeg)
        {
            PitchScale = pitchScale;
            RollScale = rollScale;
            MaxAbsPitchDeg = maxAbsPitchDeg;
            MaxAbsRollDeg = maxAbsRollDeg;
        }

        public float PitchScale { get; }
        public float RollScale { get; }
        public float MaxAbsPitchDeg { get; }
        public float MaxAbsRollDeg { get; }
    }
}
