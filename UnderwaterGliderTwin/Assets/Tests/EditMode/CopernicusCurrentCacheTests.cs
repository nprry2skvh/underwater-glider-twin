using NUnit.Framework;
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
    }
}
