using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CopernicusCurrentResponseParserTests
    {
        [Test]
        public void Request_UsesDefaultCurrentDatasetAndAcceptsValidCoordinates()
        {
            var request = new CopernicusCurrentRequest(120.25, 25.75, 0f, 520f);

            Assert.That(request.DatasetId, Is.EqualTo("cmems_mod_glo_phy-cur_anfc_0.083deg_P1D-m"));
            Assert.That(request.TryValidate(out var error), Is.True, error);
        }

        [Test]
        public void Parse_BuildsProfileFromCurrentLayers()
        {
            const string json = "{\"source\":\"Copernicus Marine\",\"datasetId\":\"cmems_mod_glo_phy-cur_anfc_0.083deg_P1D-m\",\"retrievedAtUtc\":\"2026-07-13T05:00:00Z\",\"layers\":[{\"minDepthM\":0.0,\"maxDepthM\":12.0,\"eastwardMps\":0.31,\"northwardMps\":-0.18},{\"minDepthM\":12.0,\"maxDepthM\":50.0,\"eastwardMps\":-0.05,\"northwardMps\":0.42}],\"fieldSamples\":[{\"longitudeDeg\":120,\"latitudeDeg\":25,\"depthM\":30,\"elapsedSeconds\":0,\"eastwardMps\":-0.05,\"northwardMps\":0.42}]}";

            var result = CopernicusCurrentResponseParser.Parse(json);

            Assert.That(result.Source, Is.EqualTo("Copernicus Marine"));
            Assert.That(result.DatasetId, Is.EqualTo("cmems_mod_glo_phy-cur_anfc_0.083deg_P1D-m"));
            Assert.That(result.Profile.Layers, Has.Count.EqualTo(2));
            Assert.That(result.Profile.GetVelocity(30f), Is.EqualTo(new Vector2(-0.05f, 0.42f)));
        }

        [Test]
        public void Parse_RejectsProfileOnlyPayloadBecauseAcquisitionRequiresSpatialField()
        {
            const string json = "{\"layers\":[{\"minDepthM\":0,\"maxDepthM\":1,\"eastwardMps\":0,\"northwardMps\":0}]}";

            Assert.That(() => CopernicusCurrentResponseParser.Parse(json), Throws.ArgumentException.With.Message.Contains("spatial field"));
        }

        [Test]
        public void Parse_BuildsSpatialFieldFromGridSamples()
        {
            const string json = "{\"source\":\"Copernicus Marine\",\"datasetId\":\"dataset\",\"retrievedAtUtc\":\"2026-07-18T00:00:00Z\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":50,\"eastwardMps\":0.1,\"northwardMps\":0.2}],\"fieldSamples\":[{\"longitudeDeg\":140.0,\"latitudeDeg\":15.0,\"depthM\":20.0,\"elapsedSeconds\":0.0,\"eastwardMps\":0.1,\"northwardMps\":0.2},{\"longitudeDeg\":140.1,\"latitudeDeg\":15.0,\"depthM\":20.0,\"elapsedSeconds\":0.0,\"eastwardMps\":0.4,\"northwardMps\":-0.1}]}";

            var result = CopernicusCurrentResponseParser.Parse(json);

            Assert.That(result.Field.Samples, Has.Count.EqualTo(2));
            Assert.That(result.Field.TryGetVelocity(140.1d, 15d, 20f, 0f, out var velocity), Is.True);
            Assert.That(velocity, Is.EqualTo(new Vector2(0.4f, -0.1f)));
        }

        [Test]
        public void Parse_PreservesOptionalVerticalVelocityInGridSamples()
        {
            const string json = "{\"source\":\"Copernicus Marine\",\"layers\":[{\"minDepthM\":0,\"maxDepthM\":50,\"eastwardMps\":0.1,\"northwardMps\":0.2}],\"fieldSamples\":[{\"longitudeDeg\":140.0,\"latitudeDeg\":15.0,\"depthM\":20.0,\"elapsedSeconds\":0.0,\"eastwardMps\":0.1,\"northwardMps\":0.2,\"verticalMps\":-0.03}]}";

            var result = CopernicusCurrentResponseParser.Parse(json);

            Assert.That(result.Field.Samples[0].VerticalMps, Is.EqualTo(-0.03f).Within(0.0001f));
        }
    }
}
