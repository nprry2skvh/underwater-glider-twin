using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnderwaterGliderTwin.Bootstrap
{
    public static class RuntimePathResolver
    {
        private const string CsvArgumentName = "--csv";
        private const string ModelsArgumentName = "--models";
        private static string csvPathOverride;

        public static string ResolveCsvPath()
        {
            return ResolveCsvPath(Environment.GetCommandLineArgs(), GetDefaultCsvCandidates(), csvPathOverride);
        }

        public static string ResolveCsvPath(IReadOnlyList<string> args, IReadOnlyList<string> candidates, string overridePath)
        {
            if (TryResolveExistingFile(overridePath, out var resolvedOverride))
            {
                return resolvedOverride;
            }

            return ResolveCsvPathFromArgs(args, candidates);
        }

        public static string ResolveCsvPathFromArgs(IReadOnlyList<string> args, IReadOnlyList<string> candidates)
        {
            if (TryGetExplicitPath(args, CsvArgumentName, out var explicitPath)
                && TryResolveExistingFile(explicitPath, out var resolvedExplicitPath))
            {
                return resolvedExplicitPath;
            }

            foreach (var candidate in candidates ?? Array.Empty<string>())
            {
                if (TryResolveExistingFile(candidate, out var resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            throw new FileNotFoundException("Could not find 2.csv. Pass --csv with a full path or place 2.csv beside the project, workspace, or build.");
        }

        public static string ResolveModelsDirectory()
        {
            return ResolveModelsDirectory(Environment.GetCommandLineArgs(), GetDefaultModelCandidates());
        }

        public static string ResolveModelsDirectory(IReadOnlyList<string> args, IReadOnlyList<string> candidates)
        {
            if (TryGetExplicitPath(args, ModelsArgumentName, out var explicitPath)
                && TryResolveUsableModelRoot(explicitPath, out var resolvedExplicitPath))
            {
                return resolvedExplicitPath;
            }

            foreach (var candidate in candidates ?? Array.Empty<string>())
            {
                if (TryResolveUsableModelRoot(candidate, out var resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            return FirstNonEmptyPath(candidates) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        }

        public static string ResolveLogDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "Logs");
        }

        public static string ResolveExportDirectory()
        {
            var path = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(path);
            return path;
        }

        public static void SetCsvPathOverride(string csvPath)
        {
            csvPathOverride = csvPath;
            RuntimeDataSourceState.UseCsvPath(csvPath);
        }

        private static IReadOnlyList<string> GetDefaultCsvCandidates()
        {
            var candidates = new List<string>();
            AddCandidate(candidates, csvPathOverride);
            AddCandidate(candidates, Path.Combine(Application.streamingAssetsPath, "2.csv"));
            AddCandidate(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "2.csv"));
            AddCandidate(candidates, Path.Combine(Directory.GetCurrentDirectory(), "2.csv"));
            AddParentCandidates(candidates, Application.dataPath, "2.csv", 5);
            AddParentCandidates(candidates, Directory.GetCurrentDirectory(), "2.csv", 5);
            return candidates;
        }

        private static IReadOnlyList<string> GetDefaultModelCandidates()
        {
            var candidates = new List<string>();
            AddCandidate(candidates, Path.Combine(Application.streamingAssetsPath, "Models"));
            AddCandidate(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models"));
            AddCandidate(candidates, Path.Combine(Directory.GetCurrentDirectory(), "Models"));
            AddParentCandidates(candidates, Application.dataPath, "Models", 5);
            AddParentCandidates(candidates, Directory.GetCurrentDirectory(), "Models", 5);
            return candidates;
        }

        private static void AddParentCandidates(List<string> candidates, string startPath, string childName, int maxLevels)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return;
            }

            var current = Directory.Exists(startPath)
                ? new DirectoryInfo(startPath)
                : new FileInfo(startPath).Directory;
            for (var level = 0; current != null && level <= maxLevels; level++)
            {
                AddCandidate(candidates, Path.Combine(current.FullName, childName));
                current = current.Parent;
            }
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(fullPath);
        }

        private static bool TryResolveExistingFile(string path, out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            resolvedPath = fullPath;
            return true;
        }

        private static bool TryResolveUsableModelRoot(string path, out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(Path.Combine(fullPath, "XGBoost", "manifest.json")))
            {
                return false;
            }

            resolvedPath = fullPath;
            return true;
        }

        private static string FirstNonEmptyPath(IReadOnlyList<string> candidates)
        {
            foreach (var candidate in candidates ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return null;
        }

        private static bool TryGetExplicitPath(IReadOnlyList<string> args, string argumentName, out string path)
        {
            path = null;
            if (args == null)
            {
                return false;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                {
                    path = args[i + 1];
                    return !string.IsNullOrWhiteSpace(path);
                }

                var prefix = argumentName + "=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    path = args[i].Substring(prefix.Length);
                    return !string.IsNullOrWhiteSpace(path);
                }
            }

            return false;
        }
    }
}
