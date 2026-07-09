using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public enum CameraMode
    {
        Follow,
        Global,
        Free
    }

    public sealed class TwinCameraController : MonoBehaviour
    {
        private Transform target;
        private Vector3[] trajectoryPoints;
        private Vector3 followVelocity;
        private float freeYaw;
        private float freePitch = 25f;
        private float freeDistance = 22f;

        public CameraMode CurrentMode { get; private set; } = CameraMode.Follow;

        public void Initialize(Transform followTarget, Vector3[] fullTrajectoryPoints)
        {
            target = followTarget;
            trajectoryPoints = fullTrajectoryPoints;
            SetMode(CameraMode.Follow);
        }

        public void SetMode(CameraMode mode)
        {
            CurrentMode = mode;
            if (mode == CameraMode.Global)
            {
                FrameTrajectory();
            }
        }

        private void LateUpdate()
        {
            if (CurrentMode == CameraMode.Follow)
            {
                UpdateFollow();
            }
            else if (CurrentMode == CameraMode.Free)
            {
                UpdateFree();
            }
        }

        private void UpdateFollow()
        {
            if (target == null)
            {
                return;
            }

            var desired = target.position - target.forward * 8f + Vector3.up * 4f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref followVelocity, 0.25f);
            transform.LookAt(target.position + Vector3.up * 0.8f);
        }

        private void UpdateFree()
        {
            if (Input.GetMouseButton(1))
            {
                freeYaw += Input.GetAxis("Mouse X") * 3f;
                freePitch = Mathf.Clamp(freePitch - Input.GetAxis("Mouse Y") * 3f, -10f, 80f);
            }

            freeDistance = Mathf.Clamp(freeDistance - Input.mouseScrollDelta.y * 2f, 5f, 150f);
            var move = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            var lift = 0f;
            if (Input.GetKey(KeyCode.E))
            {
                lift += 1f;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                lift -= 1f;
            }

            transform.position += (transform.TransformDirection(move) + Vector3.up * lift) * (12f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(freePitch, freeYaw, 0f);
        }

        private void FrameTrajectory()
        {
            if (trajectoryPoints == null || trajectoryPoints.Length == 0)
            {
                transform.position = new Vector3(0f, 30f, -45f);
                transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                return;
            }

            var bounds = new Bounds(trajectoryPoints[0], Vector3.zero);
            for (var i = 1; i < trajectoryPoints.Length; i++)
            {
                bounds.Encapsulate(trajectoryPoints[i]);
            }

            var size = Mathf.Max(bounds.size.x, bounds.size.z, 20f);
            transform.position = bounds.center + new Vector3(0f, Mathf.Max(size * 0.65f, 18f), -Mathf.Max(size * 0.8f, 28f));
            transform.LookAt(bounds.center);
        }
    }
}
