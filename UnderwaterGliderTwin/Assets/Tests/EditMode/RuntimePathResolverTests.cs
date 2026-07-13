using System;
using System.IO;
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
        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
