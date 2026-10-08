using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnderwaterGliderTwin.Editor;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class BuildWindowsTests
    {
        private string tempDirectory;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(tempDirectory) && Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void PublishPreservesPreviousBuildWhenDestinationIsLocked()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "windows-build-publish-" + Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(tempDirectory, "UnderwaterGliderTwin");
            var staging = Path.Combine(tempDirectory, "UnderwaterGliderTwin.staging");
            Directory.CreateDirectory(destination);
            Directory.CreateDirectory(staging);

            var lockedPreviousFile = Path.Combine(destination, "locked.txt");
            File.WriteAllText(lockedPreviousFile, "previous");
            File.WriteAllText(Path.Combine(staging, "UnderwaterGliderTwin.exe"), "new");

            var publishMethod = typeof(BuildWindows).GetMethod(
                "PublishDirectoryWithRollback",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(publishMethod, Is.Not.Null);

            using (File.Open(lockedPreviousFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<TargetInvocationException>(() => publishMethod.Invoke(
                    null,
                    new object[] { staging, destination, null }));
            }

            Assert.That(File.ReadAllText(lockedPreviousFile), Is.EqualTo("previous"));
            Assert.That(File.Exists(Path.Combine(destination, "UnderwaterGliderTwin.exe")), Is.False);
            Assert.That(File.Exists(Path.Combine(staging, "UnderwaterGliderTwin.exe")), Is.True);
        }

        [Test]
        public void PublishReplacesPreviousBuildWhenDestinationIsAvailable()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "windows-build-publish-" + Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(tempDirectory, "UnderwaterGliderTwin");
            var staging = Path.Combine(tempDirectory, "UnderwaterGliderTwin.staging");
            Directory.CreateDirectory(destination);
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(destination, "old.txt"), "old");
            File.WriteAllText(Path.Combine(staging, "UnderwaterGliderTwin.exe"), "new");

            var publishMethod = typeof(BuildWindows).GetMethod(
                "PublishDirectoryWithRollback",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.DoesNotThrow(() => publishMethod.Invoke(
                null,
                new object[] { staging, destination, null }));

            Assert.That(File.ReadAllText(Path.Combine(destination, "UnderwaterGliderTwin.exe")), Is.EqualTo("new"));
            Assert.That(File.Exists(Path.Combine(destination, "old.txt")), Is.False);
        }
    }
}
