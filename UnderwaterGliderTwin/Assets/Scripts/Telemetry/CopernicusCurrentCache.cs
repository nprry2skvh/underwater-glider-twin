using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class CopernicusCurrentCache
    {
        private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(6);

        public static string BuildCacheKey(CopernicusCurrentRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}_{1:F4}_{2:F4}_{3:F0}_{4:F0}_{5:F3}_{6:F3}_{7:F1}",
                Sanitize(request.DatasetId),
                request.longitudeDeg,
                request.latitudeDeg,
                request.minDepthM,
                request.maxDepthM,
                request.minimumLongitudeDeg,
                request.maximumLatitudeDeg,
                request.forecastHours);
        }

        public static bool TryLoad(CopernicusCurrentRequest request, out CopernicusCurrentResult result)
        {
            result = null;
            if (request == null) return false;

            try
            {
                var cachePath = GetCachePath(request);
                if (!File.Exists(cachePath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) > MaximumAge)
                {
                    return false;
                }

                var cached = CopernicusCurrentResponseParser.Parse(File.ReadAllText(cachePath));
                result = new CopernicusCurrentResult(
                    string.IsNullOrWhiteSpace(cached.Source) ? "Copernicus Marine (local cache)" : cached.Source + " (local cache)",
                    cached.DatasetId,
                    cached.RetrievedAtUtc,
                    cached.Profile);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Store(CopernicusCurrentRequest request, string responseJson)
        {
            if (request == null || string.IsNullOrWhiteSpace(responseJson)) return;

            try
            {
                var cachePath = GetCachePath(request);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                var temporaryPath = cachePath + ".tmp";
                File.WriteAllText(temporaryPath, responseJson);
                File.Copy(temporaryPath, cachePath, true);
                File.Delete(temporaryPath);
            }
            catch (Exception)
            {
                // A cache write must never make an otherwise valid current lookup fail.
            }
        }

        private static string GetCachePath(CopernicusCurrentRequest request)
        {
            return Path.Combine(Application.persistentDataPath, "CopernicusCurrentCache", BuildCacheKey(request) + ".json");
        }

        private static string Sanitize(string value)
        {
            var source = string.IsNullOrWhiteSpace(value) ? CopernicusCurrentRequest.DefaultDatasetId : value;
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                source = source.Replace(invalidCharacter, '_');
            }

            return source;
        }
    }
}
