using System;
using System.IO;
using System.Collections;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine.TestTools;

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

        [Test]
        public void TryLoad_RejectsProfileOnlyJsonBecauseAcquisitionRequiresSpatialField()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "profile.json");
            File.WriteAllText(path, "{\"source\":\"fixture\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":10,\"eastwardMps\":1,\"northwardMps\":2}]}");
            try
            {
                Assert.That(OceanCurrentFileLoader.TryLoad(path, DateTime.UtcNow, out _, out var error), Is.False);
                Assert.That(error, Does.Contain("field"));
            }
            finally { Directory.Delete(directory, true); }
        }

        [UnityTest]
        public IEnumerator Load_RejectsConverterInvalidJsonOnTheCoroutinePath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.nc");
            File.WriteAllBytes(path, new byte[] { 0x89, (byte)'H', (byte)'D', (byte)'F' });
            var previous = OceanCurrentFileLoader.Converter;
            OceanCurrentFileLoader.Converter = new FakeConverter(output => File.WriteAllText(output, "not-json"));
            var failure = string.Empty;
            try
            {
                yield return OceanCurrentFileLoader.Load(path, DateTime.UtcNow, _ => Assert.Fail("Invalid converter output must not succeed."), error => failure = error);
                Assert.That(failure, Does.Contain("converter"));
            }
            finally { OceanCurrentFileLoader.Converter = previous; Directory.Delete(directory, true); }
        }

        [UnityTest]
        public IEnumerator Load_TimesOutWhenConverterDoesNotComplete()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.nc");
            File.WriteAllBytes(path, new byte[] { 0x89, (byte)'H', (byte)'D', (byte)'F' });
            var previous = OceanCurrentFileLoader.Converter;
            var previousTimeout = OceanCurrentFileLoader.ConverterTimeoutSeconds;
            OceanCurrentFileLoader.Converter = new FakeConverter(null, false);
            OceanCurrentFileLoader.ConverterTimeoutSeconds = .01f;
            var failure = string.Empty;
            try
            {
                yield return OceanCurrentFileLoader.Load(path, DateTime.UtcNow, _ => Assert.Fail("Timed-out converter must not succeed."), error => failure = error);
                Assert.That(failure, Does.Contain("timed out"));
            }
            finally { OceanCurrentFileLoader.Converter = previous; OceanCurrentFileLoader.ConverterTimeoutSeconds = previousTimeout; Directory.Delete(directory, true); }
        }

        private sealed class FakeConverter : IOceanCurrentFileConverter
        {
            private readonly Action<string> writeOutput;
            private readonly bool complete;
            public FakeConverter(Action<string> writeOutput, bool complete = true) { this.writeOutput = writeOutput; this.complete = complete; }
            public void Convert(string inputPath, string outputPath, DateTime referenceTimeUtc, Action onCompleted, Action<string> onFailure, Action<string> onProgress)
            {
                writeOutput?.Invoke(outputPath);
                if (complete) onCompleted?.Invoke();
            }
        }
    }
}
