using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class CopernicusCurrentCache
    {
        private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(6);

        public static string BuildCacheKey(CopernicusCurrentRequest request, DateTime? referenceTimeUtc = null, OceanCurrentSourceIdentity sourceIdentity = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var material = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3:yyyyMMddHH}|{4:R}|{5:R}|{6:R}|{7:R}|{8:R}|{9:R}|{10:R}|{11:R}|{12}|{13}",
                request.providerSchemaVersion ?? "1",
                request.DatasetId,
                request.variableMapping ?? "uo,vo,w",
                (referenceTimeUtc ?? DateTime.UtcNow).ToUniversalTime(),
                request.minimumLongitudeDeg, request.maximumLongitudeDeg,
                request.minimumLatitudeDeg, request.maximumLatitudeDeg,
                request.minDepthM, request.maxDepthM, request.prefetchHalfWidthKm, request.forecastHours,
                request.timePolicy ?? "nearest",
                sourceIdentity == null ? request.sourceCacheToken ?? string.Empty : sourceIdentity.CacheToken);
            return OceanCurrentSourceIdentity.Sha256(material);
        }

        public static bool TryLoad(CopernicusCurrentRequest request, out CopernicusCurrentResult result)
        {
            return TryLoad(request, DateTime.UtcNow, null, out result);
        }

        public static bool TryLoad(CopernicusCurrentRequest request, DateTime referenceTimeUtc, OceanCurrentSourceIdentity sourceIdentity, out CopernicusCurrentResult result)
        {
            result = null;
            if (request == null) return false;

            try
            {
                var cachePath = GetCachePath(request, referenceTimeUtc, sourceIdentity);
                if (!File.Exists(cachePath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) > MaximumAge)
                {
                    return false;
                }

                var cached = CopernicusCurrentResponseParser.Parse(File.ReadAllText(cachePath));
                result = new CopernicusCurrentResult(
                    string.IsNullOrWhiteSpace(cached.Source) ? "Copernicus Marine (local cache)" : cached.Source + " (local cache)",
                    cached.DatasetId,
                    cached.RetrievedAtUtc,
                    cached.Profile,
                    cached.Field);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Store(CopernicusCurrentRequest request, string responseJson)
        {
            Store(request, responseJson, DateTime.UtcNow, null);
        }

        public static void Store(CopernicusCurrentRequest request, string responseJson, DateTime referenceTimeUtc, OceanCurrentSourceIdentity sourceIdentity)
        {
            if (request == null || string.IsNullOrWhiteSpace(responseJson)) return;

            string temporaryPath = null;
            string backupPath = null;
            try
            {
                var cachePath = GetCachePath(request, referenceTimeUtc, sourceIdentity);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                // Parse before publishing: incomplete or invalid cache data must never replace a good entry.
                CopernicusCurrentResponseParser.Parse(responseJson);
                temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporaryPath, responseJson);
                if (File.Exists(cachePath))
                {
                    backupPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".bak";
                    File.Replace(temporaryPath, cachePath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, cachePath);
                }
            }
            catch (Exception)
            {
                // A cache write must never make an otherwise valid current lookup fail.
            }
            finally
            {
                TryDelete(temporaryPath);
                TryDelete(backupPath);
            }
        }

        private static string GetCachePath(CopernicusCurrentRequest request, DateTime referenceTimeUtc, OceanCurrentSourceIdentity sourceIdentity)
        {
            return Path.Combine(Application.persistentDataPath, "CopernicusCurrentCache", BuildCacheKey(request, referenceTimeUtc, sourceIdentity) + ".json");
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); } catch (IOException) { }
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
