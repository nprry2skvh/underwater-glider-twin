using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class TrajectoryJsonCodec
    {
        public static string Serialize(TrajectoryExportSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonUtility.ToJson(ToDto(snapshot), true);
        }

        public static TrajectoryExportSnapshot Deserialize(string json)
        {
            if (!TryDeserialize(json, out var snapshot, out var error))
            {
                throw new FormatException(error);
            }
            return snapshot;
        }

        internal static bool TryDeserialize(string json, out TrajectoryExportSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "JSON is empty."; return false; }
            try
            {
                var dto = JsonUtility.FromJson<TrajectoryJsonDocument>(json);
                if (dto == null || dto.schemaVersion != TrajectoryExportSnapshot.SchemaVersion)
                { error = "Unsupported trajectory export schema."; return false; }
                if (dto.timeline == null || dto.timeline.frames == null || dto.timeline.segments == null)
                { error = "Timeline data is missing."; return false; }
                var frames = dto.timeline.frames.Select(FromDto).ToArray();
                if (frames.Length == 0) { error = "Timeline has no frames."; return false; }
                var segments = dto.timeline.segments.Select(FromDto).ToArray();
                if (segments.Length == 0) { error = "Timeline has no segments."; return false; }
                var snapshotTimeline = new SimulationTimelineSnapshot(
                    frames,
                    segments,
                    dto.timeline.revision,
                    ParseStatus(dto.timeline.status),
                    FromDto(dto.timeline.missionState),
                    FromDto(dto.playback));
                snapshot = new TrajectoryExportSnapshot(
                    snapshotTimeline,
                    FromDto(dto.playback) ?? new TrajectoryPlaybackState(0, 0f, frames[0].ElapsedSeconds, false, 1f, 1),
                    FromPredictionDto(dto.prediction),
                    FromDto(dto.camera),
                    ParseDate(dto.createdAtUtc));
                return Validate(snapshot, out error);
            }
            catch (Exception ex)
            {
                error = "Invalid trajectory JSON: " + ex.Message;
                return false;
            }
        }

        internal static bool Validate(TrajectoryExportSnapshot snapshot, out string error)
        {
            error = null;
            var frames = snapshot.Timeline.Frames;
            for (var i = 0; i < frames.Count; i++)
            {
                if (!IsFinite(frames[i])) { error = "Timeline contains a non-finite value."; return false; }
                if (i > 0 && (frames[i].RowIndex <= frames[i - 1].RowIndex || frames[i].ElapsedSeconds <= frames[i - 1].ElapsedSeconds || frames[i].ProfileSequence < frames[i - 1].ProfileSequence))
                { error = "Timeline frames are not monotonic."; return false; }
            }
            foreach (var segment in snapshot.Timeline.Segments)
            {
                if (segment.Profile == null || !IsFinite(segment.StartElapsedSeconds))
                { error = "Timeline segment is invalid."; return false; }
            }
            return true;
        }

        private static TrajectoryJsonDocument ToDto(TrajectoryExportSnapshot source)
        {
            return new TrajectoryJsonDocument
            {
                schemaVersion = TrajectoryExportSnapshot.SchemaVersion,
                algorithmVersion = TrajectoryExportSnapshot.AlgorithmVersion,
                createdAtUtc = source.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                coordinate = new CoordinateDto { datum = "WGS84", axes = "east,north,up", units = "m" },
                timeline = new TimelineDto
                {
                    revision = source.Timeline.Revision,
                    status = source.Timeline.Status.ToString(),
                    missionState = ToDto(source.Timeline.MissionState),
                    segments = source.Timeline.Segments.Select(ToDto).ToArray(),
                    frames = source.Timeline.Frames.Select(ToDto).ToArray()
                },
                playback = ToDto(source.Playback),
                prediction = ToDto(source.Prediction),
                camera = ToDto(source.Camera)
            };
        }

        private static FrameDto ToDto(TelemetryFrame f)
        {
            return new FrameDto
            {
                rowIndex = f.RowIndex, rawTime = f.RawTime ?? string.Empty, elapsedSeconds = Finite(f.ElapsedSeconds),
                longitudeDeg = Finite(f.LongitudeDeg), latitudeDeg = Finite(f.LatitudeDeg), depthM = Finite(f.DepthM), altitudeM = Finite(f.AltitudeM),
                headingDeg = Finite(f.HeadingDeg), pitchDeg = Finite(f.PitchDeg), rollDeg = Finite(f.RollDeg), voltage24V = Finite(f.Voltage24V), current24A = Finite(f.Current24A), batteryPercent = Finite(f.BatteryPercent),
                workMode = f.WorkMode ?? string.Empty, runState = f.RunState ?? string.Empty, targetSegment = Finite(f.TargetSegment), targetHeadingDeg = Finite(f.TargetHeadingDeg), targetDepthM = Finite(f.TargetDepthM), targetAltitudeM = Finite(f.TargetAltitudeM),
                propellerRpm = Finite(f.PropellerRpm), pistonMm = Finite(f.PistonMm), turnAngleDeg = Finite(f.TurnAngleDeg),
                planned = f.HasPlannedPosition ? new Coordinate2Dto { longitudeDeg = Finite(f.PlannedLongitudeDeg), latitudeDeg = Finite(f.PlannedLatitudeDeg) } : null,
                missionState = ToDto(f.MissionState), profileSequence = f.ProfileSequence
            };
        }

        private static TelemetryFrame FromDto(FrameDto f)
        {
            return new TelemetryFrame(f.rowIndex, f.rawTime, f.elapsedSeconds, f.longitudeDeg, f.latitudeDeg, f.depthM, f.altitudeM, f.headingDeg, f.pitchDeg, f.rollDeg, f.voltage24V, f.current24A, f.batteryPercent, f.workMode, f.runState, f.targetSegment, f.targetHeadingDeg, f.targetDepthM, f.targetAltitudeM, f.propellerRpm, f.pistonMm, f.turnAngleDeg, null, f.planned == null ? double.NaN : f.planned.longitudeDeg, f.planned == null ? double.NaN : f.planned.latitudeDeg, FromDto(f.missionState), f.profileSequence);
        }

        private static SegmentDto ToDto(SimulationTimelineSegment s) => new SegmentDto { profileSequence = s.ProfileSequence, requestId = s.RequestId, startRowIndex = s.StartRowIndex, startElapsedSeconds = Finite(s.StartElapsedSeconds), committedAtUtc = s.CommittedAtUtc.ToString("o", CultureInfo.InvariantCulture), profile = ToDto(s.Profile) };
        private static SimulationTimelineSegment FromDto(SegmentDto s) => new SimulationTimelineSegment(s.profileSequence, s.requestId, s.startRowIndex, s.startElapsedSeconds, FromDto(s.profile), ParseDate(s.committedAtUtc));

        private static ProfileDto ToDto(SimulationProfile p)
        {
            p = p ?? SimulationProfile.Default;
            return new ProfileDto { cycleCount = p.CycleCount, cycleDurationSeconds = Finite(p.CycleDurationSeconds), sampleIntervalSeconds = Finite(p.SampleIntervalSeconds), targetDepthM = Finite(p.TargetDepthM), horizontalSpeedMps = Finite(p.HorizontalSpeedMps), startHeadingDeg = Finite(p.StartHeadingDeg), headingDeltaPerCycleDeg = Finite(p.HeadingDeltaPerCycleDeg), pitchAmplitudeDeg = Finite(p.PitchAmplitudeDeg), rollAmplitudeDeg = Finite(p.RollAmplitudeDeg), originLongitudeDeg = Finite(p.OriginLongitudeDeg), originLatitudeDeg = Finite(p.OriginLatitudeDeg), waterColumnDepthM = Finite(p.WaterColumnDepthM), dynamics = ToDto(p.Dynamics), oceanLayers = (p.OceanCurrentProfile?.Layers ?? Array.Empty<OceanCurrentLayer>()).Where(x => x != null).Select(x => new OceanLayerDto { minDepthM = Finite(x.MinDepthM), maxDepthM = Finite(x.MaxDepthM), eastwardMps = Finite(x.EastwardMps), northwardMps = Finite(x.NorthwardMps) }).ToArray() };
        }
        private static SimulationProfile FromDto(ProfileDto p)
        {
            var profile = SimulationProfile.Default;
            if (p == null) return profile;
            profile.CycleCount = p.cycleCount; profile.CycleDurationSeconds = p.cycleDurationSeconds; profile.SampleIntervalSeconds = p.sampleIntervalSeconds; profile.TargetDepthM = p.targetDepthM; profile.HorizontalSpeedMps = p.horizontalSpeedMps; profile.StartHeadingDeg = p.startHeadingDeg; profile.HeadingDeltaPerCycleDeg = p.headingDeltaPerCycleDeg; profile.PitchAmplitudeDeg = p.pitchAmplitudeDeg; profile.RollAmplitudeDeg = p.rollAmplitudeDeg; profile.OriginLongitudeDeg = p.originLongitudeDeg; profile.OriginLatitudeDeg = p.originLatitudeDeg; profile.WaterColumnDepthM = p.waterColumnDepthM; profile.Dynamics = FromDto(p.dynamics); profile.OceanCurrentProfile = new OceanCurrentProfile((p.oceanLayers ?? Array.Empty<OceanLayerDto>()).Select(x => new OceanCurrentLayer(x.minDepthM, x.maxDepthM, x.eastwardMps, x.northwardMps))); return profile;
        }
        private static DynamicsDto ToDto(GliderDynamicsProfile d) { d = d ?? GliderDynamicsProfile.Default; return new DynamicsDto { presetName = d.PresetName, massKg = Finite(d.MassKg), referenceAreaM2 = Finite(d.ReferenceAreaM2), referenceLengthM = Finite(d.ReferenceLengthM), cruiseSpeedMps = Finite(d.CruiseSpeedMps), integrationStepSeconds = Finite(d.IntegrationStepSeconds), maxBuoyancyForceN = Finite(d.MaxBuoyancyForceN), batteryCapacityWh = Finite(d.BatteryCapacityWh), minimumBatteryPercent = Finite(d.MinimumBatteryPercent) }; }
        private static GliderDynamicsProfile FromDto(DynamicsDto d) { var result = GliderDynamicsProfile.Default; if (d == null) return result; result.PresetName = d.presetName; result.MassKg = d.massKg; result.ReferenceAreaM2 = d.referenceAreaM2; result.ReferenceLengthM = d.referenceLengthM; result.CruiseSpeedMps = d.cruiseSpeedMps; result.IntegrationStepSeconds = d.integrationStepSeconds; result.MaxBuoyancyForceN = d.maxBuoyancyForceN; result.BatteryCapacityWh = d.batteryCapacityWh; result.MinimumBatteryPercent = d.minimumBatteryPercent; return result; }
        private static PlaybackDto ToDto(TrajectoryPlaybackState p) => p == null ? null : new PlaybackDto { currentFrameIndex = p.CurrentFrameIndex, continuousIndex = Finite(p.ContinuousIndex), currentElapsedSeconds = Finite(p.CurrentElapsedSeconds), isPlaying = p.IsPlaying, speed = Finite(p.Speed), direction = p.Direction };
        private static TrajectoryPlaybackState FromDto(PlaybackDto p) => p == null ? null : new TrajectoryPlaybackState(p.currentFrameIndex, p.continuousIndex, p.currentElapsedSeconds, p.isPlaying, p.speed, p.direction);
        private static PredictionDto ToDto(UnderwaterGliderTwin.Prediction.PredictionSnapshot p) => p == null ? null : new PredictionDto { status = p.Status, startIndex = p.StartIndex, endIndex = p.EndIndex, confidence01 = Finite(p.Confidence01), predicted = (p.PredictedPoints ?? Array.Empty<Vector3>()).Select(v => new Vector3Dto { x = Finite(v.x), y = Finite(v.y), z = Finite(v.z) }).ToArray() };
        private static UnderwaterGliderTwin.Prediction.PredictionSnapshot FromPredictionDto(PredictionDto p) => p == null ? null : new UnderwaterGliderTwin.Prediction.PredictionSnapshot(Vector3.zero, (p.predicted ?? Array.Empty<Vector3Dto>()).Select(v => new Vector3(v.x, v.y, v.z)).ToArray(), Array.Empty<Vector3>(), 0f, 0f, 0f, 0f, p.confidence01, p.startIndex, p.endIndex, p.status, 0f);
        private static CameraDto ToDto(CameraSnapshot c) => c == null ? null : new CameraDto { position = ToDto(c.Position), rotation = new Vector4Dto { x = c.Rotation.x, y = c.Rotation.y, z = c.Rotation.z, w = c.Rotation.w }, fieldOfView = Finite(c.FieldOfView), pixelWidth = c.PixelWidth, pixelHeight = c.PixelHeight, cullingMask = c.CullingMask };
        private static CameraSnapshot FromDto(CameraDto c) => c == null ? null : new CameraSnapshot(new Vector3(c.position.x, c.position.y, c.position.z), new Quaternion(c.rotation.x, c.rotation.y, c.rotation.z, c.rotation.w), c.fieldOfView, c.pixelWidth, c.pixelHeight, c.cullingMask);
        private static Vector3Dto ToDto(Vector3 v) => new Vector3Dto { x = Finite(v.x), y = Finite(v.y), z = Finite(v.z) };
        private static MissionDto ToDto(SimulationMissionState? state) { if (!state.HasValue) return null; var s = state.Value; return new MissionDto { phase = s.Phase.ToString(), completedCycles = s.CompletedCycles, legElapsedSeconds = Finite(s.LegElapsedSeconds), turnaroundElapsedSeconds = Finite(s.TurnaroundElapsedSeconds), safetyWarningRaised = s.SafetyWarningRaised }; }
        private static SimulationMissionState? FromDto(MissionDto s) { if (s == null) return null; Enum.TryParse(s.phase, out SimulationMissionPhase phase); return new SimulationMissionState { Phase = phase, CompletedCycles = s.completedCycles, LegElapsedSeconds = s.legElapsedSeconds, TurnaroundElapsedSeconds = s.turnaroundElapsedSeconds, SafetyWarningRaised = s.safetyWarningRaised }; }
        private static SimulationTimelineStatus ParseStatus(string value) { return Enum.TryParse(value, out SimulationTimelineStatus status) ? status : SimulationTimelineStatus.Committed; }
        private static DateTime ParseDate(string value) { return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date.ToUniversalTime() : DateTime.UtcNow; }
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private static double Finite(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(TelemetryFrame f) => !(float.IsNaN(f.ElapsedSeconds) || float.IsInfinity(f.ElapsedSeconds) || double.IsNaN(f.LongitudeDeg) || double.IsInfinity(f.LongitudeDeg) || double.IsNaN(f.LatitudeDeg) || double.IsInfinity(f.LatitudeDeg));

        [Serializable] internal sealed class TrajectoryJsonDocument { public int schemaVersion; public string algorithmVersion; public string createdAtUtc; public CoordinateDto coordinate; public TimelineDto timeline; public PlaybackDto playback; public PredictionDto prediction; public CameraDto camera; }
        [Serializable] internal sealed class CoordinateDto { public string datum; public string axes; public string units; }
        [Serializable] internal sealed class Coordinate2Dto { public double longitudeDeg; public double latitudeDeg; }
        [Serializable] internal sealed class TimelineDto { public int revision; public string status; public FrameDto[] frames; public SegmentDto[] segments; public MissionDto missionState; }
        [Serializable] internal sealed class FrameDto { public int rowIndex; public string rawTime; public float elapsedSeconds, depthM, altitudeM, headingDeg, pitchDeg, rollDeg, voltage24V, current24A, batteryPercent, targetSegment, targetHeadingDeg, targetDepthM, targetAltitudeM, propellerRpm, pistonMm, turnAngleDeg; public double longitudeDeg, latitudeDeg; public string workMode, runState; public Coordinate2Dto planned; public MissionDto missionState; public int profileSequence; }
        [Serializable] internal sealed class SegmentDto { public int profileSequence; public long requestId; public int startRowIndex; public float startElapsedSeconds; public string committedAtUtc; public ProfileDto profile; }
        [Serializable] internal sealed class ProfileDto { public int cycleCount; public float cycleDurationSeconds, sampleIntervalSeconds, targetDepthM, horizontalSpeedMps, startHeadingDeg, headingDeltaPerCycleDeg, pitchAmplitudeDeg, rollAmplitudeDeg, waterColumnDepthM; public double originLongitudeDeg, originLatitudeDeg; public DynamicsDto dynamics; public OceanLayerDto[] oceanLayers; }
        [Serializable] internal sealed class DynamicsDto { public string presetName; public float massKg, referenceAreaM2, referenceLengthM, cruiseSpeedMps, integrationStepSeconds, maxBuoyancyForceN, batteryCapacityWh, minimumBatteryPercent; }
        [Serializable] internal sealed class OceanLayerDto { public float minDepthM, maxDepthM, eastwardMps, northwardMps; }
        [Serializable] internal sealed class MissionDto { public string phase; public int completedCycles; public float legElapsedSeconds, turnaroundElapsedSeconds; public bool safetyWarningRaised; }
        [Serializable] internal sealed class PlaybackDto { public int currentFrameIndex, direction; public float continuousIndex, currentElapsedSeconds, speed; public bool isPlaying; }
        [Serializable] internal sealed class PredictionDto { public string status; public int startIndex, endIndex; public float confidence01; public Vector3Dto[] predicted; }
        [Serializable] internal sealed class Vector3Dto { public float x, y, z; }
        [Serializable] internal sealed class Vector4Dto { public float x, y, z, w; }
        [Serializable] internal sealed class CameraDto { public Vector3Dto position; public Vector4Dto rotation; public float fieldOfView; public int pixelWidth, pixelHeight, cullingMask; }
    }
}
