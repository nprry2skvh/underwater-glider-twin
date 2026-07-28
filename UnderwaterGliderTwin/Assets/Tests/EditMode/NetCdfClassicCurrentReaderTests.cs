using System;
using System.IO;
using NUnit.Framework;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class NetCdfClassicCurrentReaderTests
    {
        [Test]
        public void TryRead_ReportsNetCdf4AsUnsupportedSoTheConverterSeamCanHandleIt()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.nc");
            File.WriteAllBytes(path, new byte[] { 0x89, (byte)'H', (byte)'D', (byte)'F' });
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out _, out var error), Is.False);
                Assert.That(NetCdfClassicCurrentReader.IsUnsupportedFormat(error), Is.True, error);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
