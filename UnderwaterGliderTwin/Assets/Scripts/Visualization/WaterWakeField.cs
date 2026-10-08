using System;
using UnderwaterGliderTwin.Mapping;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class WaterWakeField : IDisposable
    {
        private static readonly int PathSamplesId = Shader.PropertyToID("_PathSamples");
        private static readonly int SampleCountId = Shader.PropertyToID("_SampleCount");
        private static readonly int FieldOriginExtentId = Shader.PropertyToID("_FieldOriginExtent");
        private static readonly int WakeLengthId = Shader.PropertyToID("_WakeLength");
        private static readonly int WakeAgeDurationId = Shader.PropertyToID("_WakeAgeDuration");
        private static readonly int DepthFadeDistanceId = Shader.PropertyToID("_DepthFadeDistance");
        private static readonly int DepthScaleId = Shader.PropertyToID("_DepthScale");
        private static readonly int WakeStrengthId = Shader.PropertyToID("_WakeStrength");
        private static readonly int FieldTexelWidthId = Shader.PropertyToID("_FieldTexelWidth");

        private readonly WaterSurfaceSettings settings;
        private readonly Vector4[] packedSamples = new Vector4[WaterWakePathSampler.MaximumSampleCount * 2];
        private readonly Material generatorMaterial;
        private Texture2D pathTexture;
        private RenderTexture fieldTexture;
        private bool disposed;

        public WaterWakeField(WaterSurfaceSettings sourceSettings, Shader generatorShader)
        {
            settings = (sourceSettings ?? WaterSurfaceSettings.CreateDefault()).ValidatedCopy();
            if (generatorShader == null
                || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                return;
            }

            Material createdMaterial = null;
            Texture2D createdPathTexture = null;
            try
            {
                createdMaterial = new Material(generatorShader)
                {
                    name = "Water Wake Field Generator (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                createdPathTexture = new Texture2D(
                    WaterWakePathSampler.MaximumSampleCount,
                    2,
                    TextureFormat.RGBAFloat,
                    mipChain: false,
                    linear: true)
                {
                    name = "Water Wake Path Samples (Runtime)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                generatorMaterial = createdMaterial;
                pathTexture = createdPathTexture;
            }
            catch (Exception)
            {
                DestroyOwned(createdPathTexture);
                DestroyOwned(createdMaterial);
                generatorMaterial = null;
                pathTexture = null;
            }
        }

        public bool IsReady => !disposed && generatorMaterial != null && pathTexture != null;
        public RenderTexture Texture => IsReady && fieldTexture != null && fieldTexture.IsCreated() ? fieldTexture : null;
        public int Resolution => Texture != null ? Texture.width : 0;

        public static int ResolutionForQuality(WaterSurfaceQuality quality)
        {
            switch (quality)
            {
                case WaterSurfaceQuality.Low:
                    return 64;
                case WaterSurfaceQuality.Medium:
                    return 128;
                default:
                    return 256;
            }
        }

        public bool Rebuild(
            WaterWakePathSample[] samples,
            int sampleCount,
            Vector2 worldOrigin,
            float fieldDiameter,
            int resolution,
            float depthScale = 1f,
            float horizontalScale = 1f,
            float movingAgeSeconds = -1f)
        {
            if (!IsReady
                || samples == null
                || sampleCount < 1
                || sampleCount > WaterWakePathSampler.MaximumSampleCount
                || sampleCount > samples.Length
                || !IsFinite(worldOrigin.x)
                || !IsFinite(worldOrigin.y)
                || !IsFinite(fieldDiameter)
                || fieldDiameter <= 0f
                || !IsFinite(depthScale)
                || depthScale <= 0f
                || !IsFinite(horizontalScale)
                || horizontalScale <= 0f
                || !IsFinite(movingAgeSeconds)
                || movingAgeSeconds < -1f
                || !IsSupportedResolution(resolution))
            {
                return false;
            }

            try
            {
                if (!EnsureOutputTexture(resolution))
                {
                    return false;
                }
            }
            catch (Exception)
            {
                ReleaseOutputTexture();
                return false;
            }

            try
            {
                Array.Clear(packedSamples, 0, packedSamples.Length);
                for (var index = 0; index < sampleCount; index++)
                {
                    var sample = samples[index];
                    if (!IsFinite(sample.PositionXZ.x)
                        || !IsFinite(sample.PositionXZ.y)
                        || !IsFinite(sample.DepthM)
                        || !IsFinite(sample.AgeSeconds)
                        || !IsFinite(sample.DistanceAlongPath)
                        || sample.DepthM < 0f
                        || sample.AgeSeconds < 0f
                        || sample.DistanceAlongPath < 0f)
                    {
                        Clear();
                        return false;
                    }

                    packedSamples[index] = new Vector4(
                        sample.PositionXZ.x - worldOrigin.x,
                        sample.PositionXZ.y - worldOrigin.y,
                        sample.DepthM,
                        sample.AgeSeconds);
                    packedSamples[WaterWakePathSampler.MaximumSampleCount + index] = new Vector4(
                        sample.DistanceAlongPath, 0f, 0f, 0f);
                }

                pathTexture.SetPixelData(packedSamples, 0);
                pathTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                generatorMaterial.SetTexture(PathSamplesId, pathTexture);
                generatorMaterial.SetFloat(SampleCountId, sampleCount);
                generatorMaterial.SetVector(FieldOriginExtentId, new Vector4(
                    worldOrigin.x,
                    worldOrigin.y,
                    fieldDiameter,
                    1f / fieldDiameter));
                generatorMaterial.SetFloat(WakeLengthId, settings.WakeLengthM);
                var pathDistance = 0f;
                for (var index = 1; index < sampleCount; index++)
                {
                    pathDistance += Vector2.Distance(samples[index - 1].PositionXZ, samples[index].PositionXZ);
                }

                var historyAge = samples[sampleCount - 1].AgeSeconds;
                var movingHistoryAge = movingAgeSeconds >= 0f ? movingAgeSeconds : historyAge;
                var ageDuration = pathDistance > 0.00001f && movingHistoryAge > 0.00001f
                    ? settings.WakeLengthM * movingHistoryAge / pathDistance
                    : 86400f;
                var maximumAgeDuration = Mathf.Clamp(
                    2f * settings.WakeLengthM
                    / (settings.InteractionReferenceSpeedMps * horizontalScale),
                    30f,
                    86400f);
                generatorMaterial.SetFloat(WakeAgeDurationId, Mathf.Clamp(ageDuration, 30f, maximumAgeDuration));
                generatorMaterial.SetFloat(
                    DepthFadeDistanceId,
                    WaterSurfaceScale.DepthToWorld(settings.InteractionDepthFadeM, depthScale));
                generatorMaterial.SetFloat(DepthScaleId, depthScale);
                generatorMaterial.SetFloat(WakeStrengthId, settings.InteractionStrengthM);
                generatorMaterial.SetFloat(FieldTexelWidthId, fieldDiameter / resolution);

                Graphics.Blit(null, fieldTexture, generatorMaterial, 0);
                return true;
            }
            catch (Exception)
            {
                ReleaseOutputTexture();
                return false;
            }
        }

        public void Clear()
        {
            if (fieldTexture == null || !fieldTexture.IsCreated())
            {
                return;
            }

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = fieldTexture;
                GL.Clear(false, true, Color.clear);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ReleaseOutputTexture();
            DestroyOwned(pathTexture);
            DestroyOwned(generatorMaterial);
            pathTexture = null;
        }

        private bool EnsureOutputTexture(int resolution)
        {
            if (fieldTexture != null && fieldTexture.width == resolution && fieldTexture.IsCreated())
            {
                return true;
            }

            ReleaseOutputTexture();
            fieldTexture = new RenderTexture(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "Water Wake Field (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (fieldTexture.Create() && fieldTexture.IsCreated())
            {
                return true;
            }

            ReleaseOutputTexture();
            return false;
        }

        private void ReleaseOutputTexture()
        {
            if (fieldTexture == null)
            {
                return;
            }

            fieldTexture.Release();
            DestroyOwned(fieldTexture);
            fieldTexture = null;
        }

        private static bool IsSupportedResolution(int resolution)
        {
            return resolution == 64 || resolution == 128 || resolution == 256;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
