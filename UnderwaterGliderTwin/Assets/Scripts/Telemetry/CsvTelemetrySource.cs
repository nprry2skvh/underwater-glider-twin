using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class CsvTelemetrySource : ITelemetrySource
    {
        private const int ExpectedColumnCount = 51;
        private static readonly Regex TimeNumberPattern = new Regex(@"-?\d+", RegexOptions.Compiled);
        private readonly string path;

        public CsvTelemetrySource(string path)
        {
            this.path = path;
        }

        public TelemetryLoadResult Load()
        {
            var frames = new List<TelemetryFrame>(240000);
            var errors = new List<string>();
            var skipped = 0;

            using var reader = new StreamReader(path, GetGbkEncoding(), true);
            _ = reader.ReadLine();
            var lineNumber = 1;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var columns = line.Split(',');
                if (columns.Length < ExpectedColumnCount)
                {
                    skipped++;
                    errors.Add($"Skipped line {lineNumber}: expected {ExpectedColumnCount} columns, got {columns.Length}");
                    continue;
                }

                try
                {
                    frames.Add(new TelemetryFrame(
                        rowIndex: lineNumber - 2,
                        rawTime: columns[0],
                        elapsedSeconds: ParseElapsedSeconds(columns[0]),
                        longitudeDeg: ParseDouble(columns[21]),
                        latitudeDeg: ParseDouble(columns[22]),
                        depthM: ParseFloat(columns[23]),
                        altitudeM: ParseFloat(columns[24]),
                        headingDeg: ParseFloat(columns[25]),
                        pitchDeg: ParseFloat(columns[26]),
                        rollDeg: ParseFloat(columns[27]),
                        voltage24V: ParseFloat(columns[2]),
                        current24A: ParseFloat(columns[3]),
                        batteryPercent: ParseFloat(columns[41]),
                        workMode: columns[13],
                        runState: columns[14],
                        targetSegment: ParseFloat(columns[29]),
                        targetHeadingDeg: ParseFloat(columns[30]),
                        targetDepthM: ParseFloat(columns[31]),
                        targetAltitudeM: ParseFloat(columns[32]),
                        propellerRpm: ParseFloat(columns[28]),
                        pistonMm: ParseFloat(columns[18]),
                        turnAngleDeg: ParseFloat(columns[17])));
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    skipped++;
                    errors.Add($"Skipped line {lineNumber}: {ex.Message}");
                }
            }

            return new TelemetryLoadResult(frames, errors, skipped);
        }

        private static Encoding GetGbkEncoding()
        {
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch (ArgumentException)
            {
                return Encoding.Default;
            }
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string value)
        {
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static float ParseElapsedSeconds(string rawTime)
        {
            var matches = TimeNumberPattern.Matches(rawTime ?? string.Empty);
            if (matches.Count != 4)
            {
                throw new FormatException($"Could not parse telemetry time '{rawTime}'.");
            }

            var days = int.Parse(matches[0].Value, CultureInfo.InvariantCulture);
            var hours = int.Parse(matches[1].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(matches[2].Value, CultureInfo.InvariantCulture);
            var seconds = int.Parse(matches[3].Value, CultureInfo.InvariantCulture);
            return ((days * 24f + hours) * 60f + minutes) * 60f + seconds;
        }
    }
}
