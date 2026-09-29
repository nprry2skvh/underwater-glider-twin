using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public struct WaterWaveSample
    {
        public WaterWaveSample(float heightM, Vector3 normal)
        {
            HeightM = heightM;
            Normal = normal;
        }

        public float HeightM { get; }
        public Vector3 Normal { get; }
    }

    public static class WaterWaveMath
    {
        private const float GravityMps2 = 9.81f;
        private const float TwoPi = Mathf.PI * 2f;

        public static WaterWaveSample Evaluate(WaterSurfaceSettings settings, Vector2 worldXZ, float simulationTimeSeconds)
        {
            var validated = settings == null ? WaterSurfaceSettings.CreateDefault() : settings;
            var scaledTime = simulationTimeSeconds * validated.TimeMultiplier;
            var height = validated.SurfaceHeightM;
            var derivativeX = 0f;
            var derivativeZ = 0f;

            Accumulate(validated.LongWave, worldXZ, scaledTime, ref height, ref derivativeX, ref derivativeZ);
            Accumulate(validated.CrossWave, worldXZ, scaledTime, ref height, ref derivativeX, ref derivativeZ);
            Accumulate(validated.MediumWave, worldXZ, scaledTime, ref height, ref derivativeX, ref derivativeZ);
            Accumulate(validated.DetailWave, worldXZ, scaledTime, ref height, ref derivativeX, ref derivativeZ);
            Accumulate(validated.FineWave, worldXZ, scaledTime, ref height, ref derivativeX, ref derivativeZ);

            var normal = new Vector3(-derivativeX, 1f, -derivativeZ).normalized;
            return new WaterWaveSample(height, normal);
        }

        public static float AngularFrequency(float wavelengthM)
        {
            var safeWavelength = Mathf.Max(0.25f, wavelengthM);
            var waveNumber = TwoPi / safeWavelength;
            return Mathf.Sqrt(GravityMps2 * waveNumber);
        }

        private static void Accumulate(
            WaterWaveBand wave,
            Vector2 worldXZ,
            float scaledTime,
            ref float height,
            ref float derivativeX,
            ref float derivativeZ)
        {
            if (wave.AmplitudeM <= 0f)
            {
                return;
            }

            var wavelength = Mathf.Max(0.25f, wave.WavelengthM);
            var waveNumber = TwoPi / wavelength;
            var direction = wave.Direction;
            var phase = waveNumber * Vector2.Dot(direction, worldXZ)
                - AngularFrequency(wavelength) * scaledTime
                + wave.PhaseRadians;
            var sine = Mathf.Sin(phase);
            var slope = wave.AmplitudeM * waveNumber * Mathf.Cos(phase);

            height += wave.AmplitudeM * sine;
            derivativeX += slope * direction.x;
            derivativeZ += slope * direction.y;
        }
    }
}
