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

        [Test]
        public void TryRead_UsesAliasesAndConvertsCentimetersPerSecondAndKnots()
        {
            var path = WriteFixture(BuildGridFixture("lon", "lat", "u", "v", "cm/s", "knot", new[] { 120f }, new[] { 25f }, new[] { 5f }, new[] { 25f }, new[] { 1f }));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out var result, out var error), Is.True, error);
                Assert.That(result.Field.Samples[0].EastwardMps, Is.EqualTo(.25f).Within(.0001f));
                Assert.That(result.Field.Samples[0].NorthwardMps, Is.EqualTo(.514444f).Within(.0001f));
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void TryRead_SortsDescendingCoordinatesWithoutSeparatingVelocityComponents()
        {
            var path = WriteFixture(BuildGridFixture("longitude", "latitude", "uo", "vo", "m/s", "m/s", new[] { 121f, 120f }, new[] { 26f, 25f }, new[] { 10f, 5f }, new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f }, new[] { -1f, -2f, -3f, -4f, -5f, -6f, -7f, -8f }));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out var result, out var error), Is.True, error);
                var first = result.Field.Samples[0];
                Assert.That(first.LongitudeDeg, Is.EqualTo(120d));
                Assert.That(first.LatitudeDeg, Is.EqualTo(25d));
                Assert.That(first.DepthM, Is.EqualTo(5f));
                Assert.That(first.EastwardMps, Is.EqualTo(8f));
                Assert.That(first.NorthwardMps, Is.EqualTo(-8f));
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void TryRead_SelectsNearestCfTimeSlice()
        {
            var path = WriteFixture(BuildTimedFixture("hours since 2026-07-28T00:00:00Z", new[] { 0f, 2f }, new[] { 1f, 9f }, new[] { -1f, -9f }));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, new DateTime(2026, 7, 28, 1, 40, 0, DateTimeKind.Utc), out var result, out var error), Is.True, error);
                Assert.That(result.Field.Samples[0].EastwardMps, Is.EqualTo(9f));
                Assert.That(result.Field.Samples[0].NorthwardMps, Is.EqualTo(-9f));
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [TestCase(null)]
        [TestCase("hours since not-a-date")]
        public void TryRead_RejectsMissingOrInvalidTimeUnits(string units)
        {
            var path = WriteFixture(BuildTimedFixture(units, new[] { 0f, 1f }, new[] { 1f, 2f }, new[] { 1f, 2f }));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out _, out var error), Is.False);
                Assert.That(error, Does.Contain("time"));
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void TryRead_RejectsMismatchedVelocityDimensions()
        {
            var variables = new List<FixtureVariable>
            {
                new FixtureVariable("longitude", new[] { 0 }, new[] { 120f }), new FixtureVariable("latitude", new[] { 1 }, new[] { 25f }), new FixtureVariable("depth", new[] { 2 }, new[] { 5f }),
                new FixtureVariable("uo", new[] { 2, 1, 0 }, new[] { 1f }), new FixtureVariable("vo", new[] { 2, 0, 1 }, new[] { 1f })
            };
            var path = WriteFixture(Build(1, new[] { new FixtureDimension("longitude", 1), new FixtureDimension("latitude", 1), new FixtureDimension("depth", 1) }, variables));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out _, out var error), Is.False);
                Assert.That(error, Does.Contain("same dimensions"));
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        [Test]
        public void TryRead_RejectsMissingCoordinateMappingInsteadOfGuessingLengths()
        {
            var path = WriteFixture(BuildGridFixture("x_unknown", "y_unknown", "uo", "vo", "m/s", "m/s", new[] { 120f }, new[] { 25f }, new[] { 5f }, new[] { 1f }, new[] { 1f }));
            try
            {
                Assert.That(NetCdfClassicCurrentReader.TryRead(path, DateTime.UtcNow, out _, out var error), Is.False);
                Assert.That(error, Does.Contain("longitude"));
            }
            finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        }

        private static string WriteFixture(byte[] bytes)
        {
            var directory = Path.Combine(Path.GetTempPath(), "ocean-current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); var path = Path.Combine(directory, "field.nc"); File.WriteAllBytes(path, bytes); return path;
        }

        private static byte[] BuildFixture(byte version)
        {
            var variables = new List<FixtureVariable>
            {
                new FixtureVariable("longitude", new[] { 0 }, new[] { 120f }), new FixtureVariable("latitude", new[] { 1 }, new[] { 25f }),
                new FixtureVariable("depth", new[] { 2 }, new[] { 5f }), new FixtureVariable("uo", new[] { 2, 1, 0 }, new[] { .25f }), new FixtureVariable("vo", new[] { 2, 1, 0 }, new[] { -.5f })
            };
            return Build( version, new[] { new FixtureDimension("longitude", 1), new FixtureDimension("latitude", 1), new FixtureDimension("depth", 1) }, variables);
        }

        private static byte[] BuildGridFixture(string longitudeName, string latitudeName, string uName, string vName, string uUnits, string vUnits, float[] longitude, float[] latitude, float[] depth, float[] u, float[] v)
        {
            return Build(1, new[] { new FixtureDimension(longitudeName, longitude.Length), new FixtureDimension(latitudeName, latitude.Length), new FixtureDimension("depth", depth.Length) }, new List<FixtureVariable>
            {
                new FixtureVariable(longitudeName, new[] { 0 }, longitude), new FixtureVariable(latitudeName, new[] { 1 }, latitude), new FixtureVariable("depth", new[] { 2 }, depth),
                new FixtureVariable(uName, new[] { 2, 1, 0 }, u, uUnits), new FixtureVariable(vName, new[] { 2, 1, 0 }, v, vUnits)
            });
        }

        private static byte[] BuildTimedFixture(string timeUnits, float[] time, float[] u, float[] v)
        {
            return Build(1, new[] { new FixtureDimension("longitude", 1), new FixtureDimension("latitude", 1), new FixtureDimension("depth", 1), new FixtureDimension("time", time.Length) }, new List<FixtureVariable>
            {
                new FixtureVariable("longitude", new[] { 0 }, new[] { 120f }), new FixtureVariable("latitude", new[] { 1 }, new[] { 25f }), new FixtureVariable("depth", new[] { 2 }, new[] { 5f }), new FixtureVariable("time", new[] { 3 }, time, timeUnits),
                new FixtureVariable("uo", new[] { 3, 2, 1, 0 }, u, "m/s"), new FixtureVariable("vo", new[] { 3, 2, 1, 0 }, v, "m/s")
            });
        }

        private static byte[] Build(byte version, FixtureDimension[] dimensions, List<FixtureVariable> variables)
        {
            using (var initial = new MemoryStream())
            {
                WriteHeader(initial, version, dimensions, variables);
                var offset = initial.Length;
                foreach (var variable in variables) { variable.Begin = offset; offset += variable.Values.Length * 4; }
            }
            using (var stream = new MemoryStream())
            {
                WriteHeader(stream, version, dimensions, variables);
                foreach (var variable in variables) foreach (var value in variable.Values) WriteSingle(stream, value);
                return stream.ToArray();
            }
        }

        private static void WriteHeader(Stream stream, byte version, FixtureDimension[] dimensions, List<FixtureVariable> variables)
        {
            stream.WriteByte((byte)'C'); stream.WriteByte((byte)'D'); stream.WriteByte((byte)'F'); stream.WriteByte(version); WriteInt(stream, 0);
            WriteInt(stream, 10); WriteInt(stream, dimensions.Length); foreach (var dimension in dimensions) { WriteName(stream, dimension.Name); WriteInt(stream, dimension.Length); }
            WriteInt(stream, 0); WriteInt(stream, 11); WriteInt(stream, variables.Count);
            foreach (var variable in variables)
            {
                WriteName(stream, variable.Name); WriteInt(stream, variable.Dimensions.Length); foreach (var dimension in variable.Dimensions) WriteInt(stream, dimension);
                WriteAttributes(stream, variable.Units); WriteInt(stream, 5); WriteInt(stream, variable.Values.Length * 4);
                if (version == 2) { WriteInt(stream, 0); WriteInt(stream, (int)variable.Begin); } else WriteInt(stream, (int)variable.Begin);
            }
        }

        private static void WriteAttributes(Stream stream, string units)
        {
            if (string.IsNullOrEmpty(units)) { WriteInt(stream, 0); return; }
            WriteInt(stream, 12); WriteInt(stream, 1); WriteName(stream, "units"); WriteInt(stream, 2); WriteInt(stream, units.Length); foreach (var value in units) stream.WriteByte((byte)value); while (stream.Length % 4 != 0) stream.WriteByte(0);
        }

        private static void WriteName(Stream stream, string value) { WriteInt(stream, value.Length); foreach (var character in value) stream.WriteByte((byte)character); while (stream.Length % 4 != 0) stream.WriteByte(0); }
        private static void WriteInt(Stream stream, int value) { stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
        private static void WriteSingle(Stream stream, float value) { WriteInt(stream, BitConverter.SingleToInt32Bits(value)); }
        private sealed class FixtureDimension { public FixtureDimension(string name, int length) { Name = name; Length = length; } public string Name; public int Length; }
        private sealed class FixtureVariable { public FixtureVariable(string name, int[] dimensions, float[] values, string units = null) { Name = name; Dimensions = dimensions; Values = values; Units = units; } public string Name; public int[] Dimensions; public float[] Values; public string Units; public long Begin; }
    }
}
