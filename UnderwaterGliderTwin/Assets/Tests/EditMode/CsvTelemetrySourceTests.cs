using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CsvTelemetrySourceTests
    {
        [Test]
        public void Load_ParsesKnownColumnsFromGbkCsv()
        {
            var path = Path.Combine(Application.temporaryCachePath, "sample-telemetry.csv");
            var header = string.Join(",", Enumerable.Range(0, 51).Select(i => $"col{i}"));
            var row = new[]
            {
                "000年00时00分09秒", "1", "28.5", "0.3", "0", "32", "10", "0", "7", "88", "3", "0", "0",
                "水面模式", "水面", "2", "0", "32", "17", "0", "16", "120.00008333", "25.00001728", "2.3",
                "100.0", "30.4", "-2", "-5", "0", "30", "44.3", "1000", "500", "29.7", "68.6", "2.931",
                "26.4086", "2.35", "0", "27", "0", "95", "89", "0", "5241", "0", "0", "31162", "0", "0.0", "1576"
            };
            File.WriteAllText(path, header + "\n" + string.Join(",", row) + "\n", Encoding.GetEncoding(936));

            var result = new CsvTelemetrySource(path).Load();

            Assert.That(result.Frames, Has.Count.EqualTo(1));
            Assert.That(result.SkippedRows, Is.EqualTo(0));
            var frame = result.Frames[0];
            Assert.That(frame.RawTime, Is.EqualTo("000年00时00分09秒"));
            Assert.That(frame.ElapsedSeconds, Is.EqualTo(9f).Within(0.001f));
            Assert.That(frame.LongitudeDeg, Is.EqualTo(120.00008333).Within(0.00000001));
            Assert.That(frame.LatitudeDeg, Is.EqualTo(25.00001728).Within(0.00000001));
            Assert.That(frame.DepthM, Is.EqualTo(2.3f).Within(0.0001f));
            Assert.That(frame.HeadingDeg, Is.EqualTo(30.4f).Within(0.0001f));
            Assert.That(frame.PitchDeg, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(frame.RollDeg, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(frame.WorkMode, Is.EqualTo("水面模式"));
            Assert.That(frame.RunState, Is.EqualTo("水面"));
            Assert.That(frame.BatteryPercent, Is.EqualTo(95f).Within(0.0001f));
        }

        [Test]
        public void Load_SkipsRowsWithInvalidNumbers()
        {
            var path = Path.Combine(Application.temporaryCachePath, "bad-telemetry.csv");
            var header = string.Join(",", Enumerable.Range(0, 51).Select(i => $"col{i}"));
            var row = new[]
            {
                "bad", "1", "28.5", "0.3", "0", "32", "10", "0", "7", "88", "3", "0", "0",
                "水面模式", "水面", "2", "0", "32", "17", "0", "16", "not-a-number", "25.00001728", "2.3",
                "100.0", "30.4", "-2", "-5", "0", "30", "44.3", "1000", "500", "29.7", "68.6", "2.931",
                "26.4086", "2.35", "0", "27", "0", "95", "89", "0", "5241", "0", "0", "31162", "0", "0.0", "1576"
            };
            File.WriteAllText(path, header + "\n" + string.Join(",", row) + "\n", Encoding.GetEncoding(936));

            var result = new CsvTelemetrySource(path).Load();

            Assert.That(result.Frames, Has.Count.EqualTo(0));
            Assert.That(result.SkippedRows, Is.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("line 2"));
        }
    }
}
