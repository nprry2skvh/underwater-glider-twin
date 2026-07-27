using NUnit.Framework;
using UnderwaterGliderTwin.Visualization;
using UnityEngine;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class TwinCameraControllerTests
    {
        [Test]
        public void SetMode_SupportsTopAndSideViewsWithoutChangingTheTrajectory()
        {
            var cameraObject = new GameObject("TwinCameraControllerTest");
            var target = new GameObject("GliderTarget");
            var controller = cameraObject.AddComponent<TwinCameraController>();
            var trajectory = new[] { new Vector3(-10f, -5f, -8f), new Vector3(12f, -20f, 14f) };

            try
            {
                controller.Initialize(target.transform, trajectory);
                controller.SetMode(CameraMode.Top);
                Assert.That(controller.CurrentMode, Is.EqualTo(CameraMode.Top));
                Assert.That(cameraObject.transform.position.y, Is.GreaterThan(trajectory[0].y));

                controller.SetMode(CameraMode.Side);
                Assert.That(controller.CurrentMode, Is.EqualTo(CameraMode.Side));
                Assert.That(Mathf.Abs(cameraObject.transform.position.x), Is.GreaterThan(10f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ResetView_ReturnsToTheRecommendedGlobalView()
        {
            var cameraObject = new GameObject("TwinCameraResetTest");
            var target = new GameObject("GliderTarget");
            var controller = cameraObject.AddComponent<TwinCameraController>();

            try
            {
                controller.Initialize(target.transform, new[] { Vector3.zero, new Vector3(30f, -10f, 20f) });
                controller.SetMode(CameraMode.Orbit);
                controller.ResetView();

                Assert.That(controller.CurrentMode, Is.EqualTo(CameraMode.Global));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
