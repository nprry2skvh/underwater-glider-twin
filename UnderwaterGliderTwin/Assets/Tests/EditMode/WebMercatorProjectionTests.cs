using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class WebMercatorProjectionTests
    {
        [Test]
        public void LongitudeLatitudeToTile_MapsPrimeMeridianAtZoomOneToCenterTile()
        {
            Assert.That(WebMercatorProjection.LongitudeLatitudeToTile(0d, 0d, 1), Is.EqualTo(new Vector2Int(1, 1)));
        }

        [Test]
        public void GoogleProvider_WithoutKeyReturnsConfigurationError()
        {
            var provider = new GoogleMapTilesClient(new MapProviderSettings());
            Assert.That(provider.TryGetTileUrl(5, 10, 11, MapStyle.Satellite, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("Google API Key"));
        }
    }
}
