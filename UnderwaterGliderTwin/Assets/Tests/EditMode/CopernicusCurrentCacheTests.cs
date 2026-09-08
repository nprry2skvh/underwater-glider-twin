using NUnit.Framework;
using System;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CopernicusCurrentCacheTests
    {
        [Test]
        public void BuildCacheKey_IsStableForTheSameMissionRequest()
        {
            var first = new CopernicusCurrentRequest(120.123456d, 25.654321d, 0f, 1600f);
            var second = new CopernicusCurrentRequest(120.123456d, 25.654321d, 0f, 1600f);

            Assert.That(CopernicusCurrentCache.BuildCacheKey(first), Is.EqualTo(CopernicusCurrentCache.BuildCacheKey(second)));
        }

        [Test]
        public void BuildCacheKey_ChangesWhenRequestedDepthCoverageChanges()
        {
            var shallow = new CopernicusCurrentRequest(120.1d, 25.6d, 0f, 300f);
            var deep = new CopernicusCurrentRequest(120.1d, 25.6d, 0f, 1600f);

            Assert.That(CopernicusCurrentCache.BuildCacheKey(shallow), Is.Not.EqualTo(CopernicusCurrentCache.BuildCacheKey(deep)));
        }

        [Test]
        public void BuildCacheKey_ChangesWhenRequestedRegionChanges()
        {
            var narrow = new CopernicusCurrentRequest(140d, 15d, 0f, 1200f, 25f, 72f);
            var wide = new CopernicusCurrentRequest(140d, 15d, 0f, 1200f, 50f, 72f);

            Assert.That(CopernicusCurrentCache.BuildCacheKey(narrow), Is.Not.EqualTo(CopernicusCurrentCache.BuildCacheKey(wide)));
        }

        [Test]
        public void StoreAndTryLoad_UseSourceIdentityReferenceTimeAndPreserveField()
        {
            var request = new CopernicusCurrentRequest(120.2d, 25.7d, 0f, 20f);
            var reference = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
            var identity = OceanCurrentSourceIdentity.ForLocalFile("C:\\temp\\field.nc", "content-a", 1);
            const string json = "{\"source\":\"fixture\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":10,\"eastwardMps\":1,\"northwardMps\":2}],\"fieldSamples\":[{\"longitudeDeg\":120,\"latitudeDeg\":25,\"depthM\":5,\"elapsedSeconds\":0,\"eastwardMps\":1,\"northwardMps\":2,\"verticalMps\":0}]}";

            CopernicusCurrentCache.Store(request, json, reference, identity);

            Assert.That(CopernicusCurrentCache.TryLoad(request, reference, identity, out var result), Is.True);
            Assert.That(result.Field.Samples.Count, Is.EqualTo(1));
            Assert.That(CopernicusCurrentCache.TryLoad(request, reference, OceanCurrentSourceIdentity.ForLocalFile("C:\\temp\\field.nc", "content-b", 1), out _), Is.False);
        }
    }
}
