using NUnit.Framework;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class MissionGeoReferenceTests
    {
        [Test]
        public void TryCreate_AcceptsWgs84Coordinates()
        {
            Assert.That(MissionGeoReference.TryCreate(120.25, 25.5, out var value, out var error), Is.True, error);
            Assert.That(value.LongitudeDeg, Is.EqualTo(120.25));
            Assert.That(value.LatitudeDeg, Is.EqualTo(25.5));
        }

        [Test]
        public void TryCreate_RejectsInvalidLatitude()
        {
            Assert.That(MissionGeoReference.TryCreate(120, 91, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("纬度"));
        }

        [Test]
        public void MaskedGoogleKey_DoesNotExposeMiddleCharacters()
        {
            var settings = new MapProviderSettings { GoogleApiKey = "AIzaSyExampleSecret" };
            Assert.That(settings.MaskedGoogleApiKey, Is.EqualTo("AIza...cret"));
        }
    }
}
