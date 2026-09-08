namespace UnderwaterGliderTwin.Telemetry
{
    public static class GliderDynamicsProfileValidator
    {
        public static bool TryValidate(GliderDynamicsProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "GliderDynamicsProfile is required.";
                return false;
            }

            if (!IsInRange(profile.BuoyancyResponseSeconds, 0.1f, 120f)) return Fail("BuoyancyResponseSeconds", out error);
            if (!IsInRange(profile.PistonResponseSeconds, 0.1f, 120f)) return Fail("PistonResponseSeconds", out error);
            if (!IsInRange(profile.BuoyancyCurveExponent, 0.5f, 3f)) return Fail("BuoyancyCurveExponent", out error);
            if (!IsInRange(profile.BuoyancyDeadbandFraction, 0f, 0.2f)) return Fail("BuoyancyDeadbandFraction", out error);
            if (!IsInRange(profile.PistonHysteresisFraction, 0f, 0.1f)
                || profile.PistonHysteresisFraction > profile.BuoyancyDeadbandFraction) return Fail("PistonHysteresisFraction", out error);
            if (!IsInRange(profile.RollCurveExponent, 0.5f, 3f)) return Fail("RollCurveExponent", out error);
            if (!IsInRange(profile.RollDeadbandFraction, 0f, 0.25f)) return Fail("RollDeadbandFraction", out error);
            if (!IsFiniteNonNegative(profile.NonlinearRollRestoringGain)) return Fail("NonlinearRollRestoringGain", out error);
            if (!IsFiniteNonNegative(profile.MaxRollMomentNm)) return Fail("MaxRollMomentNm", out error);
            if (!IsInRange(profile.IntegrationStepSeconds, 0.01f, 1f)) return Fail("IntegrationStepSeconds", out error);

            error = null;
            return true;
        }

        private static bool IsInRange(float value, float minimum, float maximum)
        {
            return IsFinite(value) && value >= minimum && value <= maximum;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(string fieldName, out string error)
        {
            error = fieldName + " is outside its supported range.";
            return false;
        }
    }
}
