using NUnit.Framework;
using System;
using System.IO;
using UnityEngine;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class RuntimeSmokeOptionsTests
    {
        [Test]
        public void Parse_LocalCurrentPathRequestsPackagedSmoke()
        {
            var options = RuntimeSmokeOptions.Parse(new[] { "--simulation", "--smoke-local-current", @"C:\temp\ocean-current.json", "--quit-after-smoke" });

            Assert.That(options.IsRequested, Is.True);
            Assert.That(options.LocalOceanCurrentPath, Is.EqualTo(@"C:\temp\ocean-current.json"));
            Assert.That(options.QuitAfterCompletion, Is.True);
        }

        [Test]
        public void Parse_MissingLocalCurrentPathDoesNotRequestSmoke()
        {
            var options = RuntimeSmokeOptions.Parse(new[] { "--simulation", "--smoke-local-current", "--quit-after-smoke" });

            Assert.That(options.IsRequested, Is.False);
            Assert.That(options.LocalOceanCurrentPath, Is.EqualTo(string.Empty));
            Assert.That(options.QuitAfterCompletion, Is.False);
        }

        [Test]
        public void TryCreateLocalCurrentProfileUpdate_LoadsJsonWithoutMutatingBaseProfile()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "runtime-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.json");
            File.WriteAllText(path, "{\"source\":\"local smoke fixture\",\"datasetId\":\"smoke\",\"retrievedAtUtc\":\"2026-07-30T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":160,\"eastwardMps\":0.31,\"northwardMps\":-0.18}],\"fieldSamples\":[{\"longitudeDeg\":121.234567,\"latitudeDeg\":24.345678,\"depthM\":80,\"elapsedSeconds\":0,\"eastwardMps\":0.31,\"northwardMps\":-0.18,\"verticalMps\":0}]}");
            var baseProfile = SimulationProfile.Default;
            var originalLayers = baseProfile.OceanCurrentProfile.Layers.Count;

            try
            {
                Assert.That(RuntimeSmokeProfileUpdate.TryCreateLocalCurrentProfileUpdate(
                    baseProfile,
                    path,
                    new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
                    out var updated,
                    out var source,
                    out var error), Is.True, error);

                Assert.That(updated, Is.Not.SameAs(baseProfile));
                Assert.That(updated.OceanCurrentProfile.GetVelocity(80f), Is.EqualTo(new Vector2(0.31f, -0.18f)));
                Assert.That(updated.OceanCurrentField.Samples, Has.Count.EqualTo(1));
                Assert.That(source, Is.EqualTo("local smoke fixture"));
                Assert.That(baseProfile.OceanCurrentProfile.Layers.Count, Is.EqualTo(originalLayers));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BuildSmokeRebuildProfile_CompactsTheFutureRebuildWorkload()
        {
            var profile = SimulationProfile.Default;
            profile.CycleCount = 6;
            profile.CycleDurationSeconds = 900f;
            profile.SampleIntervalSeconds = 5f;
            profile.TargetDepthM = 160f;
            profile.WaterColumnDepthM = 520f;

            var smokeProfile = RuntimeSmokeProfileUpdate.BuildSmokeRebuildProfile(profile);

            Assert.That(smokeProfile, Is.Not.SameAs(profile));
            Assert.That(smokeProfile.CycleCount, Is.EqualTo(1));
            Assert.That(smokeProfile.CycleDurationSeconds, Is.LessThan(profile.CycleDurationSeconds));
            Assert.That(smokeProfile.TargetDepthM, Is.LessThanOrEqualTo(120f));
            Assert.That(smokeProfile.WaterColumnDepthM, Is.GreaterThanOrEqualTo(smokeProfile.TargetDepthM + 20f));
        }
    }
}
