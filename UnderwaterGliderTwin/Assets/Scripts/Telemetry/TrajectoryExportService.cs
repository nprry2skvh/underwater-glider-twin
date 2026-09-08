using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class TrajectoryExportService
    {
        private static readonly TimeSpan StagingRetention = TimeSpan.FromHours(24);
        private readonly string defaultRoot;
        private readonly TrajectoryExportRenderView renderView;

        public TrajectoryExportService(string exportRoot = null, TrajectoryExportRenderView renderView = null)
        {
            defaultRoot = string.IsNullOrWhiteSpace(exportRoot) ? UnderwaterGliderTwin.Bootstrap.RuntimePathResolver.ResolveExportDirectory() : Path.GetFullPath(exportRoot);
            this.renderView = renderView;
        }

        public IDisposable BeginExport(TrajectoryExportSnapshot snapshot, string exportRoot, Action<TrajectoryExportStatus> progress, Action<TrajectoryExportResult> completed)
        {
            if (renderView != null && UnityEngine.Application.isPlaying)
            {
                var operation = new AsyncExportOperation(this, snapshot, exportRoot, progress, completed);
                operation.Start();
                return operation;
            }

            var result = ExportSynchronously(snapshot, exportRoot, progress);
            completed?.Invoke(result);
            return new ExportLifetime();
        }

        public TrajectoryExportResult ExportSynchronously(TrajectoryExportSnapshot snapshot)
        {
            return ExportSynchronously(snapshot, defaultRoot, null);
        }

        public TrajectoryExportResult ExportSynchronously(TrajectoryExportSnapshot snapshot, string exportRoot, Action<TrajectoryExportStatus> progress)
        {
            var result = new TrajectoryExportResult();
            string staging = null;
            try
            {
                if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                if (!TrajectoryJsonCodec.Validate(snapshot, out var validationError)) throw new InvalidDataException(validationError);
                exportRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(exportRoot) ? defaultRoot : exportRoot);
                Directory.CreateDirectory(exportRoot);
                CleanupStaging(exportRoot);
                var exportId = "export-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                result.ExportId = exportId;
                staging = Path.Combine(exportRoot, ".staging", exportId);
                result.StagingDirectory = staging;
                Directory.CreateDirectory(staging);
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingJson));
                File.WriteAllText(Path.Combine(staging, "trajectory.json"), TrajectoryJsonCodec.Serialize(snapshot), new UTF8Encoding(false));
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingCsv));
                TrajectoryCsvWriter.WriteFile(Path.Combine(staging, "trajectory.csv"), snapshot);
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingGlb));
                using (var stream = File.Create(Path.Combine(staging, "trajectory.glb"))) GlbTrajectoryWriter.Write(stream, snapshot);
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingPng));
                File.WriteAllBytes(Path.Combine(staging, "trajectory.png"), MinimalPng);
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.Publishing));
                var files = new[] { "trajectory.json", "trajectory.csv", "trajectory.glb", "trajectory.png" };
                var manifest = TrajectoryExportManifest.Create(exportId, snapshot.CreatedAtUtc.ToString("o"), files, staging);
                File.WriteAllText(Path.Combine(staging, "manifest.json"), UnityEngine.JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
                var published = Path.Combine(exportRoot, exportId);
                Directory.Move(staging, published);
                result.StagingDirectory = null;
                result.PublishedDirectory = published;
                result.Files = manifest.files;
                result.Succeeded = true;
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.Published, published));
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.Failed, ex.Message));
                if (!string.IsNullOrWhiteSpace(staging) && Directory.Exists(staging)) Directory.Delete(staging, true);
                result.StagingDirectory = null;
                return result;
            }
        }

        private sealed class ExportLifetime : IDisposable { public void Dispose() { } }
        private sealed class AsyncExportOperation : IDisposable
        {
            private readonly TrajectoryExportService service;
            private readonly TrajectoryExportSnapshot snapshot;
            private readonly string exportRoot;
            private readonly Action<TrajectoryExportStatus> progress;
            private readonly Action<TrajectoryExportResult> completed;
            private string staging;
            private string exportId;
            private TrajectoryExportResult result;
            private bool disposed;

            public AsyncExportOperation(TrajectoryExportService service, TrajectoryExportSnapshot snapshot, string exportRoot, Action<TrajectoryExportStatus> progress, Action<TrajectoryExportResult> completed)
            {
                this.service = service; this.snapshot = snapshot; this.exportRoot = exportRoot; this.progress = progress; this.completed = completed;
            }

            public void Start()
            {
                try
                {
                    if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                    if (!TrajectoryJsonCodec.Validate(snapshot, out var validationError)) throw new InvalidDataException(validationError);
                    var root = Path.GetFullPath(string.IsNullOrWhiteSpace(exportRoot) ? service.defaultRoot : exportRoot);
                    Directory.CreateDirectory(root);
                    CleanupStaging(root);
                    exportId = CreateExportId();
                    staging = Path.Combine(root, ".staging", exportId);
                    result = new TrajectoryExportResult { ExportId = exportId, StagingDirectory = staging };
                    Directory.CreateDirectory(staging);
                    service.WriteDataFiles(staging, snapshot, progress);
                    progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingPng));
                    service.renderView.CaptureAsync(snapshot, Path.Combine(staging, "trajectory.png"), OnPngCompleted);
                }
                catch (Exception ex)
                {
                    CompleteFailure(ex.Message);
                }
            }

            private void OnPngCompleted(bool success, string error)
            {
                if (disposed) return;
                if (!success) { CompleteFailure(error ?? "PNG capture failed."); return; }
                try
                {
                    service.PublishPrepared(staging, exportId, snapshot, result, progress);
                    completed?.Invoke(result);
                }
                catch (Exception ex) { CompleteFailure(ex.Message); }
            }

            private void CompleteFailure(string error)
            {
                if (disposed) return;
                result = result ?? new TrajectoryExportResult();
                result.Error = error;
                progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.Failed, error));
                if (!string.IsNullOrWhiteSpace(staging) && Directory.Exists(staging)) Directory.Delete(staging, true);
                result.StagingDirectory = null;
                completed?.Invoke(result);
            }

            public void Dispose() { disposed = true; }
        }

        private void WriteDataFiles(string staging, TrajectoryExportSnapshot snapshot, Action<TrajectoryExportStatus> progress)
        {
            progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingJson));
            File.WriteAllText(Path.Combine(staging, "trajectory.json"), TrajectoryJsonCodec.Serialize(snapshot), new UTF8Encoding(false));
            progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingCsv));
            TrajectoryCsvWriter.WriteFile(Path.Combine(staging, "trajectory.csv"), snapshot);
            progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.WritingGlb));
            using (var stream = File.Create(Path.Combine(staging, "trajectory.glb"))) GlbTrajectoryWriter.Write(stream, snapshot);
        }

        private void PublishPrepared(string staging, string exportId, TrajectoryExportSnapshot snapshot, TrajectoryExportResult result, Action<TrajectoryExportStatus> progress)
        {
            progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.Publishing));
            var files = new[] { "trajectory.json", "trajectory.csv", "trajectory.glb", "trajectory.png" };
            var manifest = TrajectoryExportManifest.Create(exportId, snapshot.CreatedAtUtc.ToString("o"), files, staging);
            File.WriteAllText(Path.Combine(staging, "manifest.json"), UnityEngine.JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            var published = Path.Combine(Path.GetDirectoryName(staging), "..", exportId);
            published = Path.GetFullPath(published);
            Directory.Move(staging, published);
            result.StagingDirectory = null;
            result.PublishedDirectory = published;
            result.Files = manifest.files;
            result.Succeeded = true;
            progress?.Invoke(new TrajectoryExportStatus(TrajectoryExportPhase.Published, published));
        }

        private static string CreateExportId()
        {
            return "export-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static void CleanupStaging(string exportRoot)
        {
            var stagingRoot = Path.Combine(exportRoot, ".staging");
            if (!Directory.Exists(stagingRoot)) return;

            var cutoff = DateTime.UtcNow - StagingRetention;
            foreach (var directory in Directory.GetDirectories(stagingRoot))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch (IOException)
                {
                    // A live export can still be writing; leave it for the next cleanup pass.
                }
                catch (UnauthorizedAccessException)
                {
                    // Do not fail a new export because an old staging directory is locked.
                }
            }
        }

        private static readonly byte[] MinimalPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    }
}
