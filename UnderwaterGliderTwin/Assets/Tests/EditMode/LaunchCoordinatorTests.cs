using System;
using System.IO;
using System.Linq;
using System.Text;
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

        [Test]
        public void ApplyAndLaunch_ExistingInvalidHeaderCsvDoesNotLoadMainOrPersistPath()
        {
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "invalid-header.csv");
                File.WriteAllText(path, "wrong,columns\n1,2\n", StrictGbk());
                var loadedScene = string.Empty;
                var coordinator = new LaunchCoordinator(scene => loadedScene = scene);
                var request = LaunchRequestParser.Parse(new[] { "--csv", path }, string.Empty, SimulationProfile.Default);

                var launched = coordinator.ApplyAndLaunch(request);

                Assert.That(launched, Is.False);
                Assert.That(loadedScene, Is.Empty);
                Assert.That(coordinator.LastError, Does.Contain("header"));
                Assert.That(PlayerPrefs.HasKey(LaunchCoordinator.LastCsvPlayerPrefsKey), Is.False);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void ApplyAndLaunch_ExistingValidGbkCsvLoadsMainAndPersistsPath()
        {
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "valid-gbk.csv");
                File.WriteAllText(path, ValidTelemetryCsv(), StrictGbk());
                var loadedScene = string.Empty;
                var coordinator = new LaunchCoordinator(scene => loadedScene = scene);
                var request = LaunchRequestParser.Parse(new[] { "--csv", path }, string.Empty, SimulationProfile.Default);

                var launched = coordinator.ApplyAndLaunch(request);

                Assert.That(launched, Is.True);
                Assert.That(loadedScene, Is.EqualTo(LaunchCoordinator.MainSceneName));
                Assert.That(LaunchCoordinator.GetLastSuccessfulCsvPath(), Is.EqualTo(Path.GetFullPath(path)));
                Assert.That(coordinator.LastError, Is.Empty);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static string ValidTelemetryCsv()
        {
            var header = string.Join(",", Enumerable.Range(0, 51).Select(i => $"col{i}"));
            var row = new[]
            {
                "000d 00h 00m 09s", "1", "28.5", "0.3", "0", "32", "10", "0", "7", "88", "3", "0", "0",
                "姘撮潰妯″紡", "姘撮潰", "2", "0", "32", "17", "0", "16", "120.00008333", "25.00001728", "2.3",
                "100.0", "30.4", "-2", "-5", "0", "30", "44.3", "1000", "500", "29.7", "68.6", "2.931",
                "26.4086", "2.35", "0", "27", "0", "95", "89", "0", "5241", "0", "0", "31162", "0", "0.0", "1576"
            };
            return header + "\n" + string.Join(",", row) + "\n";
        }

        private static Encoding StrictGbk() =>
            Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.GetFullPath(directory);
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
