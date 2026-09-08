using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class TrajectoryExportService
    {
        private readonly string defaultRoot;

        public TrajectoryExportService(string exportRoot = null)
        {
            defaultRoot = string.IsNullOrWhiteSpace(exportRoot) ? UnderwaterGliderTwin.Bootstrap.RuntimePathResolver.ResolveExportDirectory() : Path.GetFullPath(exportRoot);
        }

        public IDisposable BeginExport(TrajectoryExportSnapshot snapshot, string exportRoot, Action<TrajectoryExportStatus> progress, Action<TrajectoryExportResult> completed)
        {
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
        private static readonly byte[] MinimalPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    }
}
