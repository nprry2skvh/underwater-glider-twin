using UnityEngine;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class GliderVisualController : MonoBehaviour
    {
        private const float VectorScale = 2.5f;
        private PlaybackController playback;

        public Transform PortControlSurface { get; private set; }
        public Transform StarboardControlSurface { get; private set; }
        public Transform VerticalTail { get; private set; }
        public LineRenderer CurrentVector { get; private set; }
        public LineRenderer WaterVelocityVector { get; private set; }
        public LineRenderer GroundVelocityVector { get; private set; }

        public void Initialize(PlaybackController playbackController)
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
            }

            playback = playbackController;
            EnsureVisuals();
            playback.FrameChangedWithReason += OnFrameChanged;
            ApplyFrame(playback.Model.CurrentFrame);
        }

        public void ApplyFrame(TelemetryFrame frame)
        {
            EnsureVisuals();

            var pitchDeflection = Mathf.Clamp(-frame.PitchDeg * 0.35f, -16f, 16f);
            var rollDeflection = Mathf.Clamp(frame.RollDeg * 0.35f, -12f, 12f);
            PortControlSurface.localRotation = Quaternion.Euler(pitchDeflection + rollDeflection, 0f, 0f);
            StarboardControlSurface.localRotation = Quaternion.Euler(pitchDeflection - rollDeflection, 0f, 0f);
            VerticalTail.localRotation = Quaternion.Euler(0f, Mathf.Clamp(frame.TurnAngleDeg * 0.08f, -10f, 10f), 0f);

            if (!frame.Diagnostics.HasValue)
            {
                SetVectorVisible(CurrentVector, false);
                SetVectorVisible(WaterVelocityVector, false);
                SetVectorVisible(GroundVelocityVector, false);
                return;
            }

            var diagnostics = frame.Diagnostics.Value;
            SetVector(CurrentVector, diagnostics.CurrentVelocityEndMps);
            SetVector(WaterVelocityVector, diagnostics.WaterVelocityEndMps);
            SetVector(GroundVelocityVector, diagnostics.WaterVelocityEndMps + diagnostics.CurrentVelocityEndMps);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChangedWithReason -= OnFrameChanged;
            }
        }

        private void OnFrameChanged(TelemetryFrame frame, int index, float progress01, FrameUpdateReason reason)
        {
            ApplyFrame(frame);
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
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 2;
            line.sharedMaterial = RuntimeMaterialFactory.Line(name + "Material", color);
            line.gameObject.SetActive(false);
            return line;
        }

        private void SetVector(LineRenderer line, Vector3 velocity)
        {
            var visible = velocity.sqrMagnitude > 0.0001f;
            SetVectorVisible(line, visible);
            if (!visible)
            {
                return;
            }

            line.SetPosition(0, transform.position);
            line.SetPosition(1, transform.position + velocity * VectorScale);
        }

        private static void SetVectorVisible(LineRenderer line, bool visible)
        {
            line.gameObject.SetActive(visible);
        }
    }
}
