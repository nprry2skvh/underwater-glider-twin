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

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
