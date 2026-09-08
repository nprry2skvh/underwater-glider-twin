using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class FollowCameraRig
    {
        public const float FollowDistance = 8f;
        public const float FollowHeight = 3.8f;

        private const float LateralOffset = 3f;
        private const float LookAhead = 1.5f;
        private const float PositionBlend = 0.18f;
        private const float RotationBlend = 0.16f;

        public Vector3 ComputeDesiredPosition(Vector3 focusPosition, Vector3 focusForward)
        {
            var horizontalForward = ResolveHorizontalForward(focusForward);
            var horizontalRight = Vector3.Cross(Vector3.up, horizontalForward).normalized;
            return focusPosition
                - horizontalForward * FollowDistance
                + horizontalRight * LateralOffset
                + Vector3.up * FollowHeight;
        }

        public Pose ComputeDesiredPose(Vector3 focusPosition, Vector3 focusForward)
        {
            var horizontalForward = ResolveHorizontalForward(focusForward);
            var desiredPosition = ComputeDesiredPosition(focusPosition, horizontalForward);
            var lookTarget = focusPosition + Vector3.up * 1.2f + horizontalForward * LookAhead;
            var desiredRotation = Quaternion.LookRotation((lookTarget - desiredPosition).normalized, Vector3.up);
            return new Pose(desiredPosition, desiredRotation);
        }

        public Pose Step(Vector3 currentPosition, Quaternion currentRotation, Vector3 focusPosition, Vector3 focusForward)
        {
            var desiredPose = ComputeDesiredPose(focusPosition, focusForward);
            var nextPosition = Vector3.Lerp(currentPosition, desiredPose.position, PositionBlend);
            var lookDirection = focusPosition + Vector3.up * 1.2f + ResolveHorizontalForward(focusForward) * LookAhead - nextPosition;
            var desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            var nextRotation = Quaternion.Slerp(currentRotation, desiredRotation, RotationBlend);
            return new Pose(nextPosition, nextRotation);
        }

        private static Vector3 ResolveHorizontalForward(Vector3 focusForward)
        {
            var horizontalForward = Vector3.ProjectOnPlane(focusForward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return horizontalForward.normalized;
        }
    }
}
