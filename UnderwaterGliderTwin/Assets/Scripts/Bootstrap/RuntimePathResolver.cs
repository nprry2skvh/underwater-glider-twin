using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Bootstrap
{
    public static class RuntimePathResolver
    {
        public static string ResolveCsvPath()
        {
            return ResolveCsvPathFromArgs(Environment.GetCommandLineArgs(), GetDefaultCsvCandidates());
        }

        public static string ResolveCsvPathFromArgs(IReadOnlyList<string> args, IReadOnlyList<string> candidates)
        {
            var explicitPath = GetExplicitCsvPath(args);
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("Could not find 2.csv. Pass --csv with a full path or place 2.csv beside the project/build.");
        }

        public static string ResolveLogDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "Logs");
        }

        private static IReadOnlyList<string> GetDefaultCsvCandidates()
        {
            var projectRoot = ParentOf(Application.dataPath, 1);
            var workspaceRoot = ParentOf(Application.dataPath, 2);
            return new[]
            {
                Path.Combine(Application.streamingAssetsPath, "2.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "2.csv"),
                Path.Combine(projectRoot, "2.csv"),
                Path.Combine(workspaceRoot, "2.csv"),
                @"D:\Desktop\digital twin\2.csv"
            };
        }

        private static string GetExplicitCsvPath(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return null;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], "--csv", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                {
                    return args[i + 1];
                }

                const string prefix = "--csv=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(prefix.Length);
                }
            }

            return null;
        }

        private static string ParentOf(string path, int levels)
        {
            var current = new DirectoryInfo(path);
            for (var i = 0; i < levels && current.Parent != null; i++)
            {
                current = current.Parent;
            }

            return current.FullName;
        }
    }
}
