using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class LaunchRequestParserTests
    {
        private string tempDirectory;
        private string csvPath;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            csvPath = Path.Combine(tempDirectory, "input.csv");
            File.WriteAllText(csvPath, "header");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(LaunchCoordinator.LastCsvPlayerPrefsKey);
            if (!string.IsNullOrWhiteSpace(tempDirectory) && Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void Parse_UsesSimulationWhenCsvIsAlsoSpecified()
        {
            var request = LaunchRequestParser.Parse(new[] { "--csv", csvPath, "--simulation" }, string.Empty, SimulationProfile.Default);
            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Simulation));
        }

        [Test]
        public void Parse_DuplicateCsvArgumentsUseTheLastExplicitPath()
        {
            var secondDirectory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(secondDirectory);
            var second = Path.Combine(secondDirectory, "second.csv");
            File.WriteAllText(second, "header");
            try
            {
                var request = LaunchRequestParser.Parse(
                    new[] { "--csv", csvPath, "--csv", second }, string.Empty, SimulationProfile.Default);

                Assert.That(request.Mode, Is.EqualTo(LaunchMode.Csv));
                Assert.That(request.CsvPath, Is.EqualTo(Path.GetFullPath(second)));
            }
            finally
            {
                Directory.Delete(secondDirectory, true);
            }
        }

        [Test]
        public void Parse_LaterInvalidCsvArgumentIsNotRescuedByEarlierValidCsv()
        {
            var missing = Path.Combine(tempDirectory, Guid.NewGuid() + ".csv");
            var request = LaunchRequestParser.Parse(
                new[] { "--csv", csvPath, "--csv", missing }, string.Empty, SimulationProfile.Default);

            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Welcome));
            Assert.That(request.HasErrors, Is.True);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-1")]
        [TestCase("1,5")]
        public void Parse_InvalidSimulationDepthStaysOnWelcome(string value)
        {
            var request = LaunchRequestParser.Parse(
                new[] { "--simulation-depth", value }, string.Empty, SimulationProfile.Default);

            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Welcome));
            Assert.That(request.HasErrors, Is.True);
        }

        [Test]
        public void Parse_LaterInvalidSimulationDepthInvalidatesEarlierValue()
        {
            var request = LaunchRequestParser.Parse(
                new[] { "--simulation-depth", "10", "--simulation-depth", "bad" }, string.Empty, SimulationProfile.Default);

            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Welcome));
            Assert.That(request.HasErrors, Is.True);
        }

        [Test]
        public void Parse_UsesLastCompleteSimulationCurrentPair()
        {
            var request = LaunchRequestParser.Parse(
                new[] { "--simulation-current", "0.1", "0.2", "--simulation-current", "0.3", "0.4" }, string.Empty, SimulationProfile.Default);

            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Simulation));
            Assert.That(request.SimulationProfile.OceanCurrentProfile.GetVelocity(0f),
                Is.EqualTo(new Vector2(0.3f, 0.4f)));
        }

        [Test]
        public void Parse_MalformedLaterSimulationCurrentInvalidatesEarlierPair()
        {
            var request = LaunchRequestParser.Parse(
                new[] { "--simulation-current", "0.1", "0.2", "--simulation-current", "bad", "0.4" }, string.Empty, SimulationProfile.Default);

            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Welcome));
            Assert.That(request.HasErrors, Is.True);
        }

        [Test]
        public void Parse_AcceptsCsvEqualsSyntax()
        {
            var request = LaunchRequestParser.Parse(new[] { "--csv=" + csvPath }, string.Empty, SimulationProfile.Default);
            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Csv));
            Assert.That(request.CsvPath, Is.EqualTo(Path.GetFullPath(csvPath)));
        }

        [Test]
        public void Parse_EmptyCsvValueStaysOnWelcomeWithError()
        {
            var request = LaunchRequestParser.Parse(new[] { "--csv=" }, string.Empty, SimulationProfile.Default);
            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Welcome));
            Assert.That(request.HasErrors, Is.True);
        }

        [Test]
        public void Parse_MissingCsvValueStaysOnWelcomeWithError()
        {
            var request = LaunchRequestParser.Parse(new[] { "--csv" }, string.Empty, SimulationProfile.Default);
            Assert.That(request.Mode, Is.EqualTo(LaunchMode.Welcome));
            Assert.That(request.HasErrors, Is.True);
        }

        [Test]
        public void Coordinator_AppliesCsvAndRequestsMainScene()
        {
            string scene = null;
            var coordinator = new LaunchCoordinator(value => scene = value);
            Assert.That(coordinator.ApplyAndLaunch(coordinator.CreateCsvRequest(csvPath)), Is.True);
            Assert.That(RuntimeDataSourceState.CurrentMode, Is.EqualTo(RuntimeDataSourceMode.Csv));
            Assert.That(scene, Is.EqualTo("Main"));
        }

        [Test]
        public void Persistence_OnlyStoresExistingFiles()
        {
            LaunchCoordinator.SaveSuccessfulCsvPath(csvPath);
            Assert.That(LaunchCoordinator.GetLastSuccessfulCsvPath(), Is.EqualTo(Path.GetFullPath(csvPath)));
            File.Delete(csvPath);
            Assert.That(LaunchCoordinator.GetLastSuccessfulCsvPath(), Is.Empty);
        }

        [Test]
        public void Persistence_IgnoresMissingFileWhenSaving()
        {
            var missing = Path.Combine(tempDirectory, "missing.csv");

            LaunchCoordinator.SaveSuccessfulCsvPath(missing);

            Assert.That(LaunchCoordinator.GetLastSuccessfulCsvPath(), Is.Empty);
        }
    }
}
