using System;
using System.Linq;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class CopernicusCurrentResponseParser
    {
        public static string Serialize(CopernicusCurrentResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var payload = new CopernicusCurrentPayload
            {
                source = result.Source,
                datasetId = result.DatasetId,
                retrievedAtUtc = result.RetrievedAtUtc,
                layers = Array.ConvertAll(result.Profile.Layers.ToArray(), layer => new CopernicusCurrentLayerPayload
                {
                    minDepthM = layer.MinDepthM,
                    maxDepthM = layer.MaxDepthM,
                    eastwardMps = layer.EastwardMps,
                    northwardMps = layer.NorthwardMps
                }),
                fieldSamples = Array.ConvertAll(result.Field.Samples.ToArray(), sample => new CopernicusCurrentFieldSamplePayload
                {
                    longitudeDeg = sample.LongitudeDeg,
                    latitudeDeg = sample.LatitudeDeg,
                    depthM = sample.DepthM,
                    elapsedSeconds = sample.ElapsedSeconds,
                    eastwardMps = sample.EastwardMps,
                    northwardMps = sample.NorthwardMps,
                    verticalMps = sample.VerticalMps
                })
            };
            return JsonUtility.ToJson(payload);
        }

        public static CopernicusCurrentResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Copernicus current response is empty.", nameof(json));
            }

            var payload = JsonUtility.FromJson<CopernicusCurrentPayload>(json);
            if (payload == null || payload.layers == null || payload.layers.Length == 0)
            {
                throw new ArgumentException("Copernicus current response contains no layers.", nameof(json));
            }

            if (payload.fieldSamples == null || payload.fieldSamples.Length == 0)
            {
                throw new ArgumentException("Copernicus current response contains no spatial field samples.", nameof(json));
            }

            var layers = new OceanCurrentLayer[payload.layers.Length];
            for (var index = 0; index < payload.layers.Length; index++)
            {
                var layer = payload.layers[index];
                layers[index] = new OceanCurrentLayer(
                    layer.minDepthM,
                    layer.maxDepthM,
                    layer.eastwardMps,
                    layer.northwardMps);
            }

            var fieldSamples = Array.ConvertAll(payload.fieldSamples, sample => new OceanCurrentFieldSample(
                    sample.longitudeDeg,
                    sample.latitudeDeg,
                    sample.depthM,
                    sample.elapsedSeconds,
                    sample.eastwardMps,
                    sample.northwardMps,
                    sample.verticalMps));

            return new CopernicusCurrentResult(
                payload.source,
                payload.datasetId,
                payload.retrievedAtUtc,
                new OceanCurrentProfile(layers),
                new OceanCurrentField(fieldSamples));
        }

        [Serializable]
        private sealed class CopernicusCurrentPayload
        {
            public string source;
            public string datasetId;
            public string retrievedAtUtc;
            public CopernicusCurrentLayerPayload[] layers;
            public CopernicusCurrentFieldSamplePayload[] fieldSamples;
        }

        [Serializable]
        private sealed class CopernicusCurrentLayerPayload
        {
            public float minDepthM;
            public float maxDepthM;
            public float eastwardMps;
            public float northwardMps;
        }

        [Serializable]
        private sealed class CopernicusCurrentFieldSamplePayload
        {
            public double longitudeDeg;
            public double latitudeDeg;
            public float depthM;
            public float elapsedSeconds;
            public float eastwardMps;
            public float northwardMps;
            public float verticalMps;
        }
    }

    public sealed class CopernicusCurrentResult
    {
        public CopernicusCurrentResult(
            string source,
            string datasetId,
            string retrievedAtUtc,
            OceanCurrentProfile profile,
            OceanCurrentField field = null)
        {
            Source = source ?? string.Empty;
            DatasetId = datasetId ?? string.Empty;
            RetrievedAtUtc = retrievedAtUtc ?? string.Empty;
            Profile = profile ?? new OceanCurrentProfile();
            Field = field ?? new OceanCurrentField();
        }

        public string Source { get; }

        public string DatasetId { get; }

        public string RetrievedAtUtc { get; }

        public OceanCurrentProfile Profile { get; }

        public OceanCurrentField Field { get; }
    }
}
