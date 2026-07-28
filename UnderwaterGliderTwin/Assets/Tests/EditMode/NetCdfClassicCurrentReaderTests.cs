using System;
using System.IO;
using System.Collections.Generic;
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

        [TestCase((byte)1)]
        [TestCase((byte)2)]
        public void TryRead_ReadsClassicAnd64BitOffsetFixtures(byte version)
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "field.nc");
            File.WriteAllBytes(path, BuildFixture(version));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out var result, out var error), Is.True, error);
                Assert.That(result.Field.Samples.Count, Is.EqualTo(1));
                Assert.That(result.Field.Samples[0].EastwardMps, Is.EqualTo(.25f).Within(.0001f));
                Assert.That(result.Field.Samples[0].NorthwardMps, Is.EqualTo(-.5f).Within(.0001f));
            }
            finally { Directory.Delete(directory, true); }
        }

        private static byte[] BuildFixture(byte version)
        {
            var variables = new List<FixtureVariable>
            {
                new FixtureVariable("longitude", new[] { 0 }, 120f), new FixtureVariable("latitude", new[] { 1 }, 25f),
                new FixtureVariable("depth", new[] { 2 }, 5f), new FixtureVariable("uo", new[] { 2, 1, 0 }, .25f), new FixtureVariable("vo", new[] { 2, 1, 0 }, -.5f)
            };
            using (var initial = new MemoryStream())
            {
                WriteHeader(initial, version, variables);
                var offset = initial.Length;
                foreach (var variable in variables) { variable.Begin = offset; offset += 4; }
            }
            using (var stream = new MemoryStream())
            {
                WriteHeader(stream, version, variables);
                foreach (var variable in variables) WriteSingle(stream, variable.Value);
                return stream.ToArray();
            }
        }

        private static void WriteHeader(Stream stream, byte version, List<FixtureVariable> variables)
        {
            stream.WriteByte((byte)'C'); stream.WriteByte((byte)'D'); stream.WriteByte((byte)'F'); stream.WriteByte(version); WriteInt(stream, 0);
            WriteInt(stream, 10); WriteInt(stream, 3);
            WriteName(stream, "longitude"); WriteInt(stream, 1); WriteName(stream, "latitude"); WriteInt(stream, 1); WriteName(stream, "depth"); WriteInt(stream, 1);
            WriteInt(stream, 0); WriteInt(stream, 11); WriteInt(stream, variables.Count);
            foreach (var variable in variables)
            {
                WriteName(stream, variable.Name); WriteInt(stream, variable.Dimensions.Length); foreach (var dimension in variable.Dimensions) WriteInt(stream, dimension);
                WriteInt(stream, 0); WriteInt(stream, 5); WriteInt(stream, 4);
                if (version == 2) { WriteInt(stream, 0); WriteInt(stream, (int)variable.Begin); } else WriteInt(stream, (int)variable.Begin);
            }
        }

        private static void WriteName(Stream stream, string value) { WriteInt(stream, value.Length); foreach (var character in value) stream.WriteByte((byte)character); while (stream.Length % 4 != 0) stream.WriteByte(0); }
        private static void WriteInt(Stream stream, int value) { stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
        private static void WriteSingle(Stream stream, float value) { WriteInt(stream, BitConverter.SingleToInt32Bits(value)); }
        private sealed class FixtureVariable { public FixtureVariable(string name, int[] dimensions, float value) { Name = name; Dimensions = dimensions; Value = value; } public string Name; public int[] Dimensions; public float Value; public long Begin; }
    }
}
