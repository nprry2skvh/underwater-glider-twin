using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Bootstrap;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class RuntimePathResolverTests
    {
        [Test]
        public void ResolveCsvPathFromArgs_UsesExplicitCsvArgument()
        {
            var directory = CreateTempDirectory();
            var explicitPath = Path.Combine(directory, "explicit.csv");
            var candidatePath = Path.Combine(directory, "2.csv");
            File.WriteAllText(explicitPath, "explicit");
            File.WriteAllText(candidatePath, "candidate");

            var resolved = RuntimePathResolver.ResolveCsvPathFromArgs(
                new[] { "--csv", explicitPath },
                new[] { candidatePath });

            Assert.That(resolved, Is.EqualTo(explicitPath));
        }

        [Test]
        public void ResolveCsvPathFromArgs_UsesFirstExistingCandidate()
        {
            var directory = CreateTempDirectory();
            var missingPath = Path.Combine(directory, "missing.csv");
            var firstExistingPath = Path.Combine(directory, "2.csv");
            File.WriteAllText(firstExistingPath, "candidate");

            var resolved = RuntimePathResolver.ResolveCsvPathFromArgs(
                Array.Empty<string>(),
                new[] { missingPath, firstExistingPath });

            Assert.That(resolved, Is.EqualTo(firstExistingPath));
        }

        [Test]
        public void ResolveCsvPath_UsesOverrideBeforeExplicitArgument()
        {
            var directory = CreateTempDirectory();
            var explicitPath = Path.Combine(directory, "explicit.csv");
            var overridePath = Path.Combine(directory, "override.csv");
            File.WriteAllText(explicitPath, "explicit");
            File.WriteAllText(overridePath, "override");

            var resolved = RuntimePathResolver.ResolveCsvPath(
                new[] { "--csv", explicitPath },
                new[] { explicitPath },
                overridePath);

            Assert.That(resolved, Is.EqualTo(overridePath));
        }

        [Test]
        public void ResolveModelsDirectory_UsesExplicitModelsArgument()
        {
            var missingCandidate = Path.Combine(CreateTempDirectory(), "Models");
            var modelsRoot = CreateModelsRoot();

            var resolved = RuntimePathResolver.ResolveModelsDirectory(
                new[] { "--models", modelsRoot },
                new[] { missingCandidate });

            Assert.That(resolved, Is.EqualTo(modelsRoot));
        }

        [Test]
        public void ResolveModelsDirectory_UsesFirstCandidateWithXGBoostManifest()
        {
            var missingCandidate = Path.Combine(CreateTempDirectory(), "Models");
            var firstUsableCandidate = CreateModelsRoot();
            var secondUsableCandidate = CreateModelsRoot();

            var resolved = RuntimePathResolver.ResolveModelsDirectory(
                Array.Empty<string>(),
                new[] { missingCandidate, firstUsableCandidate, secondUsableCandidate });

            Assert.That(resolved, Is.EqualTo(firstUsableCandidate));
        }

        [Test]
        public void ResolveModelsDirectory_DoesNotCreateMissingFallbackDirectory()
        {
            var missingCandidate = Path.Combine(CreateTempDirectory(), "Models");

            var resolved = RuntimePathResolver.ResolveModelsDirectory(
                Array.Empty<string>(),
                new[] { missingCandidate });

            Assert.That(resolved, Is.EqualTo(missingCandidate));
            Assert.That(Directory.Exists(missingCandidate), Is.False);
        }

        [Test]
        public void RuntimeScreenshotOptions_ParsesCapturePathAndQuitFlag()
        {
            var options = RuntimeScreenshotOptions.Parse(new[]
            {
                "player.exe", "--screenshot", @"C:\\capture\\ui.png", "--quit-after-screenshot"
            });

            Assert.That(options.IsCaptureRequested, Is.True);
            Assert.That(options.OutputPath, Is.EqualTo(@"C:\\capture\\ui.png"));
            Assert.That(options.QuitAfterCapture, Is.True);
            Assert.That(options.UsesFixedCaptureResolution, Is.True);
            Assert.That(RuntimeScreenshotOptions.CaptureWidth, Is.EqualTo(1920));
            Assert.That(RuntimeScreenshotOptions.CaptureHeight, Is.EqualTo(1080));
        }

        [Test]
        public void RuntimeScreenshotCapture_WarmsUpBeforeCapturingTheInitialVisualizationFrame()
        {
            var field = typeof(RuntimeScreenshotCapture).GetField("WarmupFrameCount", BindingFlags.Static | BindingFlags.Public);

            Assert.That(field, Is.Not.Null);
            Assert.That((int)field.GetValue(null), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void RuntimeScreenshotOptions_IgnoresMissingCaptureValue()
        {
            var options = RuntimeScreenshotOptions.Parse(new[] { "player.exe", "--screenshot" });

            Assert.That(options.IsCaptureRequested, Is.False);
            Assert.That(options.QuitAfterCapture, Is.False);
        }

        [Test]
        public void RuntimeScreenshotOptions_IgnoresQuitFlagWithoutCapturePath()
        {
            var options = RuntimeScreenshotOptions.Parse(new[] { "player.exe", "--quit-after-screenshot" });

            Assert.That(options.IsCaptureRequested, Is.False);
            Assert.That(options.QuitAfterCapture, Is.False);
        }

        [Test]
        public void RuntimeScreenshotOptions_IgnoresQuitFlagWhenCaptureValueIsMissing()
        {
            var options = RuntimeScreenshotOptions.Parse(new[]
            {
                "player.exe", "--screenshot", "--quit-after-screenshot"
            });

            Assert.That(options.IsCaptureRequested, Is.False);
            Assert.That(options.QuitAfterCapture, Is.False);
        }

        [Test]
        public void RuntimeDataSourceState_UsesDefaultSimulationForLaunchFlag()
        {
            RuntimeDataSourceState.UseCsvPath("telemetry.csv");

            try
            {
                var applied = RuntimeDataSourceState.ApplyCommandLineArguments(
                    new[] { "player.exe", "--simulation" });

                Assert.That(applied, Is.True);
                Assert.That(RuntimeDataSourceState.CurrentMode, Is.EqualTo(RuntimeDataSourceMode.Simulation));
                Assert.That(RuntimeDataSourceState.SimulationProfile, Is.Not.Null);
            }
            finally
            {
                RuntimeDataSourceState.UseCsvPath("telemetry.csv");
            }
        }

        [Test]
        public void RuntimeDataSourceState_AppliesLaunchCurrentToSimulationProfile()
        {
            RuntimeDataSourceState.UseCsvPath("telemetry.csv");

            try
            {
                var applied = RuntimeDataSourceState.ApplyCommandLineArguments(
                    new[] { "player.exe", "--simulation-current", "0.35", "-0.12" });

                Assert.That(applied, Is.True);
                Assert.That(RuntimeDataSourceState.CurrentMode, Is.EqualTo(RuntimeDataSourceMode.Simulation));
                Assert.That(RuntimeDataSourceState.SimulationProfile.OceanCurrentProfile.GetVelocity(80f),
                    Is.EqualTo(new Vector2(0.35f, -0.12f)));
            }
            finally
            {
                RuntimeDataSourceState.UseCsvPath("telemetry.csv");
            }
        }

        [Test]
        public void RuntimeDataSourceState_AppliesLaunchDepthToSimulationProfile()
        {
            RuntimeDataSourceState.UseCsvPath("telemetry.csv");

            try
            {
                var applied = RuntimeDataSourceState.ApplyCommandLineArguments(
                    new[] { "player.exe", "--simulation-depth", "1600" });

                Assert.That(applied, Is.True);
                Assert.That(RuntimeDataSourceState.SimulationProfile.TargetDepthM, Is.EqualTo(1600f));
                Assert.That(RuntimeDataSourceState.SimulationProfile.WaterColumnDepthM, Is.GreaterThanOrEqualTo(1600f));
                Assert.That(RuntimeDataSourceState.SimulationProfile.CycleDurationSeconds, Is.GreaterThanOrEqualTo(7800f));
            }
            finally
            {
                RuntimeDataSourceState.UseCsvPath("telemetry.csv");
            }
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.GetFullPath(directory);
        }

        private static string CreateModelsRoot()
        {
            var root = Path.Combine(CreateTempDirectory(), "Models");
            var xgboostDirectory = Path.Combine(root, "XGBoost");
            Directory.CreateDirectory(xgboostDirectory);
            File.WriteAllText(Path.Combine(xgboostDirectory, "manifest.json"), "{\"artifact_version\":1}");
            return Path.GetFullPath(root);
        }
    }
}
