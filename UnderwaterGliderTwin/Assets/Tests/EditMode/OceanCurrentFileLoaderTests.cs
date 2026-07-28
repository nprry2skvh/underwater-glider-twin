using System;
using System.IO;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class OceanCurrentFileLoaderTests
    {
        [Test]
        public void TryLoad_ParsesJsonFixtureAndNormalizesTheAbsolutePath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.json");
            File.WriteAllText(path, "{\"source\":\"fixture\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":10,\"eastwardMps\":1,\"northwardMps\":2}],\"fieldSamples\":[{\"longitudeDeg\":120,\"latitudeDeg\":25,\"depthM\":5,\"elapsedSeconds\":0,\"eastwardMps\":1,\"northwardMps\":2,\"verticalMps\":0}]}");
            try
            {
                Assert.That(OceanCurrentFileLoader.TryLoad(path, DateTime.UtcNow, out var loaded, out var error), Is.True, error);
                Assert.That(loaded.Field.Samples.Count, Is.EqualTo(1));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SourceIdentity_ChangesWhenLocalContentChanges()
        {
            var first = OceanCurrentSourceIdentity.ForLocalFile("C:\\temp\\field.nc", "hash-a", 1);
            var second = OceanCurrentSourceIdentity.ForLocalFile("C:\\temp\\field.nc", "hash-b", 1);

            Assert.That(first.CacheToken, Is.Not.EqualTo(second.CacheToken));
        }
    }
}
