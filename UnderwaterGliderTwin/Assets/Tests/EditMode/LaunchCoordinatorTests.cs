using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class LaunchCoordinatorTests
    {
        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(LaunchCoordinator.LastCsvPlayerPrefsKey);

        [Test]
        public void ApplyAndLaunch_InvalidCsvDoesNotLoadMainOrPersistPath()
        {
            var missingPath = Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".csv");
            var loadedScene = string.Empty;
            var coordinator = new LaunchCoordinator(scene => loadedScene = scene);
            var request = LaunchRequestParser.Parse(new[] { "--csv", missingPath }, string.Empty, SimulationProfile.Default);

            var launched = coordinator.ApplyAndLaunch(request);

            Assert.That(launched, Is.False);
            Assert.That(loadedScene, Is.Empty);
            Assert.That(PlayerPrefs.HasKey(LaunchCoordinator.LastCsvPlayerPrefsKey), Is.False);
        }
    }
}
