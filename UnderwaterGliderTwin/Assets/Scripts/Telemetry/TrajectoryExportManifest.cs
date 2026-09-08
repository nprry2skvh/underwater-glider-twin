using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    [Serializable]
    public sealed class ExportFileEntry
    {
        public string relativePath;
        public long byteLength;
        public string sha256;
    }

    [Serializable]
    public sealed class TrajectoryExportManifest
    {
        public int schemaVersion = TrajectoryExportSnapshot.SchemaVersion;
        public string exportId;
        public string createdAtUtc;
        public bool selfContained = true;
        public ExportFileEntry[] files;

        public static TrajectoryExportManifest Create(string exportId, string createdAtUtc, IReadOnlyList<string> relativePaths, string root)
        {
            var entries = new List<ExportFileEntry>();
            foreach (var relative in relativePaths)
            {
                var path = Path.Combine(root, relative);
                entries.Add(new ExportFileEntry { relativePath = relative, byteLength = new FileInfo(path).Length, sha256 = HashFile(path) });
            }
            return new TrajectoryExportManifest { exportId = exportId, createdAtUtc = createdAtUtc, files = entries.ToArray() };
        }

        public bool Validate(string root, out string error)
        {
            error = null;
            if (files == null || files.Length != 4) { error = "Manifest must contain four output files."; return false; }
            foreach (var file in files)
            {
                var path = Path.Combine(root, file.relativePath);
                if (!File.Exists(path) || new FileInfo(path).Length != file.byteLength || !string.Equals(HashFile(path), file.sha256, StringComparison.OrdinalIgnoreCase)) { error = "Manifest hash validation failed for " + file.relativePath; return false; }
            }
            return true;
        }

        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    public enum TrajectoryExportPhase { CaptureSnapshot, WritingJson, WritingCsv, WritingGlb, WritingPng, Publishing, Published, Failed }
    public sealed class TrajectoryExportStatus { public TrajectoryExportPhase Phase { get; } public string Message { get; } public TrajectoryExportStatus(TrajectoryExportPhase phase, string message = null) { Phase = phase; Message = message ?? phase.ToString(); } }
    public sealed class TrajectoryExportResult
    {
        public bool Succeeded { get; internal set; }
        public string ExportId { get; internal set; }
        public string PublishedDirectory { get; internal set; }
        public string StagingDirectory { get; internal set; }
        public string Error { get; internal set; }
        public IReadOnlyList<ExportFileEntry> Files { get; internal set; } = Array.Empty<ExportFileEntry>();
    }

    public sealed class TrajectoryExportRequest
    {
        public TrajectoryExportSnapshot Snapshot { get; }
        public string ExportRoot { get; }
        public TrajectoryExportRequest(TrajectoryExportSnapshot snapshot, string exportRoot = null) { Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot)); ExportRoot = exportRoot; }
    }
}
