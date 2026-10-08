using System;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public enum WaterSurfaceQuality
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public struct WaterWaveBand
    {
        public float AmplitudeM;
        public float WavelengthM;
        public float DirectionDegrees;
        public float PhaseRadians;

        public WaterWaveBand(float amplitudeM, float wavelengthM, float directionDegrees, float phaseRadians)
        {
            AmplitudeM = amplitudeM;
            WavelengthM = wavelengthM;
            DirectionDegrees = directionDegrees;
            PhaseRadians = phaseRadians;
        }

        public WaterWaveBand Validated()
        {
            return new WaterWaveBand(
                Mathf.Clamp(AmplitudeM, 0f, 5f),
                Mathf.Clamp(WavelengthM, 0.25f, 2000f),
                Mathf.Repeat(DirectionDegrees, 360f),
                Mathf.Repeat(PhaseRadians, Mathf.PI * 2f));
        }

        public Vector2 Direction
        {
            get
            {
                var radians = DirectionDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }
    }

    [Serializable]
    public sealed class WaterSurfaceSettings
    {
        public float SurfaceHeightM;
        public float PatchSizeM = 360f;
        public float FollowSnapM = 6f;
        public float TimeMultiplier = 0.72f;
        public WaterSurfaceQuality Quality = WaterSurfaceQuality.High;
        public WaterWaveBand LongWave = new WaterWaveBand(0.18f, 32f, 18f, 0f);
        public WaterWaveBand CrossWave = new WaterWaveBand(0.11f, 17f, 68f, 0.8f);
        public WaterWaveBand MediumWave = new WaterWaveBand(0.065f, 8.5f, 128f, 1.7f);
        public WaterWaveBand DetailWave = new WaterWaveBand(0.032f, 4.6f, 218f, 2.9f);
        public WaterWaveBand FineWave = new WaterWaveBand(0.018f, 2.8f, 304f, 4.1f);
        public Color ShallowColor = new Color(0.035f, 0.30f, 0.40f, 1f);
        public Color DeepColor = new Color(0.008f, 0.085f, 0.14f, 1f);
        public Color UnderwaterColor = new Color(0f, 0.27f, 0.23f, 1f);
        public Color InteractionFoamColor = new Color(0.72f, 0.86f, 0.88f, 1f);
        public float Smoothness = 0.88f;
        public float FresnelStrength = 0.9f;
        public float Alpha = 0.76f;
        public float RefractionDistortion = 0.026f;
        public float InteractionStrengthM = 0.42f;
        public float InteractionRadiusM = 19f;
        public float InteractionDepthFadeM = 0.7f;
        public float WakeLengthM = 42f;
        public float InteractionReferenceSpeedMps = 0.25f;

        public static WaterSurfaceSettings CreateDefault()
        {
            return new WaterSurfaceSettings();
        }

        public WaterSurfaceSettings ValidatedCopy()
        {
            return new WaterSurfaceSettings
            {
                SurfaceHeightM = Mathf.Clamp(SurfaceHeightM, -10000f, 10000f),
                PatchSizeM = Mathf.Clamp(PatchSizeM, 32f, 10000f),
                FollowSnapM = Mathf.Clamp(FollowSnapM, 0f, 1000f),
                TimeMultiplier = Mathf.Clamp(TimeMultiplier, 0f, 20f),
                Quality = Quality,
                LongWave = LongWave.Validated(),
                CrossWave = CrossWave.Validated(),
                MediumWave = MediumWave.Validated(),
                DetailWave = DetailWave.Validated(),
                FineWave = FineWave.Validated(),
                ShallowColor = ClampColor(ShallowColor),
                DeepColor = ClampColor(DeepColor),
                UnderwaterColor = ClampColor(UnderwaterColor),
                InteractionFoamColor = ClampColor(InteractionFoamColor),
                Smoothness = Mathf.Clamp01(Smoothness),
                FresnelStrength = Mathf.Clamp01(FresnelStrength),
                Alpha = Mathf.Clamp01(Alpha),
                RefractionDistortion = Mathf.Clamp(RefractionDistortion, 0f, 0.2f),
                InteractionStrengthM = Mathf.Clamp(InteractionStrengthM, 0f, 2f),
                InteractionRadiusM = Mathf.Clamp(InteractionRadiusM, 1f, 200f),
                InteractionDepthFadeM = Mathf.Clamp(InteractionDepthFadeM, 0.1f, 200f),
                WakeLengthM = Mathf.Clamp(WakeLengthM, 1f, 400f),
                InteractionReferenceSpeedMps = Mathf.Clamp(InteractionReferenceSpeedMps, 0.01f, 20f)
            };
        }

        public int GridSegments
        {
            get
            {
                switch (Quality)
                {
                    case WaterSurfaceQuality.Low:
                        return 64;
                    case WaterSurfaceQuality.High:
                        return 512;
                    default:
                        return 128;
                }
            }
        }

        public float MaximumWaveHeightM =>
            Mathf.Abs(LongWave.AmplitudeM)
            + Mathf.Abs(CrossWave.AmplitudeM)
            + Mathf.Abs(MediumWave.AmplitudeM)
            + Mathf.Abs(DetailWave.AmplitudeM)
            + Mathf.Abs(FineWave.AmplitudeM)
            + Mathf.Abs(InteractionStrengthM);

        private static Color ClampColor(Color color)
        {
            return new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a));
        }
    }
}
