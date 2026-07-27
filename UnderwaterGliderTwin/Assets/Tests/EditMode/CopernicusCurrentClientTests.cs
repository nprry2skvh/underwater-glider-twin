using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CopernicusCurrentClientTests
    {
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
    }
}
