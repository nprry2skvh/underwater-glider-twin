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

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add($"CSV file does not exist: {path}");
                return new TelemetryLoadResult(frames, errors, skipped);
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
                if (!IsWellFormedGbk(bytes, out var invalidIndex))
                {
                    errors.Add($"Could not decode CSV as GBK: invalid byte sequence at byte {invalidIndex}.");
                    return new TelemetryLoadResult(frames, errors, skipped);
                }
            }
            catch (IOException ex)
            {
                errors.Add($"Could not read CSV: {ex.Message}");
                return new TelemetryLoadResult(frames, errors, skipped);
            }

            string content;
            try
            {
                content = GetStrictGbkEncoding().GetString(bytes);
            }
            catch (DecoderFallbackException ex)
            {
                errors.Add($"Could not decode CSV as GBK: {ex.Message}");
                return new TelemetryLoadResult(frames, errors, skipped);
            }
            if (content.IndexOf('\uFFFD') >= 0)
            {
                errors.Add("Could not decode CSV as GBK: invalid byte sequence was replaced.");
                return new TelemetryLoadResult(frames, errors, skipped);
            }

            using var reader = new StringReader(content);
            var header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header) || header.Split(',').Length < ExpectedColumnCount)
            {
                errors.Add($"CSV header must contain at least {ExpectedColumnCount} columns.");
                return new TelemetryLoadResult(frames, errors, skipped);
            }
            var lineNumber = 1;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
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

            if (frames.Count == 0)
            {
                errors.Add("CSV did not contain any usable telemetry frames.");
            }

            return new TelemetryLoadResult(frames, errors, skipped);
        }

        private static Encoding GetStrictGbkEncoding()
        {
            return Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        private static bool IsWellFormedGbk(byte[] bytes, out int invalidIndex)
        {
            invalidIndex = -1;
            for (var i = 0; i < bytes.Length; i++)
            {
                var current = bytes[i];
                if (current <= 0x80)
                {
                    continue;
                }

                if (current < 0x81 || current > 0xFE || i + 1 >= bytes.Length)
                {
                    invalidIndex = i;
                    return false;
                }

                var trail = bytes[++i];
                if (trail < 0x40 || trail == 0x7F || trail > 0xFE)
                {
                    invalidIndex = i - 1;
                    return false;
                }
            }

            return true;
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
