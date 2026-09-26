using UnityEngine;
using UnderwaterGliderTwin.Mapping;
using UnderwaterGliderTwin.Playback;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class GliderVisualController : MonoBehaviour
    {
        private const float VectorScale = 2.5f;
        private PlaybackController playback;
        private GeoCoordinateMapper mapper;

        public Transform PortControlSurface { get; private set; }
        public Transform StarboardControlSurface { get; private set; }
        public Transform VerticalTail { get; private set; }
        public LineRenderer CurrentVector { get; private set; }
        public LineRenderer WaterVelocityVector { get; private set; }
        public LineRenderer GroundVelocityVector { get; private set; }

        public void Initialize(PlaybackController playbackController, GeoCoordinateMapper coordinateMapper)
        {
            if (playback != null)
            {
                playback.ContinuousChanged -= OnContinuousChanged;
            }

            playback = playbackController;
            mapper = coordinateMapper;
            EnsureVisuals();
            playback.ContinuousChanged += OnContinuousChanged;
            ApplySample(ContinuousMotionSampler.Sample(
                playback.Model.Frames,
                playback.Model.ContinuousElapsedSeconds));
        }

        public void ApplySample(ContinuousMotionSample sample)
        {
            EnsureVisuals();

            if (!sample.HasDiagnostics)
            {
                SetControlSurfaces(Vector3.zero);
                SetVectorVisible(CurrentVector, false);
                SetVectorVisible(WaterVelocityVector, false);
                SetVectorVisible(GroundVelocityVector, false);
                return;
            }

            SetControlSurfaces(sample.ControlSurfaceDeflectionDeg);
            SetVector(CurrentVector, sample.CurrentVelocityEndMps);
            SetVector(WaterVelocityVector, sample.WaterVelocityEndMps);
            SetVector(GroundVelocityVector, sample.GroundVelocityEndMps);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.ContinuousChanged -= OnContinuousChanged;
            }
        }

        private void OnContinuousChanged(float continuousIndex, float progress01, FrameUpdateReason reason)
        {
            ApplySample(ContinuousMotionSampler.Sample(
                playback.Model.Frames,
                playback.Model.ContinuousElapsedSeconds));
        }

        private void SetControlSurfaces(Vector3 rollPitchYawDeg)
        {
            var portDeflection = Mathf.Clamp(rollPitchYawDeg.y + rollPitchYawDeg.x, -30f, 30f);
            var starboardDeflection = Mathf.Clamp(rollPitchYawDeg.y - rollPitchYawDeg.x, -30f, 30f);
            var tailDeflection = Mathf.Clamp(rollPitchYawDeg.z, -30f, 30f);
            PortControlSurface.localRotation = Quaternion.Euler(portDeflection, 0f, 0f);
            StarboardControlSurface.localRotation = Quaternion.Euler(starboardDeflection, 0f, 0f);
            VerticalTail.localRotation = Quaternion.Euler(0f, tailDeflection, 0f);
        }

        private void EnsureVisuals()
        {
            PortControlSurface ??= transform.Find("MainWing/PortControlSurface");
            StarboardControlSurface ??= transform.Find("MainWing/StarboardControlSurface");
            VerticalTail ??= transform.Find("TailBoom/VerticalTail");
            CurrentVector ??= CreateVector("CurrentVector", new Color(0.2f, 0.92f, 1f, 0.95f));
            WaterVelocityVector ??= CreateVector("WaterVelocityVector", new Color(1f, 0.88f, 0.22f, 0.95f));
            GroundVelocityVector ??= CreateVector("GroundVelocityVector", new Color(0.34f, 0.95f, 0.53f, 0.95f));
        }

        private LineRenderer CreateVector(string name, Color color)
        {
            var vectorObject = new GameObject(name);
            vectorObject.transform.SetParent(transform, false);
            var line = vectorObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 2;
            line.sharedMaterial = RuntimeMaterialFactory.Line(name + "Material", color);
            line.gameObject.SetActive(false);
            return line;
        }

        private void SetVector(LineRenderer line, Vector3 dynamicsVelocityEndMps)
        {
            var worldVelocity = mapper != null
                ? mapper.MapDynamicsVelocity(dynamicsVelocityEndMps)
                : Vector3.zero;
            var visible = worldVelocity.sqrMagnitude > 0.0001f;
            SetVectorVisible(line, visible);
            if (!visible)
            {
                return;
            }

            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, transform.InverseTransformVector(worldVelocity * VectorScale));
        }

        private static void SetVectorVisible(LineRenderer line, bool visible)
        {
            line.gameObject.SetActive(visible);
        }
    }
}
