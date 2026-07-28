using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CopernicusCurrentClientTests
    {
        [UnityTest]
        public IEnumerator LocalFileMode_DoesNotInvokeRemoteBackendAndLoadsSelectedFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.json");
            File.WriteAllText(path, "{\"source\":\"fixture\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":10,\"eastwardMps\":1,\"northwardMps\":2}]}");
            var backend = new SpyCurrentFetchBackend();
            var client = new CopernicusCurrentClient(backend);
            CopernicusCurrentResult loaded = null;
            yield return client.Fetch(RequestForTest(), OceanCurrentAcquisitionMode.LocalFile, path, DateTime.UtcNow,
                result => loaded = result, error => Assert.Fail(error));
            Assert.That(loaded, Is.Not.Null);
            Assert.That(backend.RunCount, Is.EqualTo(0));
            Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator CacheOnlyMode_DoesNotInvokeRemoteBackendAndUsesOnlyCache()
        {
            var request = RequestForTest();
            CopernicusCurrentCache.Store(request, "{\"source\":\"fixture\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":10,\"eastwardMps\":1,\"northwardMps\":2}]}");
            var backend = new SpyCurrentFetchBackend();
            var client = new CopernicusCurrentClient(backend);
            CopernicusCurrentResult loaded = null;
            yield return client.Fetch(request, OceanCurrentAcquisitionMode.CacheOnly, null, DateTime.UtcNow,
                result => loaded = result, error => Assert.Fail(error));
            Assert.That(loaded, Is.Not.Null);
            Assert.That(backend.RunCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OnlineMode_InvokesBackendExactlyOnce()
        {
            var backend = new SpyCurrentFetchBackend();
            var client = new CopernicusCurrentClient(backend);
            yield return client.Fetch(RequestForTest(), OceanCurrentAcquisitionMode.Online, null, DateTime.UtcNow,
                result => { }, error => Assert.Fail(error));
            Assert.That(backend.RunCount, Is.EqualTo(1));
        }

        [Test]
        public void RequestTimeoutSeconds_AllowsTheInitialRemoteSubsetToComplete()
        {
            Assert.That(CopernicusCurrentClient.RequestTimeoutSeconds, Is.GreaterThanOrEqualTo(180f));
        }

        [Test]
        public void BuildProgressMessage_ReportsTheRemoteDownloadStage()
        {
            var message = CopernicusCurrentClient.BuildProgressMessage(45f);

            Assert.That(message, Does.Contain("下载"));
            Assert.That(message, Does.Contain("45"));
        }

        [Test]
        public void SelectFailureReason_SkipsCopernicusInfoAndWarningLines()
        {
            const string standardError = "INFO - Selected dataset version: \"202406\"\nWARNING - depth range was clipped\nERROR - Access token was rejected";

            var reason = CopernicusCurrentClient.SelectFailureReason(standardError, string.Empty);

            Assert.That(reason, Is.EqualTo("ERROR - Access token was rejected"));
        }

        [Test]
        public void SelectFailureReason_ReplacesMetadataOnlyOutputWithAnActionableMessage()
        {
            const string standardError = "INFO - Selected dataset version: \"202406\"\nWARNING - depth range was clipped";

            var reason = CopernicusCurrentClient.SelectFailureReason(standardError, string.Empty);

            Assert.That(reason, Does.Contain("did not return a usable response"));
            Assert.That(reason, Does.Not.StartWith("INFO"));
        }

        [Test]
        public void TryReadCompletedResponse_AcceptsAValidResponseBeforeTheFetcherExits()
        {
            var responsePath = Path.Combine(Application.temporaryCachePath, "copernicus-response-" + System.Guid.NewGuid().ToString("N") + ".json");
            const string json = "{\"source\":\"Copernicus Marine\",\"datasetId\":\"dataset\",\"retrievedAtUtc\":\"2026-07-15T03:21:10Z\",\"layers\":[{\"minDepthM\":0.0,\"maxDepthM\":12.0,\"eastwardMps\":0.31,\"northwardMps\":-0.18}]}";
            File.WriteAllText(responsePath, json);

            try
            {
                Assert.That(CopernicusCurrentClient.TryReadCompletedResponse(responsePath, out var result), Is.True);
                Assert.That(result.Profile.Layers, Has.Count.EqualTo(1));
                Assert.That(result.Profile.GetVelocity(4f), Is.EqualTo(new Vector2(0.31f, -0.18f)));
            }
            finally
            {
                File.Delete(responsePath);
            }
        }

        private static CopernicusCurrentRequest RequestForTest()
        {
            return new CopernicusCurrentRequest(120.1d, 25.6d, 0f, 100f);
        }

        private sealed class SpyCurrentFetchBackend : ICopernicusCurrentFetchBackend
        {
            public int RunCount { get; private set; }

            public void Run(CopernicusCurrentRequest request, string requestPath, string responsePath, Action<string> onCompleted, Action<string> onFailure, Action<string> onProgress)
            {
                RunCount++;
                File.WriteAllText(responsePath, "{\"source\":\"backend\",\"datasetId\":\"test\",\"retrievedAtUtc\":\"2026-07-28T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":10,\"eastwardMps\":1,\"northwardMps\":2}]}");
                onCompleted?.Invoke(responsePath);
            }
        }
    }
}
