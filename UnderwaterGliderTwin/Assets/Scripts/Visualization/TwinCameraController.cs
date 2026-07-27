using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public enum CameraMode
    {
        Follow,
        Global,
        Top,
        Side,
        Orbit
    }

    public sealed class TwinCameraController : MonoBehaviour
    {
        private readonly FollowCameraRig followRig = new FollowCameraRig();
        private Transform target;
        private Vector3[] trajectoryPoints;
        private Vector3 trajectoryCenter;
        private Vector3 smoothedFollowPosition;
        private Vector3 smoothedFollowForward = Vector3.forward;
        private Vector3 lastTargetPosition;
        private bool hasFollowState;
        private float orbitYaw;
        private float orbitPitch = 24f;
        private float orbitDistance = 22f;
        private Vector3 orbitPanOffset;

        public CameraMode CurrentMode { get; private set; } = CameraMode.Follow;

        public void Initialize(Transform followTarget, Vector3[] fullTrajectoryPoints)
        {
            target = followTarget;
            trajectoryPoints = fullTrajectoryPoints;
            trajectoryCenter = ComputeTrajectoryCenter(fullTrajectoryPoints);
            SetMode(CameraMode.Follow);
        }

        public void SetMode(CameraMode mode)
        {
            CurrentMode = mode;
            if (mode == CameraMode.Global)
            {
                FrameTrajectory();
            }
            else if (mode == CameraMode.Top)
            {
                FrameTopView();
            }
            else if (mode == CameraMode.Side)
            {
                FrameSideView();
            }
            else if (mode == CameraMode.Orbit)
            {
                CaptureOrbitFromCurrentView();
            }
            else if (mode == CameraMode.Follow)
            {
                hasFollowState = false;
                UpdateFollow();
            }
        }

        public void ResetView()
        {
            SetMode(CameraMode.Global);
        }

        public void SetMissionVolumeView(float missionDepthM, float horizontalExtent = 38f, float verticalScale = 0.05f)
        {
            SetMissionVolumeView(missionDepthM, new Vector2(horizontalExtent, horizontalExtent), verticalScale);
        }

        public void SetMissionVolumeView(float missionDepthM, Vector2 horizontalExtents, float verticalScale = 0.05f)
        {
            CurrentMode = CameraMode.Global;
            if (trajectoryPoints == null || trajectoryPoints.Length == 0)
            {
                FrameTrajectory();
                return;
            }

            var bounds = BuildTrajectoryBounds();
            var missionDepth = Mathf.Max(2f, missionDepthM * Mathf.Max(0.001f, verticalScale));
            var horizontalSpan = Mathf.Max(38f, horizontalExtents.x, horizontalExtents.y, bounds.size.x, bounds.size.z);
            var verticalSpan = Mathf.Max(missionDepth, bounds.size.y, 8f);
            var distance = Mathf.Max(horizontalSpan * 1.3f, verticalSpan * 1.25f, 46f);
            var elevation = Mathf.Max(horizontalSpan * 0.62f, verticalSpan * 0.9f, 24f);
            var focus = bounds.center;
            transform.position = focus + new Vector3(distance * 0.68f, elevation, -distance * 0.88f);
            transform.LookAt(focus);
        }

        private void LateUpdate()
        {
            if (CurrentMode == CameraMode.Follow)
            {
                if (Input.GetMouseButton(0)
                    && (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.001f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.001f))
                {
                    SetMode(CameraMode.Orbit);
                    UpdateOrbit();
                    return;
                }

                UpdateFollow();
            }
            else if (CurrentMode == CameraMode.Orbit)
            {
                UpdateOrbit();
            }
        }

        private void UpdateFollow()
        {
            if (target == null)
            {
                return;
            }

            if (!hasFollowState)
            {
                smoothedFollowPosition = target.position;
                smoothedFollowForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
                if (smoothedFollowForward.sqrMagnitude < 0.0001f)
                {
                    smoothedFollowForward = Vector3.forward;
                }
                lastTargetPosition = target.position;

                var initialPose = followRig.ComputeDesiredPose(smoothedFollowPosition, smoothedFollowForward);
                transform.SetPositionAndRotation(initialPose.position, initialPose.rotation);
                hasFollowState = true;
                return;
            }

            smoothedFollowPosition = Vector3.Lerp(smoothedFollowPosition, target.position, 0.12f);
            var motionForward = Vector3.ProjectOnPlane(target.position - lastTargetPosition, Vector3.up);
            var targetForward = motionForward.sqrMagnitude > 0.0004f
                ? motionForward.normalized
                : Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (targetForward.sqrMagnitude > 0.0001f)
            {
                smoothedFollowForward = Vector3.Slerp(smoothedFollowForward, targetForward.normalized, 0.1f);
            }
            lastTargetPosition = target.position;

            var pose = followRig.Step(transform.position, transform.rotation, smoothedFollowPosition, smoothedFollowForward);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private void UpdateOrbit()
        {
            if (Input.GetMouseButton(0))
            {
                orbitYaw += Input.GetAxis("Mouse X") * 3f;
                orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * 2.5f, -20f, 80f);
            }

            if (Input.GetMouseButton(2))
            {
                orbitPanOffset += (-transform.right * Input.GetAxis("Mouse X") - transform.up * Input.GetAxis("Mouse Y")) * Mathf.Max(orbitDistance * 0.015f, 0.08f);
            }

            orbitDistance = Mathf.Clamp(orbitDistance - Input.mouseScrollDelta.y * 2.5f, 6f, 180f);
            var focus = GetOrbitFocusPoint();
            var orbitRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            var desiredPosition = focus - orbitRotation * Vector3.forward * orbitDistance;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 0.22f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation((focus - transform.position).normalized, Vector3.up), 0.24f);
        }

        private void FrameTrajectory()
        {
            if (trajectoryPoints == null || trajectoryPoints.Length == 0)
            {
                transform.position = new Vector3(0f, 30f, -45f);
                transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                return;
            }

            var bounds = BuildTrajectoryBounds();
            if (!IsFinite(bounds.center) || !IsFinite(bounds.size))
            {
                transform.position = new Vector3(0f, 30f, -45f);
                transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                return;
            }

            var size = Mathf.Max(bounds.size.x, bounds.size.z, 20f);
            var extents = bounds.extents;
            var framingRadius = Mathf.Max(
                Mathf.Sqrt(extents.x * extents.x + extents.z * extents.z),
                extents.y * 1.6f,
                size * 0.55f);
            var fieldOfView = GetComponent<Camera>() != null ? GetComponent<Camera>().fieldOfView : 60f;
            var distance = Mathf.Max(framingRadius / Mathf.Tan(Mathf.Max(fieldOfView * 0.5f, 20f) * Mathf.Deg2Rad) * 1.28f, 28f);
            var focusPoint = bounds.center + Vector3.down * Mathf.Min(extents.y * 0.18f, 8f);
            var viewDirection = new Vector3(0.58f, 0.74f, -1f).normalized;
            var desiredPosition = focusPoint - viewDirection * distance;
            desiredPosition.y = Mathf.Max(desiredPosition.y, bounds.max.y + Mathf.Max(extents.y * 2.4f, 18f));
            transform.position = desiredPosition;
            transform.LookAt(focusPoint);
        }

        private void FrameTopView()
        {
            if (trajectoryPoints == null || trajectoryPoints.Length == 0)
            {
                transform.position = new Vector3(0f, 60f, 0f);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                return;
            }

            var bounds = BuildTrajectoryBounds();
            var span = Mathf.Max(bounds.size.x, bounds.size.z, 20f);
            var camera = GetComponent<Camera>();
            var fieldOfView = camera != null ? camera.fieldOfView : 60f;
            var distance = Mathf.Max(span / Mathf.Tan(Mathf.Max(15f, fieldOfView * 0.5f) * Mathf.Deg2Rad) * 0.72f, 30f);
            var focus = bounds.center;
            transform.position = focus + Vector3.up * distance;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }

        private void FrameSideView()
        {
            if (trajectoryPoints == null || trajectoryPoints.Length == 0)
            {
                transform.position = new Vector3(60f, 12f, 0f);
                transform.LookAt(Vector3.zero);
                return;
            }

            var bounds = BuildTrajectoryBounds();
            var span = Mathf.Max(bounds.size.x, bounds.size.y * 2f, bounds.size.z, 20f);
            var focus = bounds.center + Vector3.down * Mathf.Min(bounds.extents.y * 0.15f, 5f);
            var distance = Mathf.Max(span * 1.35f, 30f);
            transform.position = focus + new Vector3(distance, distance * 0.18f, 0f);
            transform.LookAt(focus);
        }

        private void CaptureOrbitFromCurrentView()
        {
            var focus = GetOrbitFocusPoint();
            var delta = transform.position - focus;
            orbitDistance = Mathf.Clamp(delta.magnitude, 6f, 180f);
            if (delta.sqrMagnitude < 0.001f)
            {
                orbitYaw = 0f;
                orbitPitch = 24f;
                return;
            }

            var lookRotation = Quaternion.LookRotation(-delta.normalized, Vector3.up);
            orbitYaw = lookRotation.eulerAngles.y;
            orbitPitch = lookRotation.eulerAngles.x;
            if (orbitPitch > 180f)
            {
                orbitPitch -= 360f;
            }
        }

        private Vector3 GetOrbitFocusPoint()
        {
            var baseFocus = target != null ? target.position : trajectoryCenter;
            return baseFocus + orbitPanOffset;
        }

        private Bounds BuildTrajectoryBounds()
        {
            var bounds = new Bounds(trajectoryPoints[0], Vector3.zero);
            for (var i = 1; i < trajectoryPoints.Length; i++)
            {
                bounds.Encapsulate(trajectoryPoints[i]);
            }

            return bounds;
        }

        private static Vector3 ComputeTrajectoryCenter(Vector3[] points)
        {
            if (points == null || points.Length == 0)
            {
                return Vector3.zero;
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < points.Length; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return bounds.center;
        }

        private static bool IsFinite(Vector3 vector)
        {
            return !float.IsNaN(vector.x) && !float.IsInfinity(vector.x)
                && !float.IsNaN(vector.y) && !float.IsInfinity(vector.y)
                && !float.IsNaN(vector.z) && !float.IsInfinity(vector.z);
        }
    }
}
