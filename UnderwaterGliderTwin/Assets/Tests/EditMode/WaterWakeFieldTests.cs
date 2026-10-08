using NUnit.Framework;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class WaterWakeFieldTests
    {
        [Test]
        public void ResolutionForQuality_UsesTheConfiguredBoundedSizes()
        {
            Assert.That(WaterWakeField.ResolutionForQuality(WaterSurfaceQuality.Low), Is.EqualTo(64));
            Assert.That(WaterWakeField.ResolutionForQuality(WaterSurfaceQuality.Medium), Is.EqualTo(128));
            Assert.That(WaterWakeField.ResolutionForQuality(WaterSurfaceQuality.High), Is.EqualTo(256));
        }

        [Test]
        public void DepthToWorld_UsesTheMapperVerticalScale()
        {
            Assert.That(WaterSurfaceScale.DepthToWorld(0.7f, 0.05f), Is.EqualTo(0.035f).Within(0.000001f));
            Assert.That(WaterSurfaceScale.DepthToWorld(0.7f, 1f), Is.EqualTo(0.7f).Within(0.000001f));
        }

        [Test]
        public void Create_UsesSupportedFallbackAndReleasesOwnedResources()
        {
            var field = new WaterWakeField(WaterSurfaceSettings.CreateDefault(), generatorShader: null);
            try
            {
                Assert.That(field.IsReady, Is.False);
                Assert.That(field.Texture, Is.Null);
                Assert.That(field.Rebuild(new WaterWakePathSample[0], 0, Vector2.zero, 10f, 64), Is.False);
            }
            finally
            {
                field.Dispose();
                field.Dispose();
            }

            Assert.That(field.IsReady, Is.False);
            Assert.That(field.Texture, Is.Null);
        }

        [Test]
        public void Rebuild_RejectsInvalidBoundsAndSampleCounts()
        {
            var shader = Shader.Find("Hidden/UnderwaterGliderTwin/WaterWakeField");
            if (shader == null
                || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null
                || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Assert.Ignore("Wake-field render resources are unavailable in this editor graphics context.");
            }

            using (var field = new WaterWakeField(WaterSurfaceSettings.CreateDefault(), shader))
            {
                var sample = new[] { new WaterWakePathSample(Vector2.zero, 1f, 0f) };
                Assert.That(field.Rebuild(sample, 0, Vector2.zero, 10f, 64), Is.False);
                Assert.That(field.Rebuild(sample, 1, Vector2.zero, float.NaN, 64), Is.False);
                Assert.That(field.Rebuild(sample, 1, Vector2.zero, 10f, 512), Is.False);
                var invalidDistance = new[]
                {
                    new WaterWakePathSample(Vector2.zero, 1f, 0f, float.NaN)
                };
                Assert.That(field.Rebuild(invalidDistance, 1, Vector2.zero, 10f, 64), Is.False);
            }
        }

        [Test]
        public void Rebuild_KeepsTheExistingPacketAtTheSameWorldPositionAsPlaybackAdvances()
        {
            var shader = Shader.Find("Hidden/UnderwaterGliderTwin/WaterWakeField");
            if (shader == null
                || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null
                || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Assert.Ignore("Wake-field render resources are unavailable in this editor graphics context.");
            }

            var settings = WaterSurfaceSettings.CreateDefault();
            settings.WakeLengthM = 20f;
            var early = StraightPathSamples(6, 2);
            var late = StraightPathSamples(7, 2);
            using (var field = new WaterWakeField(settings, shader))
            {
                Assert.That(field.Rebuild(early, early.Length, new Vector2(4f, 0f), 20f, 256), Is.True);
                var earlyPeak = FindCrestPeakX(field.Texture, 4f, 20f);
                Assert.That(field.Rebuild(late, late.Length, new Vector2(4f, 0f), 20f, 256), Is.True);
                var latePeak = FindCrestPeakX(field.Texture, 4f, 20f);

                Assert.That(earlyPeak, Is.EqualTo(3.3f).Within(0.4f));
                Assert.That(latePeak, Is.EqualTo(earlyPeak).Within(0.2f));
            }
        }

        [Test]
        public void Rebuild_DissipatesOldPacketsDuringAStationaryInterval()
        {
            var shader = Shader.Find("Hidden/UnderwaterGliderTwin/WaterWakeField");
            if (shader == null
                || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null
                || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Assert.Ignore("Wake-field render resources are unavailable in this editor graphics context.");
            }

            var settings = WaterSurfaceSettings.CreateDefault();
            settings.WakeLengthM = 20f;
            var recent = new[]
            {
                new WaterWakePathSample(new Vector2(6f, 0f), 0f, 0f, 6f),
                new WaterWakePathSample(new Vector2(4f, 0f), 0f, 5f, 4f),
                new WaterWakePathSample(new Vector2(2f, 0f), 0f, 10f, 2f)
            };
            var old = new[]
            {
                recent[0],
                new WaterWakePathSample(new Vector2(4f, 0f), 0f, 1000f, 4f),
                new WaterWakePathSample(new Vector2(2f, 0f), 0f, 1005f, 2f)
            };

            using (var field = new WaterWakeField(settings, shader))
            {
                Assert.That(field.Rebuild(
                    recent, recent.Length, new Vector2(4f, 0f), 20f, 256,
                    horizontalScale: 0.003f, movingAgeSeconds: 10f), Is.True);
                var recentCrest = ReadCrestAt(field.Texture, 4f, 20f, 3.3f);
                Assert.That(field.Rebuild(
                    old, old.Length, new Vector2(4f, 0f), 20f, 256,
                    horizontalScale: 0.003f, movingAgeSeconds: 10f), Is.True);
                var oldCrest = ReadCrestAt(field.Texture, 4f, 20f, 3.3f);

                Assert.That(recentCrest, Is.GreaterThan(0.01f));
                Assert.That(oldCrest, Is.LessThan(recentCrest * 0.1f));
            }
        }

        private static WaterWakePathSample[] StraightPathSamples(int newestX, int oldestX)
        {
            var samples = new WaterWakePathSample[newestX - oldestX + 1];
            for (var index = 0; index < samples.Length; index++)
            {
                var x = newestX - index;
                samples[index] = new WaterWakePathSample(new Vector2(x, 0f), 0f, 0f, x);
            }

            return samples;
        }

        private static float FindCrestPeakX(RenderTexture fieldTexture, float originX, float diameter)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(fieldTexture.width, fieldTexture.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = fieldTexture;
                pixels.ReadPixels(new Rect(0, 0, fieldTexture.width, fieldTexture.height), 0, 0);
                pixels.Apply();
                var peak = -1f;
                var peakX = 0f;
                var centerY = fieldTexture.height / 2;
                for (var x = 2.5f; x <= 4f; x += 0.1f)
                {
                    var pixelX = Mathf.Clamp(
                        Mathf.RoundToInt((x - originX) / diameter * fieldTexture.width + fieldTexture.width / 2f),
                        0,
                        fieldTexture.width - 1);
                    var crest = pixels.GetPixel(pixelX, centerY).b;
                    if (crest > peak)
                    {
                        peak = crest;
                        peakX = x;
                    }
                }

                return peakX;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
            }
        }

        private static float ReadCrestAt(RenderTexture fieldTexture, float originX, float diameter, float worldX)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(fieldTexture.width, fieldTexture.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = fieldTexture;
                pixels.ReadPixels(new Rect(0, 0, fieldTexture.width, fieldTexture.height), 0, 0);
                pixels.Apply();
                var pixelX = Mathf.Clamp(
                    Mathf.RoundToInt((worldX - originX) / diameter * fieldTexture.width + fieldTexture.width / 2f),
                    0,
                    fieldTexture.width - 1);
                return pixels.GetPixel(pixelX, fieldTexture.height / 2).b;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
