using System;
using System.Collections.Generic;
using UnderwaterGliderTwin.Telemetry;
using UnityEngine;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class RuntimeSmokeOptions
    {
        private RuntimeSmokeOptions(string localOceanCurrentPath, bool quitAfterCompletion)
        {
            LocalOceanCurrentPath = localOceanCurrentPath ?? string.Empty;
            QuitAfterCompletion = quitAfterCompletion && IsRequested;
        }

        public string LocalOceanCurrentPath { get; }
        public bool QuitAfterCompletion { get; }
        public bool IsRequested => !string.IsNullOrWhiteSpace(LocalOceanCurrentPath);

        public static RuntimeSmokeOptions Parse(IReadOnlyList<string> args)
        {
            var path = string.Empty;
            var quitAfterCompletion = false;

            if (args != null)
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var arg = args[i] ?? string.Empty;
                    if (arg.Equals("--quit-after-smoke", StringComparison.OrdinalIgnoreCase))
                    {
                        quitAfterCompletion = true;
                        continue;
                    }

                    if (!arg.Equals("--smoke-local-current", StringComparison.OrdinalIgnoreCase)
                        || i + 1 >= args.Count)
                    {
                        continue;
                    }

                    var candidate = args[i + 1] ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && !candidate.StartsWith("--", StringComparison.Ordinal))
                    {
                        path = candidate;
                        i++;
                    }
                }
            }

            return new RuntimeSmokeOptions(path, quitAfterCompletion);
        }
    }

    public static class RuntimeSmokeProfileUpdate
    {
        public static bool TryCreateLocalCurrentProfileUpdate(
            SimulationProfile baseProfile,
            string localPath,
            DateTime referenceTimeUtc,
            out SimulationProfile updatedProfile,
            out string actualSource,
            out string error)
        {
            updatedProfile = null;
            actualSource = string.Empty;
            if (!OceanCurrentFileLoader.TryLoad(localPath, referenceTimeUtc, out var result, out error))
            {
                return false;
            }

            updatedProfile = (baseProfile ?? SimulationProfile.Default).Clone();
            updatedProfile.OceanCurrentProfile = result.Profile?.Clone() ?? new OceanCurrentProfile();
            updatedProfile.OceanCurrentField = result.Field?.Clone() ?? new OceanCurrentField();
            updatedProfile.OceanCurrentSourcePreference = OceanCurrentSourcePreference.NetworkPreferred;
            actualSource = string.IsNullOrWhiteSpace(result.Source)
                ? "LocalFile"
                : result.Source;
            error = string.Empty;
            return true;
        }

        public static SimulationProfile BuildSmokeRebuildProfile(SimulationProfile baseProfile)
        {
            var profile = (baseProfile ?? SimulationProfile.Default).Clone();
            profile.CycleCount = 1;
            profile.CycleDurationSeconds = Math.Max(20f, profile.SampleIntervalSeconds * 4f);
            profile.SampleIntervalSeconds = Math.Max(1f, Mathf.Min(profile.SampleIntervalSeconds, 5f));
            profile.TargetDepthM = Math.Max(20f, Mathf.Min(profile.TargetDepthM, 120f));
            profile.WaterColumnDepthM = Math.Max(profile.TargetDepthM + 20f, profile.WaterColumnDepthM);
            return profile;
        }
    }
}
