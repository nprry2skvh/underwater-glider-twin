using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnderwaterGliderTwin.Prediction
{
    [Serializable]
    public sealed class XGBoostFeatureSchema
    {
        public int history_length;
        public int sample_interval_seconds;
        public int[] horizons_seconds;
        public string[] feature_names;
        public float[] mean;
        public float[] scale;

        public int HistoryLength => history_length;
        public int SampleIntervalSeconds => sample_interval_seconds;
        public int[] HorizonsSeconds => horizons_seconds ?? Array.Empty<int>();
        public string[] FeatureNames => feature_names ?? Array.Empty<string>();
    }

    public sealed class XGBoostArtifact
    {
        private const int SupportedArtifactVersion = 1;
        private readonly Dictionary<string, string> outputSources;
        private readonly Dictionary<string, XGBoostTreeModel> models;
        private readonly XGBoostFeatureSchema schema;

        private XGBoostArtifact(
            string directory,
            Dictionary<string, string> outputSources,
            Dictionary<string, XGBoostTreeModel> models,
            XGBoostFeatureSchema schema)
        {
            Directory = directory;
            this.outputSources = outputSources;
            this.models = models;
            this.schema = schema;
        }

        public string Directory { get; }
        public XGBoostFeatureSchema Schema => schema;

        public string GetOutputSource(string target)
        {
            return outputSources.TryGetValue(target ?? string.Empty, out var source) ? source : string.Empty;
        }

        public XGBoostTreeModel GetModel(string target)
        {
            return models.TryGetValue(target ?? string.Empty, out var model) ? model : null;
        }

        public static bool TryLoad(string directory, out XGBoostArtifact artifact, out string error)
        {
            artifact = null;
            error = string.Empty;
            var manifestPath = Path.Combine(directory ?? string.Empty, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                error = "XGBoost manifest.json is missing.";
                return false;
            }

            var manifest = File.ReadAllText(manifestPath);
            var hasCurrentSchema = Regex.IsMatch(manifest, "\\\"artifact_schema_version\\\"\\s*:\\s*" + SupportedArtifactVersion)
                && Regex.IsMatch(manifest, "\\\"validation_status\\\"\\s*:\\s*\\\"accepted\\\"", RegexOptions.IgnoreCase);
            var hasLegacySchema = Regex.IsMatch(manifest, "\\\"artifact_version\\\"\\s*:\\s*" + SupportedArtifactVersion);
            if (!hasCurrentSchema && !hasLegacySchema)
            {
                error = "XGBoost artifact manifest is unsupported or not accepted.";
                return false;
            }

            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            var reportPath = Path.Combine(directory, "validation_report.json");
            var report = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            var matches = Regex.Matches(report, "\\\"(?<target>[a-z_]+)\\\"\\s*:\\s*\\\"(?<source>xgboost|stable)\\\"");
            if (matches.Count == 0)
            {
                matches = Regex.Matches(manifest, "\\\"(?<target>[a-z_]+)\\\"\\s*:\\s*\\\"(?<source>xgboost|stable)\\\"");
            }
            foreach (Match match in matches)
            {
                sources[match.Groups["target"].Value] = match.Groups["source"].Value;
            }

            if (sources.Count == 0)
            {
                error = "XGBoost artifact output_sources are missing.";
                return false;
            }

            XGBoostFeatureSchema schema = null;
            var schemaPath = Path.Combine(directory, "feature_schema.json");
            if (File.Exists(schemaPath))
            {
                schema = JsonUtility.FromJson<XGBoostFeatureSchema>(File.ReadAllText(schemaPath));
                if (schema?.feature_names == null || schema.feature_names.Length == 0
                    || schema.mean == null || schema.scale == null
                    || schema.mean.Length != schema.feature_names.Length
                    || schema.scale.Length != schema.feature_names.Length)
                {
                    error = "XGBoost feature schema is invalid.";
                    return false;
                }
            }

            var models = new Dictionary<string, XGBoostTreeModel>(StringComparer.Ordinal);
            var modelMatches = Regex.Matches(manifest, @"models/(?<target>[a-z_]+)\.json");
            foreach (Match match in modelMatches)
            {
                var target = match.Groups["target"].Value;
                var modelPath = Path.Combine(directory, "models", target + ".json");
                if (!File.Exists(modelPath))
                {
                    error = "XGBoost model is missing: " + target;
                    return false;
                }

                var model = JsonUtility.FromJson<XGBoostTreeModel>(File.ReadAllText(modelPath));
                if (model?.trees == null || model.trees.Length == 0)
                {
                    error = "XGBoost model is invalid: " + target;
                    return false;
                }

                models[target] = model;
            }

            artifact = new XGBoostArtifact(directory, sources, models, schema);
            return true;
        }
    }
}
